from shared.config.logging_config import get_logger
"""
Document Processing Service

Orchestrates the complete document analysis workflow including Azure Document Intelligence
integration, confidence evaluation, and blob storage for low-confidence documents.
"""

import os
import uuid
import asyncio
from typing import Optional, Dict, Any, Tuple, BinaryIO
from datetime import datetime

# Simple logging setup
import logging

# Import models and services
from models import (
    DocumentAnalysisUrlRequest,
    DocumentAnalysisFileRequest,
    DocumentAnalysisResponse,
    SerialFieldResult,
    AnalysisStatus,
    FieldExtractionStatus,
    ErrorResponse,
    ErrorCode
)
from services.document_intelligence_service import DocumentIntelligenceService
from repositories.blob_storage_repository import BlobStorageRepository


class DocumentProcessingService:
    """
    Service for orchestrating complete document analysis workflows.
    
    Coordinates Azure Document Intelligence API calls, confidence evaluation,
    field extraction, and storage of low-confidence documents for review.
    
    Attributes:
        doc_intel_service (DocumentIntelligenceService): Azure Document Intelligence service
        blob_repository (BlobStorageRepository): Blob storage repository  
        logger: Structured logger instance
        confidence_threshold (float): Global confidence threshold for field acceptance
        enable_blob_storage (bool): Whether to store low-confidence documents
    """

    def __init__(
        self,
        doc_intel_service: Optional[DocumentIntelligenceService] = None,
        blob_repository: Optional[BlobStorageRepository] = None,
        confidence_threshold: Optional[float] = None,
        enable_blob_storage: Optional[bool] = None
    ):
        """
        Initialize the Document Processing service with all dependencies and configuration.
        
        This constructor sets up the complete document processing pipeline including:
        - Azure Document Intelligence service for AI-powered field extraction
        - Blob storage repository for low-confidence document management
        - Confidence threshold evaluation for document quality assessment
        - Environment-based configuration for production flexibility
        
        The service follows a dependency injection pattern where external services can be
        provided for testing, or will be auto-created using environment configuration.
        
        Args:
            doc_intel_service (Optional[DocumentIntelligenceService]): 
                Pre-configured Azure Document Intelligence service instance.
                If None, creates a new instance using environment variables:
                - DOCUMENT_INTELLIGENCE_ENDPOINT
                - DOCUMENT_INTELLIGENCE_KEY
                - DOCUMENT_INTELLIGENCE_API_VERSION
                - DEFAULT_MODEL_ID
                
            blob_repository (Optional[BlobStorageRepository]): 
                Pre-configured blob storage repository for document storage.
                If None and blob storage is enabled, creates new instance using:
                - AZURE_STORAGE_CONNECTION_STRING
                - BLOB_CONTAINER_PREFIX
                
            confidence_threshold (Optional[float]): 
                Minimum confidence score (0.0-1.0) for accepting extracted fields.
                If None, uses CONFIDENCE_THRESHOLD environment variable (default: 0.7).
                Documents below this threshold are stored for manual review.
                
            enable_blob_storage (Optional[bool]): 
                Whether to enable automatic storage of low-confidence documents.
                If None, uses ENABLE_BLOB_STORAGE environment variable (default: true).
                When enabled, documents below confidence threshold are stored with metadata.
                
        Raises:
            Exception: If required Azure Document Intelligence configuration is missing
            Exception: If blob storage is enabled but Azure Storage configuration is invalid
        """
        # Get logger for this service
        self.logger = get_logger('warehouse_returns.document_intelligence.processing')
        
        # Initialize services (create if not provided)
        self.doc_intel_service = doc_intel_service or DocumentIntelligenceService()
        
        # Blob storage is optional (may be disabled in some environments)
        if enable_blob_storage is None:
            enable_blob_storage = os.getenv('ENABLE_BLOB_STORAGE', 'true').lower() == 'true'
        
        self.enable_blob_storage = enable_blob_storage
        
        self.logger.info(
            f"[BLOB-STORAGE-INIT] Initializing blob storage configuration - "
            f"Enabled: {self.enable_blob_storage}, "
            f"Env-Setting: {os.getenv('ENABLE_BLOB_STORAGE', 'not_set')}, "
            f"Connection-String-Set: {bool(os.getenv('AZURE_STORAGE_CONNECTION_STRING'))}"
        )
        
        if self.enable_blob_storage:
            try:
                # Get container name from environment variable
                container_name = os.getenv('BLOB_CONTAINER_PREFIX', 'document-intelligence')
                self.blob_repository = blob_repository or BlobStorageRepository(
                    container_name=container_name
                )
                self.logger.info(
                    f"[BLOB-STORAGE-INIT] Blob storage repository initialized successfully - "
                    f"Container: {getattr(self.blob_repository, 'container_name', 'unknown')}, "
                    f"Container-From-Env: {container_name}"
                )
            except Exception as e:
                self.logger.warning(
                    f"[BLOB-STORAGE-INIT] Blob storage not available, disabling low-confidence document storage - "
                    f"Error: {type(e).__name__}: {str(e)}"
                )
                self.enable_blob_storage = False
                self.blob_repository = None
        else:
            self.logger.info(
                "[BLOB-STORAGE-INIT] Blob storage disabled via configuration"
            )
            self.blob_repository = None
        
        # Configuration
        if confidence_threshold is None:
            confidence_threshold = float(os.getenv('CONFIDENCE_THRESHOLD', '0.7'))
        
        self.confidence_threshold = confidence_threshold
        
        self.logger.info(
            "Document Processing service initialized",
            confidence_threshold=self.confidence_threshold,
            enable_blob_storage=self.enable_blob_storage
        )

    async def process_document_from_url(
        self,
        request: DocumentAnalysisUrlRequest,
        correlation_id: Optional[str] = None
    ) -> DocumentAnalysisResponse:
        """
        Process a document from URL through the complete analysis workflow.
        
        Args:
            request (DocumentAnalysisUrlRequest): URL-based analysis request
            correlation_id (Optional[str]): Correlation ID for tracing
            
        Returns:
            DocumentAnalysisResponse: Complete analysis results with field extraction
        """
        # Generate analysis ID and correlation ID if not provided
        analysis_id = f"analysis-{uuid.uuid4()}"
        if not correlation_id:
            correlation_id = f"corr-{uuid.uuid4()}"
        
        start_time = datetime.utcnow()
        
        self.logger.info(
            "Starting document processing from URL",
            analysis_id=analysis_id,
            document_url=str(request.document_url),
            model_id=request.model_id,
            confidence_threshold=request.confidence_threshold,
            correlation_id=correlation_id
        )
        
        try:
            # Step 1: Analyze document with Azure Document Intelligence
            azure_response, error = await self.doc_intel_service.analyze_document_from_url(
                request, correlation_id
            )
            
            if error or not azure_response:
                return self._create_failed_response(
                    analysis_id=analysis_id,
                    error=error,
                    document_metadata={
                        "source_type": "url",
                        "document_url": str(request.document_url),
                        "document_type": request.document_type
                    },
                    start_time=start_time,
                    correlation_id=correlation_id
                )
            
            # Step 2: Extract Serial field from Azure response
            serial_value, azure_confidence, extraction_success = azure_response.get_serial_extraction()
            
            # Step 3: Evaluate confidence against threshold
            effective_threshold = request.confidence_threshold or self.confidence_threshold
            meets_threshold = azure_confidence >= effective_threshold
            
            self.logger.info(
                "Field extraction completed",
                analysis_id=analysis_id,
                serial_value=serial_value,
                confidence=azure_confidence,
                threshold=effective_threshold,
                meets_threshold=meets_threshold,
                correlation_id=correlation_id
            )
            
            # Step 4: Create serial field result
            serial_field = self._create_serial_field_result(
                serial_value, azure_confidence, meets_threshold, extraction_success
            )
            
            # Step 5: Determine overall status
            if extraction_success and meets_threshold:
                status = AnalysisStatus.SUCCEEDED
            elif extraction_success and not meets_threshold:
                status = AnalysisStatus.REQUIRES_REVIEW
            else:
                status = AnalysisStatus.FAILED
            
            # Step 6: Handle low-confidence documents (store for review if enabled)
            blob_storage_info = None
            
            self.logger.info(
                f"[BLOB-STORAGE-DECISION] Evaluating blob storage criteria - Analysis: {analysis_id}, "
                f"Confidence: {azure_confidence} vs Threshold: {effective_threshold}, "
                f"Meets-Threshold: {meets_threshold}, Extraction-Success: {extraction_success}, "
                f"Blob-Enabled: {self.enable_blob_storage}, Repo-Available: {bool(self.blob_repository)}, "
                f"URL: {str(request.document_url)[:50]}..., Correlation: {correlation_id}"
            )
            
            if (not meets_threshold and extraction_success and 
                self.enable_blob_storage and self.blob_repository):
                
                self.logger.info(
                    "[BLOB-STORAGE-DECISION] Document qualifies for blob storage - proceeding with URL download",
                    analysis_id=analysis_id,
                    correlation_id=correlation_id
                )
                
                try:
                    # For URL documents, we need to download first to store in blob
                    document_data = await self._download_document_from_url(str(request.document_url))
                    
                    if document_data:
                        blob_info, blob_error = await self._store_low_confidence_document(
                            analysis_id=analysis_id,
                            document_data=document_data,
                            filename=f"url_document_{analysis_id}",
                            content_type="application/octet-stream",  # Unknown from URL
                            serial_field=serial_field,
                            request_metadata={
                                "source_type": "url",
                                "document_url": str(request.document_url),
                                "model_id": request.model_id,
                                "confidence_threshold": effective_threshold
                            },
                            correlation_id=correlation_id
                        )
                        
                        if blob_info:
                            blob_storage_info = blob_info
                        
                except Exception as e:
                    self.logger.warning(
                        "[BLOB-STORAGE-ERROR] Failed to store low-confidence URL document",
                        analysis_id=analysis_id,
                        exception=str(e),
                        exception_type=type(e).__name__,
                        correlation_id=correlation_id
                    )
            else:
                skip_reasons = []
                if meets_threshold:
                    skip_reasons.append(f"confidence_meets_threshold({azure_confidence}>={effective_threshold})")
                if not extraction_success:
                    skip_reasons.append("extraction_failed")
                if not self.enable_blob_storage:
                    skip_reasons.append("blob_storage_disabled")
                if not self.blob_repository:
                    skip_reasons.append("blob_repository_unavailable")
                
                self.logger.info(
                    f"[BLOB-STORAGE-DECISION] Skipping blob storage - Analysis: {analysis_id}, "
                    f"Skip-Reasons: {skip_reasons}, URL: {str(request.document_url)[:50]}..., Correlation: {correlation_id}"
                )
            
            # Step 7: Create and return response
            completed_time = datetime.utcnow()
            processing_time_ms = int((completed_time - start_time).total_seconds() * 1000)
            
            response = DocumentAnalysisResponse(
                analysis_id=analysis_id,
                status=status,
                serial_field=serial_field,
                document_metadata={
                    "source_type": "url",
                    "document_url": str(request.document_url),
                    "document_type": request.document_type,
                    "model_id": request.model_id
                },
                processing_metadata={
                    "processing_time_ms": processing_time_ms,
                    "azure_api_version": azure_response.analyzeResult.apiVersion if azure_response.analyzeResult else "unknown",
                    "confidence_threshold": effective_threshold,
                    "model_used": request.model_id
                },
                blob_storage_info=blob_storage_info,
                created_at=start_time,
                completed_at=completed_time,
                correlation_id=correlation_id
            )
            
            self.logger.info(
                "Document processing completed successfully",
                analysis_id=analysis_id,
                status=status,
                processing_time_ms=processing_time_ms,
                correlation_id=correlation_id
            )
            
            return response
            
        except Exception as e:
            self.logger.error(
                "Unexpected error during document processing",
                analysis_id=analysis_id,
                exception=e,
                correlation_id=correlation_id
            )
            
            return self._create_failed_response(
                analysis_id=analysis_id,
                error=ErrorResponse(
                    error_code=ErrorCode.PROCESSING_ERROR,
                    message="Unexpected error during document processing",
                    details=str(e),
                    correlation_id=correlation_id
                ),
                document_metadata={
                    "source_type": "url",
                    "document_url": str(request.document_url),
                    "document_type": request.document_type
                },
                start_time=start_time,
                correlation_id=correlation_id
            )

    async def process_document_from_bytes(
        self,
        document_data: bytes,
        filename: str,
        content_type: str,
        request: DocumentAnalysisFileRequest,
        correlation_id: Optional[str] = None
    ) -> DocumentAnalysisResponse:
        """
        Process a document from byte data through the complete analysis workflow.
        
        This method orchestrates the end-to-end document processing pipeline:
        1. Validates input document data and metadata
        2. Sends document to Azure Document Intelligence for AI analysis
        3. Extracts serial number fields with confidence scoring
        4. Evaluates confidence against configured thresholds
        5. Routes low-confidence documents to blob storage for manual review
        6. Returns comprehensive analysis results with storage information
        
        The method implements the core business logic for document quality assessment:
        - High confidence (≥ threshold): Results returned immediately
        - Low confidence (< threshold): Document stored for review and retraining
        - Extraction failures: Error responses with detailed context
        
        Args:
            document_data (bytes): 
                Raw binary document content (PDF, JPEG, PNG, TIFF formats).
                Must be valid document format supported by Azure Document Intelligence.
                Maximum size controlled by MAX_FILE_SIZE_MB environment variable.
                
            filename (str): 
                Original document filename for metadata and storage organization.
                Used for file extension detection and storage path generation.
                Should include file extension for proper MIME type handling.
                
            content_type (str): 
                MIME type of the document (e.g., 'application/pdf', 'image/jpeg').
                Used for Azure Document Intelligence format specification.
                Must match SUPPORTED_CONTENT_TYPES environment configuration.
                
            request (DocumentAnalysisFileRequest): 
                Structured analysis request with processing parameters:
                - model_id: Azure Document Intelligence model identifier
                - document_type: Business context for the document
                - confidence_threshold: Optional override for quality threshold
                
            correlation_id (Optional[str]): 
                Unique identifier for request tracking across service boundaries.
                If None, generates new correlation ID for this processing session.
                Used for distributed tracing and log correlation.
                
        Returns:
            DocumentAnalysisResponse: 
                Comprehensive analysis results containing:
                - analysis_id: Unique identifier for this analysis
                - status: Processing outcome (completed/failed)
                - serial_field: Extracted serial number with confidence score
                - blob_storage_info: Storage details if document was stored
                - processing_metadata: Performance and model information
                - correlation_id: Request tracking identifier
                
        Raises:
            Exception: Critical processing failures (network, authentication, etc.)
            
        Example:
            >>> document_bytes = open('return_doc.pdf', 'rb').read()
            >>> request = DocumentAnalysisFileRequest(model_id='serialnumber')
            >>> response = await service.process_document_from_bytes(
            ...     document_bytes, 'return_doc.pdf', 'application/pdf', request
            ... )
            >>> print(f"Serial: {response.serial_field.value}")
            >>> if response.blob_storage_info:
            ...     print(f"Stored for review: {response.blob_storage_info['storage_url']}")
        """
        # Generate analysis ID and correlation ID if not provided
        analysis_id = f"analysis-{uuid.uuid4()}"
        if not correlation_id:
            correlation_id = f"corr-{uuid.uuid4()}"
        
        start_time = datetime.utcnow()
        
        self.logger.info(
            "Starting document processing from file upload",
            analysis_id=analysis_id,
            filename=filename,
            content_type=content_type,
            file_size_bytes=len(document_data),
            model_id=request.model_id,
            confidence_threshold=request.confidence_threshold,
            correlation_id=correlation_id
        )
        
        try:
            # Step 1: Analyze document with Azure Document Intelligence
            azure_response, error = await self.doc_intel_service.analyze_document_from_bytes(
                document_data, request, filename, content_type, correlation_id
            )
            
            if error or not azure_response:
                return self._create_failed_response(
                    analysis_id=analysis_id,
                    error=error,
                    document_metadata={
                        "source_type": "file_upload",
                        "filename": filename,
                        "content_type": content_type,
                        "file_size_bytes": len(document_data),
                        "document_type": request.document_type
                    },
                    start_time=start_time,
                    correlation_id=correlation_id
                )
            
            # Step 2: Extract Serial field from Azure response
            serial_value, azure_confidence, extraction_success = azure_response.get_serial_extraction()
            
            # Step 3: Evaluate confidence against threshold
            effective_threshold = request.confidence_threshold or self.confidence_threshold
            meets_threshold = azure_confidence >= effective_threshold
            
            self.logger.info(
                "Field extraction completed",
                analysis_id=analysis_id,
                filename=filename,
                serial_value=serial_value,
                confidence=azure_confidence,
                threshold=effective_threshold,
                meets_threshold=meets_threshold,
                correlation_id=correlation_id
            )
            
            # Step 4: Create serial field result
            serial_field = self._create_serial_field_result(
                serial_value, azure_confidence, meets_threshold, extraction_success
            )
            
            # Step 5: Determine overall status
            if extraction_success and meets_threshold:
                status = AnalysisStatus.SUCCEEDED
            elif extraction_success and not meets_threshold:
                status = AnalysisStatus.REQUIRES_REVIEW
            else:
                status = AnalysisStatus.FAILED
            
            # Step 6: Handle low-confidence documents (store for review if enabled)
            blob_storage_info = None
            
            self.logger.info(
                f"[BLOB-STORAGE-DECISION] Evaluating blob storage criteria - Analysis: {analysis_id}, "
                f"Confidence: {azure_confidence} vs Threshold: {effective_threshold}, "
                f"Meets-Threshold: {meets_threshold}, Extraction-Success: {extraction_success}, "
                f"Blob-Enabled: {self.enable_blob_storage}, Repo-Available: {bool(self.blob_repository)}, "
                f"Filename: {filename}, Correlation: {correlation_id}"
            )
            
            if (not meets_threshold and extraction_success and 
                self.enable_blob_storage and self.blob_repository):
                
                self.logger.info(
                    "[BLOB-STORAGE-DECISION] Document qualifies for blob storage - proceeding with storage",
                    analysis_id=analysis_id,
                    filename=filename,
                    file_size_bytes=len(document_data),
                    correlation_id=correlation_id
                )
                
                blob_info, blob_error = await self._store_low_confidence_document(
                    analysis_id=analysis_id,
                    document_data=document_data,
                    filename=filename,
                    content_type=content_type,
                    serial_field=serial_field,
                    request_metadata={
                        "source_type": "file_upload",
                        "filename": filename,
                        "content_type": content_type,
                        "file_size_bytes": len(document_data),
                        "model_id": request.model_id,
                        "confidence_threshold": effective_threshold
                    },
                    correlation_id=correlation_id
                )
                
                if blob_info:
                    blob_storage_info = blob_info
                    self.logger.info(
                        "[BLOB-STORAGE-SUCCESS] Document stored successfully",
                        analysis_id=analysis_id,
                        blob_path=blob_info.get('document_blob_path'),
                        storage_url=blob_info.get('storage_url'),
                        correlation_id=correlation_id
                    )
                elif blob_error:
                    self.logger.warning(
                        "[BLOB-STORAGE-ERROR] Failed to store low-confidence document",
                        analysis_id=analysis_id,
                        error_code=blob_error.error_code,
                        error_message=blob_error.message,
                        error_details=blob_error.details,
                        correlation_id=correlation_id
                    )
            else:
                skip_reasons = []
                if meets_threshold:
                    skip_reasons.append(f"confidence_meets_threshold({azure_confidence}>={effective_threshold})")
                if not extraction_success:
                    skip_reasons.append("extraction_failed")
                if not self.enable_blob_storage:
                    skip_reasons.append("blob_storage_disabled")
                if not self.blob_repository:
                    skip_reasons.append("blob_repository_unavailable")
                
                self.logger.info(
                    f"[BLOB-STORAGE-DECISION] Skipping blob storage - Analysis: {analysis_id}, "
                    f"Skip-Reasons: {skip_reasons}, Filename: {filename}, Correlation: {correlation_id}"
                )
            
            # Step 7: Create and return response
            completed_time = datetime.utcnow()
            processing_time_ms = int((completed_time - start_time).total_seconds() * 1000)
            
            response = DocumentAnalysisResponse(
                analysis_id=analysis_id,
                status=status,
                serial_field=serial_field,
                document_metadata={
                    "source_type": "file_upload",
                    "filename": filename,
                    "content_type": content_type,
                    "file_size_bytes": len(document_data),
                    "document_type": request.document_type,
                    "model_id": request.model_id
                },
                processing_metadata={
                    "processing_time_ms": processing_time_ms,
                    "azure_api_version": azure_response.analyzeResult.apiVersion if azure_response.analyzeResult else "unknown",
                    "confidence_threshold": effective_threshold,
                    "model_used": request.model_id
                },
                blob_storage_info=blob_storage_info,
                created_at=start_time,
                completed_at=completed_time,
                correlation_id=correlation_id
            )
            
            self.logger.info(
                "Document processing completed successfully",
                analysis_id=analysis_id,
                filename=filename,
                status=status,
                processing_time_ms=processing_time_ms,
                correlation_id=correlation_id
            )
            
            return response
            
        except Exception as e:
            self.logger.error(
                "Unexpected error during document processing",
                analysis_id=analysis_id,
                filename=filename,
                exception=e,
                correlation_id=correlation_id
            )
            
            return self._create_failed_response(
                analysis_id=analysis_id,
                error=ErrorResponse(
                    error_code=ErrorCode.PROCESSING_ERROR,
                    message="Unexpected error during document processing",
                    details=str(e),
                    correlation_id=correlation_id
                ),
                document_metadata={
                    "source_type": "file_upload",
                    "filename": filename,
                    "content_type": content_type,
                    "file_size_bytes": len(document_data),
                    "document_type": request.document_type
                },
                start_time=start_time,
                correlation_id=correlation_id
            )

    def _create_serial_field_result(
        self,
        serial_value: Optional[str],
        confidence: float,
        meets_threshold: bool,
        extraction_success: bool
    ) -> SerialFieldResult:
        """
        Create a SerialFieldResult from extraction data.
        
        Args:
            serial_value (Optional[str]): Extracted serial number
            confidence (float): Confidence score from Azure
            meets_threshold (bool): Whether confidence meets threshold
            extraction_success (bool): Whether extraction was successful
            
        Returns:
            SerialFieldResult: Formatted serial field result
        """
        if not extraction_success:
            status = FieldExtractionStatus.NOT_FOUND
        elif meets_threshold:
            status = FieldExtractionStatus.EXTRACTED
        else:
            status = FieldExtractionStatus.LOW_CONFIDENCE
        
        return SerialFieldResult(
            field_name="Serial",
            value=serial_value if meets_threshold else None,  # Only return value if confidence is sufficient
            confidence=confidence,
            status=status,
            extraction_metadata={
                "meets_threshold": meets_threshold,
                "extraction_success": extraction_success,
                "raw_extracted_value": serial_value  # Keep raw value for debugging
            }
        )

    async def _store_low_confidence_document(
        self,
        analysis_id: str,
        document_data: bytes,
        filename: str,
        content_type: str,
        serial_field: SerialFieldResult,
        request_metadata: Dict[str, Any],
        correlation_id: Optional[str] = None
    ) -> Tuple[Optional[Dict[str, str]], Optional[ErrorResponse]]:
        """
        Store low-confidence document in blob storage for review.
        
        Args:
            analysis_id (str): Analysis identifier
            document_data (bytes): Document file content
            filename (str): Original filename
            content_type (str): MIME type
            serial_field (SerialFieldResult): Serial field extraction result
            request_metadata (Dict[str, Any]): Request metadata
            correlation_id (Optional[str]): Correlation ID for tracing
            
        Returns:
            Tuple[Optional[Dict[str, str]], Optional[ErrorResponse]]:
                Storage info and error (if any)
        """
        self.logger.info(
            "[BLOB-STORAGE-STORE] Starting low-confidence document storage",
            analysis_id=analysis_id,
            filename=filename,
            content_type=content_type,
            file_size_bytes=len(document_data),
            serial_confidence=serial_field.confidence,
            raw_extracted_value=serial_field.extraction_metadata.get('raw_extracted_value'),
            correlation_id=correlation_id
        )
        
        analysis_metadata = {
            "serial_field": {
                "value": serial_field.value,
                "confidence": serial_field.confidence,
                "status": serial_field.status,
                "extraction_metadata": serial_field.extraction_metadata
            },
            "request_metadata": request_metadata,
            "requires_review_reason": "Low confidence score"
        }
        
        try:
            result = self.blob_repository.store_low_confidence_document(
                analysis_id=analysis_id,
                document_data=document_data,
                filename=filename,
                content_type=content_type,
                analysis_metadata=analysis_metadata,
                correlation_id=correlation_id
            )
            
            self.logger.info(
                "[BLOB-STORAGE-STORE] Blob repository call completed",
                analysis_id=analysis_id,
                success=result[0] is not None,
                error=result[1] is not None,
                correlation_id=correlation_id
            )
            
            return result
            
        except Exception as e:
            self.logger.error(
                "[BLOB-STORAGE-STORE] Exception during blob storage",
                analysis_id=analysis_id,
                exception=str(e),
                exception_type=type(e).__name__,
                correlation_id=correlation_id
            )
            
            error_response = ErrorResponse(
                error_code=ErrorCode.BLOB_STORAGE_ERROR,
                message="Exception during blob storage",
                details=str(e),
                correlation_id=correlation_id
            )
            return None, error_response

    async def _download_document_from_url(self, url: str) -> Optional[bytes]:
        """
        Download document content from URL.
        
        Args:
            url (str): Document URL to download
            
        Returns:
            Optional[bytes]: Document content or None if failed
        """
        try:
            import aiohttp
            
            async with aiohttp.ClientSession() as session:
                async with session.get(url) as response:
                    if response.status == 200:
                        return await response.read()
                    else:
                        self.logger.warning(
                            "Failed to download document from URL",
                            url=url,
                            status_code=response.status
                        )
                        return None
                        
        except Exception as e:
            self.logger.error(
                "Error downloading document from URL",
                url=url,
                exception=e
            )
            return None

    def _create_failed_response(
        self,
        analysis_id: str,
        error: Optional[ErrorResponse],
        document_metadata: Dict[str, Any],
        start_time: datetime,
        correlation_id: Optional[str] = None
    ) -> DocumentAnalysisResponse:
        """
        Create a failed analysis response.
        
        Args:
            analysis_id (str): Analysis identifier
            error (Optional[ErrorResponse]): Error information
            document_metadata (Dict[str, Any]): Document metadata
            start_time (datetime): Processing start time
            correlation_id (Optional[str]): Correlation ID
            
        Returns:
            DocumentAnalysisResponse: Failed response
        """
        completed_time = datetime.utcnow()
        processing_time_ms = int((completed_time - start_time).total_seconds() * 1000)
        
        return DocumentAnalysisResponse(
            analysis_id=analysis_id,
            status=AnalysisStatus.FAILED,
            serial_field=SerialFieldResult(
                field_name="Serial",
                value=None,
                confidence=0.0,
                status=FieldExtractionStatus.EXTRACTION_ERROR
            ),
            document_metadata=document_metadata,
            processing_metadata={
                "processing_time_ms": processing_time_ms,
                "confidence_threshold": self.confidence_threshold
            },
            blob_storage_info=None,
            created_at=start_time,
            completed_at=completed_time,
            correlation_id=correlation_id,
            error_details={
                "error_code": error.error_code if error else ErrorCode.PROCESSING_ERROR,
                "message": error.message if error else "Processing failed",
                "details": error.details if error else "Unknown error occurred"
            }
        )

    async def health_check(self) -> Dict[str, Any]:
        """
        Perform comprehensive health check on all service dependencies.
        
        Returns:
            Dict[str, Any]: Health check results
        """
        self.logger.info("Performing document processing service health check")
        
        health_results = {
            "service": "document_processing",
            "status": "healthy",
            "timestamp": datetime.utcnow().isoformat(),
            "components": {}
        }
        
        try:
            # Check Document Intelligence service
            doc_intel_health = self.doc_intel_service.health_check()
            health_results["components"]["document_intelligence"] = doc_intel_health
            
            # Check Blob Storage service (if enabled)
            if self.enable_blob_storage and self.blob_repository:
                blob_health = self.blob_repository.health_check()
                health_results["components"]["blob_storage"] = blob_health
            else:
                health_results["components"]["blob_storage"] = {
                    "status": "disabled",
                    "message": "Blob storage not enabled"
                }
            
            # Determine overall status
            component_statuses = [
                comp.get("status", "unhealthy") 
                for comp in health_results["components"].values()
                if comp.get("status") not in ["disabled"]
            ]
            
            if all(status == "healthy" for status in component_statuses):
                health_results["status"] = "healthy"
            else:
                health_results["status"] = "degraded"
            
            self.logger.info(
                "Document processing service health check completed",
                status=health_results["status"]
            )
            
        except Exception as e:
            self.logger.error(
                "Document processing service health check failed",
                exception=e
            )
            health_results["status"] = "unhealthy"
            health_results["error"] = str(e)
        
        return health_results
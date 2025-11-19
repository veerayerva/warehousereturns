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
        Initialize the Document Processing service.
        
        Args:
            doc_intel_service (Optional[DocumentIntelligenceService]): Document Intelligence service
            blob_repository (Optional[BlobStorageRepository]): Blob storage repository
            confidence_threshold (Optional[float]): Confidence threshold (default from env)
            enable_blob_storage (Optional[bool]): Enable blob storage (default from env)
        """
        # Get logger for this service
        self.logger = get_logger('warehouse_returns.document_intelligence.processing')
        
        # Initialize services (create if not provided)
        self.doc_intel_service = doc_intel_service or DocumentIntelligenceService()
        
        # Blob storage is optional (may be disabled in some environments)
        if enable_blob_storage is None:
            enable_blob_storage = os.getenv('ENABLE_BLOB_STORAGE', 'true').lower() == 'true'
        
        self.enable_blob_storage = enable_blob_storage
        
        if self.enable_blob_storage:
            try:
                self.blob_repository = blob_repository or BlobStorageRepository()
            except Exception as e:
                self.logger.warning(
                    "Blob storage not available, disabling low-confidence document storage",
                    exception=e
                )
                self.enable_blob_storage = False
                self.blob_repository = None
        else:
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
            if (not meets_threshold and extraction_success and 
                self.enable_blob_storage and self.blob_repository):
                
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
                        "Failed to store low-confidence URL document",
                        analysis_id=analysis_id,
                        exception=e,
                        correlation_id=correlation_id
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
        
        Args:
            document_data (bytes): Document file content
            filename (str): Original filename
            content_type (str): MIME type of the document
            request (DocumentAnalysisFileRequest): File-based analysis request
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
            if (not meets_threshold and extraction_success and 
                self.enable_blob_storage and self.blob_repository):
                
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
                elif blob_error:
                    self.logger.warning(
                        "Failed to store low-confidence document",
                        analysis_id=analysis_id,
                        error_code=blob_error.error_code,
                        correlation_id=correlation_id
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
        
        return await self.blob_repository.store_low_confidence_document(
            analysis_id=analysis_id,
            document_data=document_data,
            filename=filename,
            content_type=content_type,
            analysis_metadata=analysis_metadata,
            correlation_id=correlation_id
        )

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
                blob_health = await self.blob_repository.health_check()
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
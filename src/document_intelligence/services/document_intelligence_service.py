"""
Azure Document Intelligence Service

Integrates with Azure Document Intelligence API to analyze documents and extract
fields with confidence scores. Handles both URL-based and file upload scenarios.
"""

import os
import asyncio
import aiohttp
import json
import time
from typing import Optional, Dict, Any, Tuple, BinaryIO, List
from datetime import datetime, timedelta
from azure.ai.documentintelligence import DocumentIntelligenceClient
from azure.ai.documentintelligence.models import AnalyzeDocumentRequest
from azure.core.credentials import AzureKeyCredential
from azure.core.exceptions import HttpResponseError, ServiceRequestError

# Simple logging setup
import logging

# Import shared utilities
import sys
import os
sys.path.append(os.path.join(os.path.dirname(__file__), '..', '..'))
from shared.config.logging_config import get_logger

# Import models
from models import (
    AzureDocIntelResponse,
    DocumentAnalysisUrlRequest,
    DocumentAnalysisFileRequest,
    ErrorResponse,
    ErrorCode
)


class DocumentIntelligenceService:
    """
    Service for integrating with Azure Document Intelligence API.
    
    Provides methods to analyze documents from URLs or file uploads,
    extract specified fields (like Serial numbers), and handle
    confidence scoring and error scenarios.
    
    Attributes:
        endpoint (str): Azure Document Intelligence service endpoint
        credential (AzureKeyCredential): Authentication credential
        client (DocumentIntelligenceClient): Azure Document Intelligence client
        logger: Structured logger instance
        default_model_id (str): Default model ID for document analysis
        max_retry_attempts (int): Maximum retry attempts for transient failures
        retry_delay_seconds (int): Base delay between retry attempts
    """

    def __init__(
        self,
        endpoint: Optional[str] = None,
        api_key: Optional[str] = None,
        default_model_id: str = "serialnumber",
        max_retry_attempts: int = 3,
        retry_delay_seconds: int = 2
    ):
        """
        Initialize the Document Intelligence service.
        
        Args:
            endpoint (Optional[str]): Azure Document Intelligence endpoint URL
            api_key (Optional[str]): Azure Document Intelligence API key
            default_model_id (str): Default model ID for document analysis
            max_retry_attempts (int): Maximum retry attempts for API calls
            retry_delay_seconds (int): Base delay between retries
            
        Raises:
            ValueError: If endpoint or API key is not provided and not in environment
        """
        # Get logger for this service
        self.logger = get_logger('warehouse_returns.document_intelligence.service')
        
        # Get configuration from environment or parameters
        self.endpoint = endpoint or os.getenv('DOCUMENT_INTELLIGENCE_ENDPOINT')
        api_key = api_key or os.getenv('DOCUMENT_INTELLIGENCE_KEY')
        
        # Validate required configuration
        if not self.endpoint:
            error_msg = "Document Intelligence endpoint is required. Set DOCUMENT_INTELLIGENCE_ENDPOINT environment variable."
            self.logger.error("Missing Document Intelligence endpoint configuration")
            raise ValueError(error_msg)
            
        if not api_key:
            error_msg = "Document Intelligence API key is required. Set DOCUMENT_INTELLIGENCE_KEY environment variable."
            self.logger.error("Missing Document Intelligence API key configuration")
            raise ValueError(error_msg)
        
        # Initialize Azure Document Intelligence client
        try:
            self.credential = AzureKeyCredential(api_key)
            self.client = DocumentIntelligenceClient(
                endpoint=self.endpoint,
                credential=self.credential
            )
            self.logger.info(
                "Document Intelligence service initialized successfully",
                endpoint=self.endpoint,
                model_id=default_model_id
            )
        except Exception as e:
            self.logger.error(
                "Failed to initialize Document Intelligence client",
                endpoint=self.endpoint,
                exception=e
            )
            raise
        
        # Service configuration
        self.default_model_id = default_model_id
        self.max_retry_attempts = max_retry_attempts
        self.retry_delay_seconds = retry_delay_seconds

    async def analyze_document_from_url(
        self,
        request: DocumentAnalysisUrlRequest,
        correlation_id: Optional[str] = None
    ) -> Tuple[AzureDocIntelResponse, Optional[ErrorResponse]]:
        """
        Analyze a document from a URL using Azure Document Intelligence.
        
        Args:
            request (DocumentAnalysisUrlRequest): URL-based analysis request
            correlation_id (Optional[str]): Correlation ID for tracing
            
        Returns:
            Tuple[AzureDocIntelResponse, Optional[ErrorResponse]]: 
                Analysis results and error (if any)
        """
        start_time = time.time()
        
        self.logger.info(
            "Starting document analysis from URL",
            document_url=str(request.document_url),
            model_id=request.model_id,
            correlation_id=correlation_id
        )
        
        try:
            # Prepare analysis request
            analyze_request = AnalyzeDocumentRequest(url_source=str(request.document_url))
            
            # Execute analysis with retry logic
            for attempt in range(1, self.max_retry_attempts + 1):
                try:
                    self.logger.info(
                        f"Document analysis attempt {attempt}/{self.max_retry_attempts}",
                        correlation_id=correlation_id
                    )
                    
                    # Submit analysis to Azure Document Intelligence
                    poller = self.client.begin_analyze_document(
                        model_id=request.model_id,
                        analyze_request=analyze_request
                    )
                    
                    # Wait for analysis completion
                    azure_result = poller.result()
                    
                    # Convert to our response model
                    response = self._convert_azure_response(azure_result)
                    
                    processing_time = time.time() - start_time
                    self.logger.info(
                        "Document analysis completed successfully",
                        processing_time_ms=int(processing_time * 1000),
                        status=response.status,
                        model_id=request.model_id,
                        correlation_id=correlation_id
                    )
                    
                    return response, None
                    
                except HttpResponseError as e:
                    if e.status_code == 429:  # Rate limited
                        if attempt < self.max_retry_attempts:
                            delay = self.retry_delay_seconds * (2 ** (attempt - 1))
                            self.logger.warning(
                                f"Rate limited, retrying in {delay} seconds",
                                attempt=attempt,
                                correlation_id=correlation_id
                            )
                            await asyncio.sleep(delay)
                            continue
                    
                    # Non-retryable HTTP error
                    self.logger.error(
                        "Azure Document Intelligence HTTP error",
                        status_code=e.status_code,
                        error_message=str(e),
                        correlation_id=correlation_id
                    )
                    
                    error_response = ErrorResponse.create_azure_api_error(
                        azure_error={
                            "status_code": e.status_code,
                            "message": str(e),
                            "error_code": getattr(e, 'error_code', None)
                        },
                        correlation_id=correlation_id
                    )
                    return None, error_response
                    
                except ServiceRequestError as e:
                    if attempt < self.max_retry_attempts:
                        delay = self.retry_delay_seconds * (2 ** (attempt - 1))
                        self.logger.warning(
                            f"Service request error, retrying in {delay} seconds",
                            attempt=attempt,
                            error_message=str(e),
                            correlation_id=correlation_id
                        )
                        await asyncio.sleep(delay)
                        continue
                    
                    # Max retries exceeded
                    self.logger.error(
                        "Service request failed after maximum retries",
                        max_attempts=self.max_retry_attempts,
                        error_message=str(e),
                        correlation_id=correlation_id
                    )
                    
                    error_response = ErrorResponse(
                        error_code=ErrorCode.AZURE_API_ERROR,
                        message="Document Intelligence service temporarily unavailable",
                        details=str(e),
                        correlation_id=correlation_id,
                        suggested_action="Please retry the request after a few minutes",
                        retry_after_seconds=60
                    )
                    return None, error_response
            
            # This should not be reached, but handle edge case
            error_response = ErrorResponse(
                error_code=ErrorCode.ANALYSIS_FAILED,
                message="Document analysis failed after all retry attempts",
                correlation_id=correlation_id
            )
            return None, error_response
            
        except Exception as e:
            processing_time = time.time() - start_time
            self.logger.error(
                "Unexpected error during document analysis",
                processing_time_ms=int(processing_time * 1000),
                exception=e,
                correlation_id=correlation_id
            )
            
            error_response = ErrorResponse(
                error_code=ErrorCode.INTERNAL_ERROR,
                message="Unexpected error during document analysis",
                details=str(e),
                correlation_id=correlation_id
            )
            return None, error_response

    async def analyze_document_from_bytes(
        self,
        document_bytes: bytes,
        request: DocumentAnalysisFileRequest,
        filename: str,
        content_type: str,
        correlation_id: Optional[str] = None
    ) -> Tuple[AzureDocIntelResponse, Optional[ErrorResponse]]:
        """
        Analyze a document from byte data using Azure Document Intelligence.
        
        Args:
            document_bytes (bytes): Document file content as bytes
            request (DocumentAnalysisFileRequest): File-based analysis request  
            filename (str): Original filename for validation and logging
            content_type (str): MIME type of the document
            correlation_id (Optional[str]): Correlation ID for tracing
            
        Returns:
            Tuple[AzureDocIntelResponse, Optional[ErrorResponse]]:
                Analysis results and error (if any)
        """
        start_time = time.time()
        
        self.logger.info(
            "Starting document analysis from file upload",
            filename=filename,
            content_type=content_type,
            file_size_bytes=len(document_bytes),
            model_id=request.model_id,
            correlation_id=correlation_id
        )
        
        try:
            # Validate file upload constraints
            try:
                request.validate_file_upload(filename, content_type, len(document_bytes))
            except ValueError as e:
                self.logger.warning(
                    "File upload validation failed",
                    filename=filename,
                    content_type=content_type,
                    file_size_bytes=len(document_bytes),
                    validation_error=str(e),
                    correlation_id=correlation_id
                )
                
                error_response = ErrorResponse(
                    error_code=ErrorCode.INVALID_FILE_TYPE,
                    message="File upload validation failed",
                    details=str(e),
                    correlation_id=correlation_id,
                    suggested_action="Please ensure file meets size and format requirements"
                )
                return None, error_response
            
            # Execute analysis with retry logic
            for attempt in range(1, self.max_retry_attempts + 1):
                try:
                    self.logger.info(
                        f"Document analysis attempt {attempt}/{self.max_retry_attempts}",
                        filename=filename,
                        correlation_id=correlation_id
                    )
                    
                    # Submit analysis to Azure Document Intelligence
                    poller = self.client.begin_analyze_document(
                        model_id=request.model_id,
                        analyze_request=document_bytes,
                        content_type=content_type
                    )
                    
                    # Wait for analysis completion
                    azure_result = poller.result()
                    
                    # Convert to our response model
                    response = self._convert_azure_response(azure_result)
                    
                    processing_time = time.time() - start_time
                    self.logger.info(
                        "Document analysis completed successfully",
                        filename=filename,
                        processing_time_ms=int(processing_time * 1000),
                        status=response.status,
                        model_id=request.model_id,
                        correlation_id=correlation_id
                    )
                    
                    return response, None
                    
                except HttpResponseError as e:
                    if e.status_code == 429:  # Rate limited
                        if attempt < self.max_retry_attempts:
                            delay = self.retry_delay_seconds * (2 ** (attempt - 1))
                            self.logger.warning(
                                f"Rate limited, retrying in {delay} seconds",
                                attempt=attempt,
                                filename=filename,
                                correlation_id=correlation_id
                            )
                            await asyncio.sleep(delay)
                            continue
                    
                    # Non-retryable HTTP error
                    self.logger.error(
                        "Azure Document Intelligence HTTP error",
                        filename=filename,
                        status_code=e.status_code,
                        error_message=str(e),
                        correlation_id=correlation_id
                    )
                    
                    error_response = ErrorResponse.create_azure_api_error(
                        azure_error={
                            "status_code": e.status_code,
                            "message": str(e),
                            "filename": filename,
                            "error_code": getattr(e, 'error_code', None)
                        },
                        correlation_id=correlation_id
                    )
                    return None, error_response
                    
                except ServiceRequestError as e:
                    if attempt < self.max_retry_attempts:
                        delay = self.retry_delay_seconds * (2 ** (attempt - 1))
                        self.logger.warning(
                            f"Service request error, retrying in {delay} seconds",
                            attempt=attempt,
                            filename=filename,
                            error_message=str(e),
                            correlation_id=correlation_id
                        )
                        await asyncio.sleep(delay)
                        continue
                    
                    # Max retries exceeded
                    self.logger.error(
                        "Service request failed after maximum retries",
                        filename=filename,
                        max_attempts=self.max_retry_attempts,
                        error_message=str(e),
                        correlation_id=correlation_id
                    )
                    
                    error_response = ErrorResponse(
                        error_code=ErrorCode.AZURE_API_ERROR,
                        message="Document Intelligence service temporarily unavailable",
                        details=str(e),
                        correlation_id=correlation_id,
                        suggested_action="Please retry the request after a few minutes",
                        retry_after_seconds=60
                    )
                    return None, error_response
            
            # This should not be reached, but handle edge case
            error_response = ErrorResponse(
                error_code=ErrorCode.ANALYSIS_FAILED,
                message="Document analysis failed after all retry attempts",
                correlation_id=correlation_id
            )
            return None, error_response
            
        except Exception as e:
            processing_time = time.time() - start_time
            self.logger.error(
                "Unexpected error during document analysis",
                filename=filename,
                processing_time_ms=int(processing_time * 1000),
                exception=e,
                correlation_id=correlation_id
            )
            
            error_response = ErrorResponse(
                error_code=ErrorCode.INTERNAL_ERROR,
                message="Unexpected error during document analysis", 
                details=str(e),
                correlation_id=correlation_id
            )
            return None, error_response

    def _convert_azure_response(self, azure_result) -> AzureDocIntelResponse:
        """
        Convert Azure Document Intelligence response to our response model.
        
        Args:
            azure_result: Raw response from Azure Document Intelligence API
            
        Returns:
            AzureDocIntelResponse: Converted response model
        """
        try:
            # Convert the Azure response to a dictionary first
            # This handles the Azure SDK response object structure
            response_dict = {
                "status": "succeeded",
                "createdDateTime": datetime.utcnow(),
                "lastUpdatedDateTime": datetime.utcnow(),
                "analyzeResult": self._extract_analyze_result(azure_result)
            }
            
            # Parse into our Pydantic model
            return AzureDocIntelResponse.parse_obj(response_dict)
            
        except Exception as e:
            self.logger.error(
                "Error converting Azure response",
                exception=e
            )
            # Return minimal valid response on conversion error
            return AzureDocIntelResponse(
                status="failed",
                createdDateTime=datetime.utcnow(),
                lastUpdatedDateTime=datetime.utcnow(),
                analyzeResult=None,
                error={"message": f"Response conversion error: {str(e)}"}
            )

    def _extract_analyze_result(self, azure_result) -> Optional[Dict[str, Any]]:
        """
        Extract and format the analyze result from Azure response.
        
        Args:
            azure_result: Raw Azure Document Intelligence result
            
        Returns:
            Optional[Dict[str, Any]]: Formatted analyze result
        """
        try:
            if not hasattr(azure_result, 'documents') or not azure_result.documents:
                return None
            
            # Extract key information from the Azure response
            analyze_result = {
                "apiVersion": getattr(azure_result, 'api_version', "2024-11-30"),
                "modelId": getattr(azure_result, 'model_id', self.default_model_id),
                "stringIndexType": "utf16CodeUnit",
                "content": getattr(azure_result, 'content', ''),
                "documents": []
            }
            
            # Process each document in the results
            for doc in azure_result.documents:
                document_result = {
                    "docType": getattr(doc, 'doc_type', self.default_model_id),
                    "fields": {},
                    "confidence": getattr(doc, 'confidence', 0.0),
                    "boundingRegions": [],
                    "spans": []
                }
                
                # Extract fields (specifically looking for Serial field)
                if hasattr(doc, 'fields') and doc.fields:
                    for field_name, field_value in doc.fields.items():
                        if field_name == 'Serial':  # Focus on Serial field per requirements
                            document_result["fields"][field_name] = {
                                "type": getattr(field_value, 'value_type', 'string'),
                                "valueString": getattr(field_value, 'value', None),
                                "content": getattr(field_value, 'content', None),
                                "confidence": getattr(field_value, 'confidence', 0.0),
                                "boundingRegions": self._extract_bounding_regions(field_value),
                                "spans": self._extract_spans(field_value)
                            }
                
                analyze_result["documents"].append(document_result)
            
            return analyze_result
            
        except Exception as e:
            self.logger.error(
                "Error extracting analyze result",
                exception=e
            )
            return None

    def _extract_bounding_regions(self, field_value) -> List[Dict[str, Any]]:
        """
        Extract bounding regions from Azure field value.
        
        Args:
            field_value: Azure field value object
            
        Returns:
            List[Dict[str, Any]]: Formatted bounding regions
        """
        try:
            if not hasattr(field_value, 'bounding_regions'):
                return []
            
            regions = []
            for region in field_value.bounding_regions:
                regions.append({
                    "pageNumber": getattr(region, 'page_number', 1),
                    "polygon": list(getattr(region, 'polygon', []))
                })
            return regions
            
        except Exception:
            return []

    def _extract_spans(self, field_value) -> List[Dict[str, Any]]:
        """
        Extract content spans from Azure field value.
        
        Args:
            field_value: Azure field value object
            
        Returns:
            List[Dict[str, Any]]: Formatted content spans
        """
        try:
            if not hasattr(field_value, 'spans'):
                return []
            
            spans = []
            for span in field_value.spans:
                spans.append({
                    "offset": getattr(span, 'offset', 0),
                    "length": getattr(span, 'length', 0)
                })
            return spans
            
        except Exception:
            return []

    def health_check(self) -> Dict[str, Any]:
        """
        Perform health check on Document Intelligence service connectivity.
        
        Returns:
            Dict[str, Any]: Health check results
        """
        try:
            # Simple connectivity test - this could be enhanced with actual API call
            health_status = {
                "service": "document_intelligence",
                "status": "healthy" if self.endpoint and self.credential else "unhealthy",
                "endpoint": self.endpoint,
                "timestamp": datetime.utcnow().isoformat(),
                "configuration": {
                    "default_model_id": self.default_model_id,
                    "max_retry_attempts": self.max_retry_attempts,
                    "retry_delay_seconds": self.retry_delay_seconds
                }
            }
            
            self.logger.info("Document Intelligence health check completed", status=health_status["status"])
            return health_status
            
        except Exception as e:
            self.logger.error("Document Intelligence health check failed", exception=e)
            return {
                "service": "document_intelligence", 
                "status": "unhealthy",
                "error": str(e),
                "timestamp": datetime.utcnow().isoformat()
            }
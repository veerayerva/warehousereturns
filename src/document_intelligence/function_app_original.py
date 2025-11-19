"""
Document Intelligence Function App

Production-ready Azure Functions app for document analysis using Azure Document Intelligence.
Provides URL-based and file upload document processing with Serial field extraction,
confidence scoring, and automated blob storage for low-confidence documents.

Features:
- Azure Document Intelligence API integration with custom "serialnumber" model
- Dual input methods: URL and file upload
- Confidence threshold evaluation with configurable thresholds
- Automatic blob storage for low-confidence documents requiring review
- Comprehensive error handling and structured logging
- Health checks for all service dependencies
"""

import sys
import os
sys.path.append(os.path.join(os.path.dirname(__file__), '..'))

import azure.functions as func
import json
import asyncio
import uuid
from typing import Dict, Any, Optional
from datetime import datetime

# Import shared logging components
from shared.config.logging_config import get_logger, log_function_calls
from shared.middleware.logging_middleware import create_http_logging_wrapper

# Import Document Intelligence components
from services.document_processing_service import DocumentProcessingService
from models import (
    DocumentAnalysisUrlRequest,
    DocumentAnalysisFileRequest,
    DocumentAnalysisResponse,
    ErrorResponse,
    ErrorCode
)

# Initialize the Function App using Azure Functions v2 programming model
app = func.FunctionApp()

# Get logger for this function app
logger = get_logger('warehouse_returns.document_intelligence')

# Initialize processing service (will be created on first use)
_processing_service: Optional[DocumentProcessingService] = None


def get_processing_service() -> DocumentProcessingService:
    """
    Get or create the document processing service instance.
    
    Returns:
        DocumentProcessingService: Initialized service instance
    """
    global _processing_service
    
    if _processing_service is None:
        logger.info("Initializing Document Processing Service")
        _processing_service = DocumentProcessingService()
    
    return _processing_service


@app.function_name(name="ProcessDocument")
@app.route(route="documents/analyze", methods=["POST"], auth_level=func.AuthLevel.FUNCTION)
@create_http_logging_wrapper("ProcessDocument")
@log_function_calls("document_intelligence.process_document")
def process_document(req: func.HttpRequest) -> func.HttpResponse:
    """
    Process documents using Azure Document Intelligence for Serial field extraction.
    
    Supports two input methods:
    1. JSON body with document_url for URL-based processing
    2. Multipart form data with file upload
    
    Returns comprehensive analysis results with confidence scoring and
    automatic blob storage routing for low-confidence documents.
    
    Request Body (JSON):
    {
        "document_url": "https://example.com/document.pdf",
        "document_type": "warehouse_return",
        "model_id": "serialnumber",
        "confidence_threshold": 0.8
    }
    
    Request Body (Multipart):
    - document: File upload (PDF, JPG, PNG)
    - model_id: Optional, defaults to "serialnumber"
    - document_type: Optional, defaults to "warehouse_return"  
    - confidence_threshold: Optional, uses service default
    
    Returns:
        DocumentAnalysisResponse with Serial field extraction results
    """
    
    # Generate correlation ID for request tracing
    correlation_id = f"req-{uuid.uuid4()}"
    
    try:
        logger.info(
            "Document processing request received",
            correlation_id=correlation_id,
            content_type=req.headers.get('content-type', 'unknown'),
            content_length=req.headers.get('content-length', 0)
        )
        
        # Log business event
        logger.log_business_event(
            "document_processing_started",
            entity_type="document",
            correlation_id=correlation_id,
            properties={
                "content_type": req.headers.get('content-type', 'unknown'),
                "content_length": req.headers.get('content-length', 0)
            }
        )
        
        # Get processing service
        processing_service = get_processing_service()
        
        # Check request type: file upload vs URL
        files = req.files
        if files and len(files) > 0:
            # File upload processing
            response = asyncio.run(_process_file_upload(files, req, processing_service, correlation_id))
        else:
            # URL processing
            response = asyncio.run(_process_url_request(req, processing_service, correlation_id))
        
        # Convert response to JSON
        response_data = response.model_dump(exclude_none=True)
        
        # Log business event based on result
        if response.status.value == "succeeded":
            logger.log_business_event(
                "document_processing_succeeded",
                entity_id=response.analysis_id,
                entity_type="document_analysis",
                correlation_id=correlation_id,
                properties={
                    "serial_value": response.serial_field.value,
                    "confidence": response.serial_field.confidence,
                    "processing_time_ms": response.processing_metadata.get("processing_time_ms", 0),
                    "requires_review": response.status.value == "requires_review"
                }
            )
        else:
            logger.log_business_event(
                "document_processing_failed",
                entity_id=response.analysis_id,
                entity_type="document_analysis",
                correlation_id=correlation_id,
                properties={
                    "status": response.status.value,
                    "error_details": response.error_details
                }
            )
        
        # Determine HTTP status code based on analysis result
        if response.status.value == "succeeded":
            status_code = 200
        elif response.status.value == "requires_review":
            status_code = 202  # Accepted, but requires manual review
        else:
            status_code = 422  # Unprocessable Entity
        
        logger.info(
            "Document processing completed",
            analysis_id=response.analysis_id,
            status=response.status.value,
            correlation_id=correlation_id
        )
        
        return func.HttpResponse(
            json.dumps(response_data, indent=2, default=str),
            status_code=status_code,
            mimetype="application/json",
            headers={"X-Correlation-ID": correlation_id}
        )
        
    except Exception as e:
        # Log unexpected errors
        logger.error(
            "Unexpected error during document processing",
            exception=e,
            correlation_id=correlation_id,
            event_type="document_processing_error"
        )
        
        # Create error response
        error_response = {
            "error": {
                "code": "PROCESSING_ERROR",
                "message": "Unexpected error during document processing",
                "details": str(e),
                "correlation_id": correlation_id
            }
        }
        
        return func.HttpResponse(
            json.dumps(error_response, indent=2),
            status_code=500,
            mimetype="application/json",
            headers={"X-Correlation-ID": correlation_id}
        )


async def _process_file_upload(
    files: Dict[str, Any],
    req: func.HttpRequest,
    processing_service: DocumentProcessingService,
    correlation_id: str
) -> DocumentAnalysisResponse:
    """
    Process file upload request.
    
    Args:
        files (Dict[str, Any]): Request files
        req (func.HttpRequest): HTTP request
        processing_service (DocumentProcessingService): Processing service
        correlation_id (str): Correlation ID for tracing
        
    Returns:
        DocumentAnalysisResponse: Analysis results
    """
    # Get document file
    file = files.get('document')
    if not file:
        logger.warning("Document file missing in multipart request", correlation_id=correlation_id)
        raise ValueError("Document file is required in multipart request")
    
    # Read file content
    file_content = file.read()
    filename = getattr(file, 'filename', 'unknown_file')
    content_type = getattr(file, 'content_type', 'application/octet-stream')
    
    logger.info(
        "Processing uploaded file",
        filename=filename,
        file_size=len(file_content),
        content_type=content_type,
        correlation_id=correlation_id
    )
    
    # Parse form data for additional parameters
    form_data = {}
    try:
        # Try to get form fields (Azure Functions may not populate req.form for multipart)
        if hasattr(req, 'form') and req.form:
            form_data = dict(req.form)
    except Exception:
        # Form data parsing may fail in some cases
        pass
    
    # Create file analysis request
    try:
        file_request = DocumentAnalysisFileRequest(
            model_id=form_data.get('model_id', 'serialnumber'),
            document_type=form_data.get('document_type', 'warehouse_return'),
            confidence_threshold=float(form_data.get('confidence_threshold')) if form_data.get('confidence_threshold') else None
        )
    except (ValueError, TypeError) as e:
        logger.error("Invalid request parameters", exception=e, correlation_id=correlation_id)
        raise ValueError(f"Invalid request parameters: {e}")
    
    # Validate file request
    try:
        file_request.validate_file_upload(filename, len(file_content), content_type)
    except ValueError as e:
        logger.warning("File validation failed", validation_error=str(e), correlation_id=correlation_id)
        raise e
    
    # Process document
    return await processing_service.process_document_from_bytes(
        document_data=file_content,
        filename=filename,
        content_type=content_type,
        request=file_request,
        correlation_id=correlation_id
    )


async def _process_url_request(
    req: func.HttpRequest,
    processing_service: DocumentProcessingService,
    correlation_id: str
) -> DocumentAnalysisResponse:
    """
    Process URL-based document request.
    
    Args:
        req (func.HttpRequest): HTTP request
        processing_service (DocumentProcessingService): Processing service
        correlation_id (str): Correlation ID for tracing
        
    Returns:
        DocumentAnalysisResponse: Analysis results
    """
    # Parse JSON request body
    try:
        req_body = req.get_json()
    except ValueError as e:
        logger.error("Invalid JSON in request body", exception=e, correlation_id=correlation_id)
        raise ValueError("Invalid JSON format in request body")
    
    if not req_body:
        logger.warning("Empty request body", correlation_id=correlation_id)
        raise ValueError("Request body is required for URL processing")
    
    # Create URL analysis request with validation
    try:
        url_request = DocumentAnalysisUrlRequest(**req_body)
    except Exception as e:
        logger.warning("Invalid request data", validation_error=str(e), correlation_id=correlation_id)
        raise ValueError(f"Invalid request data: {e}")
    
    logger.info(
        "Processing document from URL",
        document_url=str(url_request.document_url),
        document_type=url_request.document_type,
        model_id=url_request.model_id,
        correlation_id=correlation_id
    )
    
    # Process document
    return await processing_service.process_document_from_url(
        request=url_request,
        correlation_id=correlation_id
    )


@app.function_name(name="GetAnalysisResult")
@app.route(route="documents/analysis/{analysis_id}", methods=["GET"], auth_level=func.AuthLevel.FUNCTION)
@create_http_logging_wrapper("GetAnalysisResult")
def get_analysis_result(req: func.HttpRequest) -> func.HttpResponse:
    """
    Retrieve document analysis results by analysis ID.
    
    This endpoint provides access to previously completed document analysis results.
    Since the processing is synchronous, this is primarily used for:
    - Re-retrieving results after initial processing
    - Checking analysis status and metadata
    - Accessing blob storage information for low-confidence documents
    
    Path Parameters:
        analysis_id (str): The unique analysis identifier returned from ProcessDocument
        
    Query Parameters:
        include_metadata (bool): Whether to include detailed processing metadata
        include_blob_info (bool): Whether to include blob storage information
        
    Returns:
        DocumentAnalysisResponse: Complete analysis results with field extraction
        
    Note: This is a mock implementation as analyses are processed synchronously.
    In a production system with async processing, this would retrieve results
    from a persistent store (database, cache, etc.).
    """
    
    correlation_id = f"get-{uuid.uuid4()}"
    
    try:
        analysis_id = req.route_params.get('analysis_id')
        
        logger.info(
            "Analysis result retrieval requested",
            analysis_id=analysis_id,
            correlation_id=correlation_id
        )
        
        if not analysis_id:
            logger.warning("Analysis ID missing from request", correlation_id=correlation_id)
            error_response = {
                "error": {
                    "code": "INVALID_REQUEST",
                    "message": "Analysis ID is required in the URL path",
                    "details": "Use format: /documents/analysis/{analysis_id}",
                    "correlation_id": correlation_id
                }
            }
            return func.HttpResponse(
                json.dumps(error_response, indent=2),
                status_code=400,
                mimetype="application/json",
                headers={"X-Correlation-ID": correlation_id}
            )
        
        # Parse query parameters
        include_metadata = req.params.get('include_metadata', 'true').lower() == 'true'
        include_blob_info = req.params.get('include_blob_info', 'true').lower() == 'true'
        
        logger.info(
            "Processing analysis result retrieval",
            analysis_id=analysis_id,
            include_metadata=include_metadata,
            include_blob_info=include_blob_info,
            correlation_id=correlation_id
        )
        
        # Since processing is synchronous, we don't have persistent storage yet
        # This is a mock implementation that would be replaced with database lookup
        # in a production system with asynchronous processing
        
        # Check if analysis_id follows expected format (analysis-<uuid>)
        if not analysis_id.startswith('analysis-'):
            logger.warning(
                "Invalid analysis ID format",
                analysis_id=analysis_id,
                correlation_id=correlation_id
            )
            error_response = {
                "error": {
                    "code": "ANALYSIS_NOT_FOUND",
                    "message": f"Analysis with ID '{analysis_id}' not found",
                    "details": "Analysis IDs should be in format 'analysis-<uuid>'",
                    "correlation_id": correlation_id
                }
            }
            return func.HttpResponse(
                json.dumps(error_response, indent=2),
                status_code=404,
                mimetype="application/json",
                headers={"X-Correlation-ID": correlation_id}
            )
        
        # Mock result - in production this would come from database/cache
        mock_result = {
            "analysis_id": analysis_id,
            "status": "succeeded",  # Could be: succeeded, failed, requires_review, processing
            "serial_field": {
                "field_name": "Serial",
                "value": "SN123456789",
                "confidence": 0.92,
                "status": "extracted",
                "extraction_metadata": {
                    "meets_threshold": True,
                    "extraction_success": True,
                    "raw_extracted_value": "SN123456789"
                }
            },
            "document_metadata": {
                "source_type": "url",  # or "file_upload"
                "document_type": "warehouse_return",
                "model_id": "serialnumber"
            },
            "created_at": "2024-01-15T10:30:00.000Z",
            "completed_at": "2024-01-15T10:30:15.500Z",
            "correlation_id": correlation_id
        }
        
        # Add processing metadata if requested
        if include_metadata:
            mock_result["processing_metadata"] = {
                "processing_time_ms": 15500,
                "azure_api_version": "2023-07-31",
                "confidence_threshold": 0.7,
                "model_used": "serialnumber"
            }
        
        # Add blob storage info if requested and applicable
        if include_blob_info and mock_result.get("status") == "requires_review":
            mock_result["blob_storage_info"] = {
                "container": "low-confidence",
                "blob_path": f"pending-review/{analysis_id}/document.pdf",
                "storage_account": "warehousereturns",
                "stored_at": "2024-01-15T10:30:16.000Z"
            }
        
        # Log successful retrieval
        logger.log_business_event(
            "analysis_result_retrieved",
            entity_id=analysis_id,
            entity_type="document_analysis",
            correlation_id=correlation_id,
            properties={
                "include_metadata": include_metadata,
                "include_blob_info": include_blob_info,
                "status": mock_result["status"]
            }
        )
        
        logger.info(
            "Analysis result retrieved successfully",
            analysis_id=analysis_id,
            status=mock_result["status"],
            correlation_id=correlation_id
        )
        
        return func.HttpResponse(
            json.dumps(mock_result, indent=2, default=str),
            status_code=200,
            mimetype="application/json",
            headers={"X-Correlation-ID": correlation_id}
        )
        
    except Exception as e:
        logger.error(
            "Error retrieving analysis result",
            exception=e,
            analysis_id=analysis_id if 'analysis_id' in locals() else None,
            correlation_id=correlation_id
        )
        
        error_response = {
            "error": {
                "code": "INTERNAL_ERROR",
                "message": "Unexpected error retrieving analysis result",
                "details": str(e),
                "correlation_id": correlation_id
            }
        }
        
        return func.HttpResponse(
            json.dumps(error_response, indent=2),
            status_code=500,
            mimetype="application/json",
            headers={"X-Correlation-ID": correlation_id}
        )


@app.function_name(name="DocumentHealthCheck")
@app.route(route="documents/health", methods=["GET"], auth_level=func.AuthLevel.ANONYMOUS)
def health_check(req: func.HttpRequest) -> func.HttpResponse:
    """
    Comprehensive health check endpoint for document intelligence service.
    
    Performs health checks on all service dependencies:
    - Document Processing Service (orchestration layer)
    - Azure Document Intelligence Service (API connectivity)
    - Blob Storage Repository (storage connectivity, if enabled)
    - Logging infrastructure
    
    Query Parameters:
        detailed (bool): Include detailed component information (default: false)
        timeout (int): Health check timeout in seconds (default: 30)
        
    Returns:
        Health status with component-level details and overall service health
    """
    
    correlation_id = f"health-{uuid.uuid4()}"
    
    try:
        logger.info("Health check requested", correlation_id=correlation_id)
        
        # Parse query parameters
        detailed = req.params.get('detailed', 'false').lower() == 'true'
        timeout_seconds = int(req.params.get('timeout', '30'))
        
        # Perform comprehensive health check
        try:
            # Get processing service and run health check
            processing_service = get_processing_service()
            health_results = asyncio.run(
                asyncio.wait_for(
                    processing_service.health_check(),
                    timeout=timeout_seconds
                )
            )
            
            # Add service-level information
            health_results.update({
                "version": "1.0.0",
                "service": "document-intelligence",
                "environment": os.getenv('AZURE_FUNCTIONS_ENVIRONMENT', 'development'),
                "function_app_name": os.getenv('WEBSITE_SITE_NAME', 'warehouse-returns-doc-intel'),
                "correlation_id": correlation_id
            })
            
            # Add configuration status
            config_status = {
                "azure_document_intelligence": {
                    "endpoint_configured": bool(os.getenv('DOCUMENT_INTELLIGENCE_ENDPOINT')),
                    "api_key_configured": bool(os.getenv('DOCUMENT_INTELLIGENCE_API_KEY'))
                },
                "blob_storage": {
                    "connection_string_configured": bool(os.getenv('AZURE_STORAGE_CONNECTION_STRING')),
                    "enabled": os.getenv('ENABLE_BLOB_STORAGE', 'true').lower() == 'true'
                },
                "confidence_threshold": float(os.getenv('CONFIDENCE_THRESHOLD', '0.7'))
            }
            
            if detailed:
                health_results["configuration"] = config_status
            
            # Determine HTTP status code based on health
            if health_results.get("status") == "healthy":
                status_code = 200
            elif health_results.get("status") == "degraded":
                status_code = 200  # Still serving requests
            else:
                status_code = 503  # Service unavailable
            
            logger.info(
                "Health check completed",
                status=health_results.get("status", "unknown"),
                correlation_id=correlation_id
            )
            
        except asyncio.TimeoutError:
            logger.warning(
                "Health check timed out",
                timeout_seconds=timeout_seconds,
                correlation_id=correlation_id
            )
            
            health_results = {
                "service": "document-intelligence",
                "status": "timeout",
                "timestamp": datetime.utcnow().isoformat(),
                "message": f"Health check timed out after {timeout_seconds} seconds",
                "correlation_id": correlation_id
            }
            status_code = 408  # Request timeout
            
        except Exception as e:
            logger.error(
                "Health check failed with error",
                exception=e,
                correlation_id=correlation_id
            )
            
            health_results = {
                "service": "document-intelligence",
                "status": "unhealthy",
                "timestamp": datetime.utcnow().isoformat(),
                "error": str(e),
                "correlation_id": correlation_id
            }
            status_code = 503  # Service unavailable
        
        return func.HttpResponse(
            json.dumps(health_results, indent=2, default=str),
            status_code=status_code,
            mimetype="application/json",
            headers={"X-Correlation-ID": correlation_id}
        )
        
    except Exception as e:
        # Fallback for critical errors
        logger.error(
            "Critical error in health check endpoint",
            exception=e,
            correlation_id=correlation_id if 'correlation_id' in locals() else 'unknown'
        )
        
        fallback_response = {
            "service": "document-intelligence",
            "status": "critical_error",
            "timestamp": datetime.utcnow().isoformat(),
            "error": "Critical error in health check endpoint",
            "details": str(e)
        }
        
        return func.HttpResponse(
            json.dumps(fallback_response, indent=2),
            status_code=500,
            mimetype="application/json"
        )
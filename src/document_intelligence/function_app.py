"""
Document Intelligence Function App - Production Ready

Azure Functions v2 application for document analysis using Azure Document Intelligence.
Provides URL-based and file upload document processing with Serial field extraction,
confidence scoring, and automated blob storage for low-confidence documents.

Key Features:
- Azure Document Intelligence API integration with custom "serialnumber" model
- Dual input methods: URL-based and file upload document processing
- Confidence threshold evaluation with configurable thresholds
- Automatic blob storage for low-confidence documents requiring review
- Comprehensive error handling and health checks

Author: Warehouse Returns Team
Version: 1.0.0
License: Internal Use
"""

# Standard library imports
import os
import sys
sys.path.append(os.path.abspath(os.path.join(os.path.dirname(__file__), '..')))

# Load environment variables from .env.local if it exists
try:
    from dotenv import load_dotenv
    load_dotenv('.env.local')
except ImportError:
    pass  # dotenv not installed, rely on local.settings.json

import json
import logging
import uuid
from datetime import datetime, timezone
from typing import Dict, Any, Optional
import asyncio

# Debug support for VS Code
try:
    import debugpy
    # Only start debugger if not already running
    if not debugpy.is_client_connected():
        debugpy.listen(5678)
        print("🐛 Debug server started on port 5678. VS Code can now attach!")
except ImportError:
    print("debugpy not available - install with: pip install debugpy")
except Exception as e:
    print(f"Debug setup error: {e}")

# Azure Functions imports
import azure.functions as func

JSON_MIMETYPE = "application/json"

# Configure logging with structured format for production
log_level = os.environ.get('LOG_LEVEL', 'INFO').upper()
logging.basicConfig(
    level=getattr(logging, log_level),
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    datefmt='%Y-%m-%d %H:%M:%S'
)
logger = logging.getLogger(__name__)

# Log application startup
logger.info(f"Document Intelligence API starting up - Environment: {os.environ.get('WAREHOUSE_RETURNS_ENV', 'unknown')}")

# Local service imports
from services.document_processing_service import DocumentProcessingService
from models import (
    DocumentAnalysisUrlRequest,
    DocumentAnalysisFileRequest,
    DocumentAnalysisResponse,
    ErrorResponse,
    ErrorCode
)
from models.DocumentAnalysisRequestModel import DocumentType

# Create the Function App
app = func.FunctionApp()

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
        logger.info("[SERVICE-INIT] Initializing Document Processing Service")
        try:
            _processing_service = DocumentProcessingService()
            logger.info(f"[SERVICE-INIT] Document Processing Service initialized - "
                       f"Blob-Enabled: {_processing_service.enable_blob_storage}, "
                       f"Blob-Repo-Available: {_processing_service.blob_repository is not None}")
        except Exception as e:
            logger.error(f"[SERVICE-INIT] Failed to initialize Document Processing Service - "
                        f"Error: {type(e).__name__}: {str(e)}")
            raise
    
    return _processing_service


def _generate_correlation_id() -> str:
    """Generate a unique correlation ID for request tracking."""
    return str(uuid.uuid4())


def _get_security_headers() -> Dict[str, str]:
    """Get standard security headers for responses."""
    return {
        'X-Content-Type-Options': 'nosniff',
        'X-Frame-Options': 'DENY',
        'X-XSS-Protection': '1; mode=block',
        'Strict-Transport-Security': 'max-age=31536000; includeSubDomains',
        'Content-Security-Policy': "default-src 'self'"
    }


def _create_error_response(message: str, status_code: int, correlation_id: str = None) -> func.HttpResponse:
    """Create a standardized error response."""
    error_response = ErrorResponse(
        error_code=ErrorCode.PROCESSING_ERROR,
        message=message,
        correlation_id=correlation_id,
        timestamp=datetime.now(timezone.utc)
    )
    
    return func.HttpResponse(
        error_response.json(),
        status_code=status_code,
        mimetype=JSON_MIMETYPE,
        headers=_get_security_headers()
    )


@app.function_name(name="ProcessDocument")
@app.route(route="process-document", methods=["POST"], auth_level=func.AuthLevel.ANONYMOUS)
def process_document(req: func.HttpRequest) -> func.HttpResponse:
    """
    Process a document for Serial number extraction.
    
    Accepts either:
    - JSON with document_url for URL-based analysis
    - Form data with file upload for direct file analysis
    
    Returns analysis results with confidence scoring and
    blob storage information if confidence is below threshold.
    """
    # Generate correlation ID for request tracing
    correlation_id = _generate_correlation_id()
    
    # Log incoming HTTP request details
    logger.info(
        f"[HTTP-REQUEST] Endpoint: /api/process-document, Method: {req.method}, "
        f"Content-Type: {req.headers.get('content-type', 'not-specified')}, "
        f"Content-Length: {req.headers.get('content-length', 'not-specified')}, "
        f"User-Agent: {req.headers.get('user-agent', 'not-specified')[:100]}..., "
        f"Correlation-ID: {correlation_id}"
    )
    
    logger.info(f"ProcessDocument endpoint called - Correlation ID: {correlation_id}")
    
    try:
        processing_service = get_processing_service()
        content_type = req.headers.get('content-type', '').lower()
        result = None
        if content_type.startswith('application/json'):
            result = _handle_json_request(req, processing_service, correlation_id)
        elif content_type.startswith('multipart/form-data'):
            result = _handle_file_upload(req, processing_service, correlation_id)
        else:
            return _create_error_response(
                "Unsupported content type. Use application/json or multipart/form-data",
                400,
                correlation_id
            )
        if isinstance(result, func.HttpResponse):
            return result
        
        # Convert DocumentAnalysisResponse to JSON response
        response_data = {
            "analysis_id": result.analysis_id,
            "status": result.status,
            "serial_field": result.serial_field.dict() if result.serial_field else None,
            "document_metadata": result.document_metadata,
            "processing_metadata": result.processing_metadata,
            "blob_storage_info": result.blob_storage_info,
            "created_at": result.created_at.isoformat() if result.created_at else None,
            "completed_at": result.completed_at.isoformat() if result.completed_at else None,
            "correlation_id": result.correlation_id,
            "error_details": result.error_details
        }
        
        # Log successful HTTP response details
        serial_value = result.serial_field.value if result.serial_field else None
        serial_confidence = result.serial_field.confidence if result.serial_field else 0.0
        serial_status = result.serial_field.status if result.serial_field else "none"
        
        logger.info(
            f"[HTTP-RESPONSE-SUCCESS] Status: 200, Analysis-ID: {result.analysis_id}, "
            f"Serial-Value: {serial_value}, Serial-Confidence: {serial_confidence:.3f}, "
            f"Serial-Status: {serial_status}, Response-Size: {len(json.dumps(response_data))} chars, "
            f"Correlation-ID: {correlation_id}"
        )
        
        logger.info(f"Document processing completed successfully - Correlation ID: {correlation_id}")
        return func.HttpResponse(
            json.dumps(response_data, indent=2),
            status_code=200,
            mimetype=JSON_MIMETYPE,
            headers=_get_security_headers()
        )
    except Exception as e:
        # Log error HTTP response details
        logger.error(
            f"[HTTP-RESPONSE-ERROR] Status: 500, Error-Type: {type(e).__name__}, "
            f"Error-Message: {str(e)[:200]}..., Correlation-ID: {correlation_id}",
            exc_info=True
        )
        
        logger.error(f"Unexpected error in ProcessDocument - Correlation ID: {correlation_id}", exc_info=True)
        return _create_error_response("An unexpected error occurred while processing the document", 500, correlation_id)

def _handle_json_request(req, processing_service, correlation_id):
    try:
        req_body = req.get_json()
        if not req_body:
            return _create_error_response("Request body is required", 400, correlation_id)
        
        # Log the parsed JSON request data
        logger.info(
            f"[JSON-REQUEST] Type: url_analysis, Body: {json.dumps(req_body)[:300]}..., "
            f"Correlation-ID: {correlation_id}"
        )
        
        url_request = DocumentAnalysisUrlRequest(**req_body)
        logger.info(f"Processing document from URL - Correlation ID: {correlation_id}")
        loop = asyncio.new_event_loop()
        asyncio.set_event_loop(loop)
        try:
            result = loop.run_until_complete(
                processing_service.process_document_from_url(url_request, correlation_id)
            )
        finally:
            loop.close()
        return result
    except ValueError:
        logger.error(f"Invalid JSON in request - Correlation ID: {correlation_id}", exc_info=True)
        return _create_error_response("Invalid JSON format in request body", 400, correlation_id)
    except Exception as e:
        logger.error(f"Error processing URL request - Correlation ID: {correlation_id}", exc_info=True)
        return _create_error_response(f"Error processing document from URL: {str(e)}", 500, correlation_id)

def _handle_file_upload(req, processing_service, correlation_id):
    try:
        files = req.files
        if not files or 'file' not in files:
            return _create_error_response("File is required in multipart form data", 400, correlation_id)
        uploaded_file = files['file']
        if not uploaded_file.filename:
            return _create_error_response("File name is required", 400, correlation_id)
        
        # Get file size before processing
        file_size = 0
        if uploaded_file.stream:
            current_pos = uploaded_file.stream.tell()
            uploaded_file.stream.seek(0, 2)  # Seek to end
            file_size = uploaded_file.stream.tell()
            uploaded_file.stream.seek(current_pos)  # Return to original position
        
        # Log the file upload request data
        logger.info(
            f"[FILE-UPLOAD] Type: file_analysis, Filename: {uploaded_file.filename}, "
            f"Content-Type: {uploaded_file.content_type or 'unknown'}, "
            f"File-Size: {file_size} bytes, Correlation-ID: {correlation_id}"
        )
        
        # Reset file stream position after reading for size
        uploaded_file.stream.seek(0)
        
        logger.info(f"Processing uploaded file: {uploaded_file.filename} - Correlation ID: {correlation_id}")
        file_content = uploaded_file.read()
        
        # Create DocumentAnalysisFileRequest from form data or use defaults
        content_type = uploaded_file.content_type or 'application/octet-stream'
        file_request = DocumentAnalysisFileRequest(
            document_type=DocumentType.SERIAL_NUMBER,
            model_id="serialnumber",
            confidence_threshold=float(os.getenv('CONFIDENCE_THRESHOLD', '0.7')),
            correlation_id=correlation_id
        )
        
        loop = asyncio.new_event_loop()
        asyncio.set_event_loop(loop)
        try:
            result = loop.run_until_complete(
                processing_service.process_document_from_bytes(
                    file_content,
                    uploaded_file.filename,
                    content_type,
                    file_request,
                    correlation_id
                )
            )
        finally:
            loop.close()
        return result
    except Exception as e:
        logger.error(f"Error processing file upload - Correlation ID: {correlation_id}", exc_info=True)
        return _create_error_response(f"Error processing uploaded file: {str(e)}", 500, correlation_id)
        
@app.function_name(name="DocumentHealthCheck")
@app.route(route="health", methods=["GET"], auth_level=func.AuthLevel.ANONYMOUS)
def document_health_check(req: func.HttpRequest) -> func.HttpResponse:
    """
    Health check endpoint for Document Intelligence API.
    
    Validates connectivity to:
    - Azure Document Intelligence service
    - Azure Blob Storage
    
    Returns service status and dependency health information.
    """
    # Generate correlation ID for request tracing
    correlation_id = _generate_correlation_id()
    
    logger.info(f"DocumentHealthCheck endpoint called - Correlation ID: {correlation_id}")
    
    try:
        processing_service = get_processing_service()
        
        # Perform health checks
        loop = asyncio.new_event_loop()
        asyncio.set_event_loop(loop)
        try:
            health_status = loop.run_until_complete(
                processing_service.health_check()
            )
        finally:
            loop.close()
        
        # Determine overall health status
        service_status = health_status.get("status", "unhealthy")
        overall_status = "healthy" if service_status == "healthy" else "unhealthy"
        status_code = 200 if overall_status == "healthy" else 503
        
        response_data = {
            "status": overall_status,
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "correlation_id": correlation_id,
            "services": health_status.get("components", {}),
            "version": "1.0.0"
        }
        
        logger.info(f"Health check completed - Status: {overall_status} - Correlation ID: {correlation_id}")
        
        return func.HttpResponse(
            json.dumps(response_data, indent=2),
            status_code=status_code,
            mimetype=JSON_MIMETYPE,
            headers=_get_security_headers()
        )
        
    except Exception as e:
        logger.error(f"Error during health check - Correlation ID: {correlation_id}", exc_info=True)
        
        error_response = {
            "status": "unhealthy",
            "timestamp": datetime.now(timezone.utc).isoformat(),
            "correlation_id": correlation_id,
            "error": f"Health check failed: {str(e)}",
            "version": "1.0.0"
        }
        
        return func.HttpResponse(
            json.dumps(error_response, indent=2),
            status_code=503,
            mimetype=JSON_MIMETYPE,
            headers=_get_security_headers()
        )


@app.function_name(name="GetSwaggerDoc")
@app.route(route="swagger", methods=["GET"], auth_level=func.AuthLevel.ANONYMOUS)
def get_swagger_doc(req: func.HttpRequest) -> func.HttpResponse:
    """
    Swagger/OpenAPI documentation endpoint for Document Intelligence API.
    """
    swagger_doc = {
        "openapi": "3.0.0",
        "info": {
            "title": "Document Intelligence API",
            "version": "1.0.0",
            "description": "Azure Document Intelligence API for document analysis and Serial field extraction"
        },
        "paths": {
            "/api/process-document": {
                "post": {
                    "summary": "Process document for Serial number extraction",
                    "description": "Accepts URL or file upload for document analysis",
                    "requestBody": {
                        "content": {
                            "application/json": {
                                "schema": {
                                    "type": "object",
                                    "properties": {
                                        "document_url": {"type": "string", "format": "uri"}
                                    },
                                    "required": ["document_url"]
                                }
                            },
                            "multipart/form-data": {
                                "schema": {
                                    "type": "object",
                                    "properties": {
                                        "file": {"type": "string", "format": "binary"}
                                    }
                                }
                            }
                        }
                    },
                    "responses": {
                        "200": {"description": "Document processed successfully"},
                        "400": {"description": "Invalid request"},
                        "500": {"description": "Processing error"}
                    }
                }
            },

            "/health": {
                "get": {
                    "summary": "Health check endpoint",
                    "responses": {
                        "200": {"description": "Service is healthy"},
                        "503": {"description": "Service is unhealthy"}
                    }
                }
            }
        }
    }
    
    return func.HttpResponse(
        json.dumps(swagger_doc, indent=2),
        status_code=200,
        mimetype=JSON_MIMETYPE,
        headers=_get_security_headers()
    )


@app.function_name(name="SwaggerUI")
@app.route(route="docs", methods=["GET"], auth_level=func.AuthLevel.ANONYMOUS)
def swagger_ui(req: func.HttpRequest) -> func.HttpResponse:
    """
    Swagger UI documentation page.
    """
    swagger_ui_html = '''<!DOCTYPE html>
<html>
<head>
    <title>Document Intelligence API Documentation</title>
    <link rel="stylesheet" type="text/css" href="https://unpkg.com/swagger-ui-dist@5.9.0/swagger-ui.css" />
    <style>
        html { box-sizing: border-box; overflow: -moz-scrollbars-vertical; overflow-y: scroll; }
        *, *:before, *:after { box-sizing: inherit; }
        body { margin:0; background: #fafafa; }
    </style>
</head>
<body>
    <div id="swagger-ui"></div>
    <script src="https://unpkg.com/swagger-ui-dist@5.9.0/swagger-ui-bundle.js"></script>
    <script src="https://unpkg.com/swagger-ui-dist@5.9.0/swagger-ui-standalone-preset.js"></script>
    <script>
        window.onload = function() {
            const ui = SwaggerUIBundle({
                url: '/api/swagger',
                dom_id: '#swagger-ui',
                deepLinking: true,
                presets: [
                    SwaggerUIBundle.presets.apis,
                    SwaggerUIStandalonePreset
                ],
                plugins: [
                    SwaggerUIBundle.plugins.DownloadUrl
                ],
                layout: "StandaloneLayout",
                tryItOutEnabled: true
            });
        };
    </script>
</body>
</html>'''
    
    # Custom headers for Swagger UI to allow external resources
    swagger_headers = {
        'X-Content-Type-Options': 'nosniff',
        'X-Frame-Options': 'DENY',
        'X-XSS-Protection': '1; mode=block',
        'Strict-Transport-Security': 'max-age=31536000; includeSubDomains',
        'Content-Security-Policy': "default-src 'self'; script-src 'self' 'unsafe-inline' https://unpkg.com; style-src 'self' 'unsafe-inline' https://unpkg.com; font-src 'self' https://unpkg.com"
    }
    
    return func.HttpResponse(
        swagger_ui_html,
        status_code=200,
        mimetype="text/html",
        headers=swagger_headers
    )
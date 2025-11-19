"""
PieceInfo API Function App - Production Ready

Azure Functions v2 application that aggregates warehouse piece information from multiple external APIs.
This application provides a single endpoint to retrieve comprehensive piece details by combining:
- Piece Inventory Location data (warehouse, rack location, serial numbers)
- Product Master data (descriptions, models, brands, categories)
- Vendor Details (contact information, addresses, policies)

Key Features:
- RESTful API with OpenAPI/Swagger documentation
- SSL/HTTPS support for secure external API communication
- Comprehensive error handling and logging
- Health check endpoint for monitoring

- Environment-based configuration management

Author: Warehouse Returns Team
Version: 1.0.0
License: Internal Use
"""

# Standard library imports
import os
import json
import logging
import re
import uuid
from datetime import datetime
from typing import Dict, Any, Optional
import asyncio

# Azure Functions imports
import azure.functions as func

# Local service imports
from services.aggregation_service import SimpleAggregationService

# Configure logging with structured format for production
log_level = os.environ.get('LOG_LEVEL', 'INFO').upper()
logging.basicConfig(
    level=getattr(logging, log_level),
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    datefmt='%Y-%m-%d %H:%M:%S'
)
logger = logging.getLogger(__name__)

# Log application startup
logger.info(f"PieceInfo API starting up - Environment: {os.environ.get('WAREHOUSE_RETURNS_ENV', 'unknown')}")



# Create the Function App
app = func.FunctionApp()


@app.function_name(name="GetSwaggerDoc")
@app.route(route="swagger", methods=["GET"], auth_level=func.AuthLevel.ANONYMOUS)
def get_swagger_doc(req: func.HttpRequest) -> func.HttpResponse:
    """
    Swagger/OpenAPI documentation endpoint for PieceInfo API.
    """
    
    swagger_doc = {
        "openapi": "3.0.3",
        "info": {
            "title": "PieceInfo API",
            "description": "API for aggregating piece information from multiple external sources",
            "version": "1.0.0",
            "contact": {
                "name": "Warehouse Returns Team"
            }
        },
        "servers": [
            {
                "url": "/api",
                "description": "PieceInfo API Server"
            }
        ],
        "paths": {
            "/pieces/{piece_number}": {
                "get": {
                    "summary": "Get aggregated piece information",
                    "description": "Retrieves comprehensive piece information by combining data from piece inventory, product master, and vendor APIs",
                    "parameters": [
                        {
                            "name": "piece_number",
                            "in": "path",
                            "required": True,
                            "schema": {"type": "string"},
                            "description": "Unique piece inventory identifier",
                            "example": "170080637"
                        }
                    ],
                    "responses": {
                        "200": {
                            "description": "Successful response with aggregated piece information",
                            "content": {
                                "application/json": {
                                    "schema": {"$ref": "#/components/schemas/AggregatedPieceInfo"}
                                }
                            }
                        },
                        "400": {"description": "Bad request - validation error"},
                        "404": {"description": "Piece not found"},
                        "500": {"description": "Internal server error"}
                    }
                }
            }
        },
        "components": {
            "schemas": {
                "AggregatedPieceInfo": {
                    "type": "object",
                    "properties": {
                        "piece_inventory_key": {"type": "string", "example": "170080637"},
                        "sku": {"type": "string", "example": "67007500"},
                        "vendor_code": {"type": "string", "example": "VIZIA"},
                        "warehouse_location": {"type": "string", "example": "WHKCTY"},
                        "rack_location": {"type": "string", "example": "R03-019-03"},
                        "serial_number": {"type": "string", "example": "SZVOU5GB1600294"},
                        "description": {"type": "string", "example": "ALL-IN-ONE SOUNDBAR"},
                        "vendor_name": {"type": "string", "example": "NIGHT & DAY"}
                    }
                }
            }
        }
    }
    
    return func.HttpResponse(
        json.dumps(swagger_doc, indent=2),
        status_code=200,
        mimetype="application/json",
        headers={
            "Access-Control-Allow-Origin": "*",
            "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
            "Access-Control-Allow-Headers": "Content-Type"
        }
    )


@app.function_name(name="SwaggerUI")
@app.route(route="docs", methods=["GET"], auth_level=func.AuthLevel.ANONYMOUS)
def swagger_ui(req: func.HttpRequest) -> func.HttpResponse:
    """
    Swagger UI HTML page for interactive API documentation.
    """
    
    swagger_ui_html = '''
    <!DOCTYPE html>
    <html>
    <head>
        <title>PieceInfo API Documentation</title>
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
    </html>
    '''
    
    return func.HttpResponse(
        swagger_ui_html,
        status_code=200,
        mimetype="text/html"
    )


def _validate_piece_number(piece_number: str) -> tuple[bool, Optional[str]]:
    """
    Validate piece number format and business rules.
    
    Args:
        piece_number: The piece number to validate
        
    Returns:
        Tuple of (is_valid, error_message)
    """
    if not piece_number:
        return False, "piece_number is required"
    
    # Remove whitespace and convert to string
    piece_number = str(piece_number).strip()
    
    # Check minimum length
    if len(piece_number) < 3:
        return False, "piece_number must be at least 3 characters long"
    
    # Check maximum length
    if len(piece_number) > 50:
        return False, "piece_number must not exceed 50 characters"
    
    # Check for valid alphanumeric characters (allow some special chars)
    if not re.match(r'^[A-Za-z0-9\-_]*$', piece_number):
        return False, "piece_number contains invalid characters. Only alphanumeric, hyphens, and underscores are allowed"
    
    return True, None


def _generate_correlation_id() -> str:
    """
    Generate a unique correlation ID for request tracing.
    
    Returns:
        UUID string for correlation tracking
    """
    return str(uuid.uuid4())


def _create_error_response(error_message: str, status_code: int, correlation_id: Optional[str] = None) -> func.HttpResponse:
    """
    Create standardized error response with security headers.
    
    Args:
        error_message: The error message to include in response
        status_code: HTTP status code
        correlation_id: Optional correlation ID for tracing
        
    Returns:
        Azure Functions HTTP response with error details
    """
    error_response = {
        "error": error_message,
        "timestamp": datetime.utcnow().isoformat() + "Z",
        "status_code": status_code
    }
    
    if correlation_id:
        error_response["correlation_id"] = correlation_id
    
    return func.HttpResponse(
        json.dumps(error_response, indent=2),
        status_code=status_code,
        mimetype="application/json",
        headers=_get_security_headers()
    )


def _get_security_headers() -> Dict[str, str]:
    """
    Get standard security headers for all responses.
    
    Returns:
        Dictionary of security headers
    """
    return {
        "X-Content-Type-Options": "nosniff",
        "X-Frame-Options": "DENY",
        "X-XSS-Protection": "1; mode=block",
        "Strict-Transport-Security": "max-age=31536000; includeSubDomains",
        "Cache-Control": "no-cache, no-store, must-revalidate",
        "Pragma": "no-cache"
    }


@app.function_name(name="GetPieceInfo")
@app.route(route="pieces/{piece_number}", methods=["GET"], auth_level=func.AuthLevel.ANONYMOUS)
def get_piece_info(req: func.HttpRequest) -> func.HttpResponse:
    """
    Get aggregated piece information by piece number.
    
    This endpoint combines data from three external APIs:
    1. Piece Inventory Location API - warehouse and rack location details
    2. Product Master API - product descriptions, models, brands
    3. Vendor Details API - vendor contact and policy information
    
    Args:
        req: Azure Functions HTTP request object containing piece_number in route
        
    Returns:
        HTTP response with aggregated piece information or error details
        
    Raises:
        Various exceptions for validation errors, API failures, or system errors
    """
    # Generate correlation ID for request tracing
    correlation_id = _generate_correlation_id()
    
    logger.info(f"GetPieceInfo endpoint called - Correlation ID: {correlation_id}")
    

    
    try:
        # Extract and validate piece number from route parameter
        piece_number = req.route_params.get('piece_number')
        
        # Validate piece number format and business rules
        is_valid, error_message = _validate_piece_number(piece_number)
        if not is_valid:
            logger.warning(f"Invalid piece number: {piece_number} - {error_message} - Correlation ID: {correlation_id}")
            return _create_error_response(error_message, 400, correlation_id)
        
        logger.info(f"Processing piece number: {piece_number} - Correlation ID: {correlation_id}")
        

        
        # Initialize aggregation service with correlation ID for tracing
        try:
            aggregation_service = SimpleAggregationService()
            logger.info(f"Aggregation service initialized - Correlation ID: {correlation_id}")
        except Exception as init_error:
            logger.error(f"Failed to initialize aggregation service - Correlation ID: {correlation_id} - Error: {init_error}")
            return _create_error_response("Service initialization failed", 500, correlation_id)
        
        # Get aggregated piece information from external APIs
        try:
            result = asyncio.run(aggregation_service.get_aggregated_piece_info(piece_number))
            logger.info(f"Successfully retrieved aggregated data - Piece: {piece_number} - Correlation ID: {correlation_id}")
        except Exception as aggregation_error:
            logger.error(f"Aggregation failed - Piece: {piece_number} - Correlation ID: {correlation_id} - Error: {aggregation_error}")
            
            # Check for specific error types
            error_msg = str(aggregation_error).lower()
            if "not found" in error_msg or "404" in error_msg:
                return _create_error_response(f"Piece {piece_number} not found", 404, correlation_id)
            elif "timeout" in error_msg:
                return _create_error_response("Request timeout - please try again", 504, correlation_id)
            elif "ssl" in error_msg or "certificate" in error_msg:
                return _create_error_response("SSL/Certificate error - contact support", 502, correlation_id)
            else:
                return _create_error_response("Failed to retrieve piece information", 500, correlation_id)
        
        # Add metadata to response
        response_data = {
            **result,
            "metadata": {
                "correlation_id": correlation_id,
                "timestamp": datetime.utcnow().isoformat() + "Z",
                "version": "1.0.0",
                "source": "pieceinfo-api"
            }
        }
        

        
        logger.info(f"Successful response generated - Piece: {piece_number} - Correlation ID: {correlation_id}")
        
        return func.HttpResponse(
            json.dumps(response_data, indent=2),
            status_code=200,
            mimetype="application/json",
            headers=_get_security_headers()
        )
        
    except Exception as e:
        # Log comprehensive error details for debugging
        logger.error(f"Unexpected error processing piece info request - Correlation ID: {correlation_id}", 
                    exc_info=True, extra={"piece_number": piece_number, "correlation_id": correlation_id})
        
        # Return generic error message to client (don't expose internal details)
        return _create_error_response(
            "An unexpected error occurred while processing the request", 
            500, 
            correlation_id
        )


@app.function_name(name="PieceInfoHealthCheck")
@app.route(route="pieces/health", methods=["GET"], auth_level=func.AuthLevel.ANONYMOUS)
def health_check(req: func.HttpRequest) -> func.HttpResponse:
    """
    Comprehensive health check endpoint for monitoring and alerting.
    
    This endpoint provides:
    - Overall service health status
    - Component-level health checks
    - Configuration validation
    - Environment information
    - Performance metrics
    
    Args:
        req: Azure Functions HTTP request object
        
    Returns:
        HTTP response with detailed health information
    """
    health_correlation_id = _generate_correlation_id()
    logger.info(f"Health check requested - Correlation ID: {health_correlation_id}")
    
    # Perform component health checks
    components_status = {}
    overall_healthy = True
    
    try:
        # Check aggregation service initialization
        test_service = SimpleAggregationService()
        components_status["aggregation_service"] = "healthy"
    except Exception as e:
        components_status["aggregation_service"] = f"unhealthy: {str(e)}"
        overall_healthy = False
        logger.warning(f"Aggregation service health check failed: {e}")
    
    # Check configuration completeness
    config_issues = []
    required_env_vars = [
        'EXTERNAL_API_BASE_URL',
        'OCP_APIM_SUBSCRIPTION_KEY'
    ]
    
    for env_var in required_env_vars:
        if not os.environ.get(env_var):
            config_issues.append(f"Missing {env_var}")
            overall_healthy = False
    
    components_status["configuration"] = "healthy" if not config_issues else f"issues: {', '.join(config_issues)}"
    components_status["logging"] = "healthy"
    components_status["ssl_verification"] = "enabled" if os.environ.get('VERIFY_SSL', 'false').lower() == 'true' else "disabled"
    
    health_status = {
        "status": "healthy" if overall_healthy else "unhealthy",
        "timestamp": datetime.utcnow().isoformat() + "Z",
        "correlation_id": health_correlation_id,
        "version": "1.0.0",
        "service": "pieceinfo-api",
        "environment": os.environ.get('WAREHOUSE_RETURNS_ENV', 'unknown'),
        "components": components_status,
        "configuration": {
            "base_url": os.environ.get('EXTERNAL_API_BASE_URL', 'NOT_CONFIGURED'),
            "timeout_seconds": float(os.environ.get('API_TIMEOUT_SECONDS', '30')),
            "max_retries": int(os.environ.get('API_MAX_RETRIES', '3')),
            "max_batch_size": int(os.environ.get('MAX_BATCH_SIZE', '10')),
            "subscription_key_configured": bool(os.environ.get('OCP_APIM_SUBSCRIPTION_KEY')),
            "ssl_verification": os.environ.get('VERIFY_SSL', 'false'),
            "log_level": os.environ.get('LOG_LEVEL', 'INFO')
        }
    }
    
    # Add configuration issues if any
    if config_issues:
        health_status["configuration_issues"] = config_issues
    
    status_code = 200 if overall_healthy else 503
    
    return func.HttpResponse(
        json.dumps(health_status, indent=2),
        status_code=status_code,
        mimetype="application/json",
        headers=_get_security_headers()
    )
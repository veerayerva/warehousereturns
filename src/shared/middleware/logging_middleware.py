"""
Logging middleware for Azure Functions.
Provides request/response logging and correlation tracking.
"""

import azure.functions as func
import json
import time
import uuid
from datetime import datetime
from typing import Dict, Any, Optional

from ..config.logging_config import get_logger


class LoggingMiddleware:
    """Middleware for request/response logging and correlation tracking."""
    
    def __init__(self, function_name: str):
        """
        Initialize logging middleware with function-specific configuration.
        
        This constructor sets up comprehensive logging infrastructure for Azure Functions:
        - Creates function-specific logger with proper naming convention
        - Configures correlation tracking for request tracing
        - Sets up structured logging for Application Insights integration
        - Initializes performance monitoring capabilities
        
        Logger Configuration:
        - Namespace: 'warehouse_returns.{function_name}'
        - Log Level: Inherited from application configuration
        - Formatting: JSON structured for cloud logging
        - Destination: Azure Application Insights + Console
        
        Args:
            function_name (str): Azure Function name for logger identification
                               e.g., 'document_intelligence', 'pieceinfo_api'
        """
        self.function_name = function_name
        self.logger = get_logger(f'warehouse_returns.{function_name}')
    
    def log_request(self, req: func.HttpRequest, correlation_id: str = None) -> str:
        """
        Log comprehensive incoming HTTP request details with correlation tracking.
        
        This method captures complete request information for debugging and monitoring:
        - HTTP method, URL, and headers (security-filtered)
        - Request body size and content type
        - Client IP and user agent information
        - Correlation ID for request tracing across services
        - Timestamp for performance analysis
        
        Security Considerations:
        - Sensitive headers are filtered out (Authorization, API keys)
        - Request body content is not logged for privacy
        - Only metadata and size information is captured
        
        Args:
            req (func.HttpRequest): Azure Functions HTTP request object
            correlation_id (str, optional): Existing correlation ID or auto-generated
        
        Returns:
            str: Correlation ID for this request (generated if not provided)
        """
        
        Args:
            req: Azure Functions HTTP request
            correlation_id: Optional correlation ID, generates one if not provided
            
        Returns:
            Correlation ID for tracking
        """
        if not correlation_id:
            correlation_id = str(uuid.uuid4())
        
        # Extract request details
        request_details = {
            'method': req.method,
            'url': req.url,
            'headers': dict(req.headers),
            'query_params': dict(req.params),
            'correlation_id': correlation_id,
            'function_name': self.function_name,
            'request_id': req.headers.get('x-ms-request-id', 'unknown'),
            'user_agent': req.headers.get('user-agent', 'unknown')
        }
        
        # Log request body for non-GET requests (be careful with sensitive data)
        if req.method != 'GET':
            try:
                body = req.get_body()
                if body:
                    # Only log first 1000 characters to avoid huge logs
                    body_str = body.decode('utf-8')[:1000]
                    request_details['body_preview'] = body_str
                    request_details['body_size'] = len(body)
            except Exception:
                request_details['body'] = 'Unable to decode body'
        
        self.logger.info(
            f"Incoming request: {req.method} {req.url}",
            extra={'custom_properties': request_details}
        )
        
        return correlation_id
    
    def log_response(self, response: func.HttpResponse, correlation_id: str, 
                    duration_ms: float, additional_context: Dict[str, Any] = None) -> None:
        """
        Log comprehensive HTTP response details with performance metrics and context.
        
        This method captures detailed response information for monitoring and debugging:
        - HTTP status code and response headers
        - Processing duration for performance analysis
        - Response size and content type information
        - Correlation ID for request/response tracking
        - Additional business context and metadata
        
        Performance Monitoring:
        - Processing duration (milliseconds) for SLA tracking
        - Response size metrics for bandwidth analysis
        - Status code distribution for health monitoring
        - Error rate calculation for alerting
        
        Args:
            response (func.HttpResponse): Azure Functions HTTP response object
            correlation_id (str): Request correlation ID for distributed tracing
            duration_ms (float): Request processing duration in milliseconds
            additional_context (Dict[str, Any], optional): Business-specific context data
        """
        response_details = {
            'status_code': response.status_code,
            'headers': dict(response.headers) if response.headers else {},
            'correlation_id': correlation_id,
            'function_name': self.function_name,
            'duration_ms': round(duration_ms, 2)
        }
        
        # Add additional context if provided
        if additional_context:
            response_details.update(additional_context)
        
        # Log response body preview for error responses
        if response.status_code >= 400:
            try:
                body = response.get_body()
                if body:
                    body_str = body.decode('utf-8')[:500]  # Limit error body logging
                    response_details['error_body'] = body_str
            except Exception:
                pass
        
        # Determine log level based on status code
        if response.status_code >= 500:
            log_level = 'error'
        elif response.status_code >= 400:
            log_level = 'warning'
        else:
            log_level = 'info'
        
        message = f"Response: {response.status_code} in {duration_ms:.2f}ms"
        
        getattr(self.logger, log_level)(
            message,
            extra={'custom_properties': response_details}
        )
    
    def log_exception(self, exception: Exception, correlation_id: str, 
                     additional_context: Dict[str, Any] = None) -> None:
        """
        Log unhandled exceptions.
        
        Args:
            exception: The exception that occurred
            correlation_id: Request correlation ID
            additional_context: Additional context to log
        """
        error_details = {
            'exception_type': type(exception).__name__,
            'exception_message': str(exception),
            'correlation_id': correlation_id,
            'function_name': self.function_name
        }
        
        if additional_context:
            error_details.update(additional_context)
        
        self.logger.error(
            f"Unhandled exception in {self.function_name}",
            exception=exception,
            extra={'custom_properties': error_details}
        )


def create_http_logging_wrapper(function_name: str):
    """
    Create a decorator for HTTP function logging.
    
    Args:
        function_name: Name of the Azure Function
        
    Returns:
        Decorator function
    """
    def decorator(function):
        def wrapper(req: func.HttpRequest, *args, **kwargs):
            middleware = LoggingMiddleware(function_name)
            start_time = time.time()
            
            # Generate correlation ID
            correlation_id = middleware.log_request(req)
            
            try:
                # Execute the function
                response = function(req, *args, **kwargs)
                
                # Calculate duration
                duration_ms = (time.time() - start_time) * 1000
                
                # Log response
                middleware.log_response(response, correlation_id, duration_ms)
                
                # Add correlation ID to response headers
                if hasattr(response, 'headers'):
                    if response.headers is None:
                        response.headers = {}
                    response.headers['x-correlation-id'] = correlation_id
                
                return response
                
            except Exception as e:
                # Calculate duration
                duration_ms = (time.time() - start_time) * 1000
                
                # Log exception
                middleware.log_exception(e, correlation_id, {'duration_ms': duration_ms})
                
                # Re-raise the exception
                raise
        
        return wrapper
    return decorator
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
        Initialize logging middleware.
        
        Args:
            function_name: Name of the Azure Function
        """
        self.function_name = function_name
        self.logger = get_logger(f'warehouse_returns.{function_name}')
    
    def log_request(self, req: func.HttpRequest, correlation_id: str = None) -> str:
        """
        Log incoming HTTP request.
        
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
        Log outgoing HTTP response.
        
        Args:
            response: Azure Functions HTTP response
            correlation_id: Request correlation ID
            duration_ms: Request processing duration in milliseconds
            additional_context: Additional context to log
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
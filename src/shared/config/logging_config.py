"""
Centralized logging configuration for warehouse returns application.
Provides structured logging with Azure Application Insights integration.
"""

import logging
import logging.config
import os
import json
import sys
from datetime import datetime
from typing import Dict, Any, Optional
from functools import wraps
import traceback

try:
    from opencensus.ext.azure.log_exporter import AzureLogHandler
    from opencensus.ext.azure.trace_exporter import AzureExporter
    from opencensus.trace.samplers import ProbabilitySampler
    from opencensus.trace.tracer import Tracer
    AZURE_LOGGING_AVAILABLE = True
except ImportError:
    AZURE_LOGGING_AVAILABLE = False


class StructuredFormatter(logging.Formatter):
    """Custom formatter that creates structured JSON logs."""
    
    def format(self, record: logging.LogRecord) -> str:
        """Format log record as structured JSON."""
        # Create base log entry
        log_entry = {
            'timestamp': datetime.utcnow().isoformat() + 'Z',
            'level': record.levelname,
            'logger': record.name,
            'message': record.getMessage(),
            'module': record.module,
            'function': record.funcName,
            'line': record.lineno,
            'thread': record.thread,
            'process': record.process
        }
        
        # Add exception information if present
        if record.exc_info:
            log_entry['exception'] = {
                'type': record.exc_info[0].__name__,
                'message': str(record.exc_info[1]),
                'traceback': traceback.format_exception(*record.exc_info)
            }
        
        # Add custom properties if present
        if hasattr(record, 'custom_properties'):
            log_entry['custom_properties'] = record.custom_properties
        
        # Add correlation ID if present
        if hasattr(record, 'correlation_id'):
            log_entry['correlation_id'] = record.correlation_id
        
        # Add user context if present
        if hasattr(record, 'user_context'):
            log_entry['user_context'] = record.user_context
        
        # Add operation context if present
        if hasattr(record, 'operation_context'):
            log_entry['operation_context'] = record.operation_context
        
        return json.dumps(log_entry, default=str)


class WarehouseReturnsLogger:
    """Centralized logger for warehouse returns application."""
    
    def __init__(self, name: str = None):
        """
        Initialize logger with configuration.
        
        Args:
            name: Logger name, defaults to calling module
        """
        self.name = name or __name__
        self.logger = logging.getLogger(self.name)
        self._setup_logger()
        self._tracer = None
        if AZURE_LOGGING_AVAILABLE:
            self._setup_tracer()
    
    def _setup_logger(self) -> None:
        """Setup logger with appropriate handlers and formatters."""
        # Prevent duplicate setup
        if self.logger.handlers:
            return
        
        # Get configuration
        log_level = os.getenv('LOG_LEVEL', 'INFO').upper()
        environment = os.getenv('ENVIRONMENT', 'development')
        
        # Set logger level
        self.logger.setLevel(getattr(logging, log_level, logging.INFO))
        
        # Console handler with structured formatting
        console_handler = logging.StreamHandler(sys.stdout)
        console_handler.setLevel(logging.DEBUG)
        
        if environment == 'development':
            # Human-readable format for development
            console_formatter = logging.Formatter(
                '%(asctime)s - %(name)s - %(levelname)s - %(message)s'
            )
        else:
            # Structured JSON format for production
            console_formatter = StructuredFormatter()
        
        console_handler.setFormatter(console_formatter)
        self.logger.addHandler(console_handler)
        
        # Azure Application Insights handler
        if AZURE_LOGGING_AVAILABLE:
            connection_string = os.getenv('APPLICATION_INSIGHTS_CONNECTION_STRING')
            if connection_string:
                try:
                    azure_handler = AzureLogHandler(connection_string=connection_string)
                    azure_handler.setLevel(logging.INFO)
                    
                    # Use structured formatter for Application Insights
                    azure_formatter = StructuredFormatter()
                    azure_handler.setFormatter(azure_formatter)
                    
                    self.logger.addHandler(azure_handler)
                    self.logger.info("Azure Application Insights logging enabled")
                except Exception as e:
                    self.logger.warning(f"Failed to setup Azure logging: {str(e)}")
        
        # Prevent propagation to avoid duplicate logs
        self.logger.propagate = False
    
    def _setup_tracer(self) -> None:
        """Setup distributed tracing."""
        connection_string = os.getenv('APPLICATION_INSIGHTS_CONNECTION_STRING')
        if connection_string:
            try:
                exporter = AzureExporter(connection_string=connection_string)
                sampler = ProbabilitySampler(rate=1.0)  # Sample 100% in development
                self._tracer = Tracer(exporter=exporter, sampler=sampler)
            except Exception as e:
                self.logger.warning(f"Failed to setup tracing: {str(e)}")
    
    def _add_context(self, extra: Dict[str, Any] = None, **kwargs) -> Dict[str, Any]:
        """Add context information to log entry."""
        context = {}
        
        # Add extra properties
        if extra:
            context.update(extra)
        
        # Add keyword arguments
        context.update(kwargs)
        
        return {'custom_properties': context} if context else {}
    
    def debug(self, message: str, extra: Dict[str, Any] = None, **kwargs) -> None:
        """Log debug message."""
        self.logger.debug(message, extra=self._add_context(extra, **kwargs))
    
    def info(self, message: str, extra: Dict[str, Any] = None, **kwargs) -> None:
        """Log info message."""
        self.logger.info(message, extra=self._add_context(extra, **kwargs))
    
    def warning(self, message: str, extra: Dict[str, Any] = None, **kwargs) -> None:
        """Log warning message."""
        self.logger.warning(message, extra=self._add_context(extra, **kwargs))
    
    def error(self, message: str, exception: Exception = None, extra: Dict[str, Any] = None, **kwargs) -> None:
        """Log error message."""
        if exception:
            self.logger.error(message, exc_info=True, extra=self._add_context(extra, **kwargs))
        else:
            self.logger.error(message, extra=self._add_context(extra, **kwargs))
    
    def critical(self, message: str, exception: Exception = None, extra: Dict[str, Any] = None, **kwargs) -> None:
        """Log critical message."""
        if exception:
            self.logger.critical(message, exc_info=True, extra=self._add_context(extra, **kwargs))
        else:
            self.logger.critical(message, extra=self._add_context(extra, **kwargs))
    
    def log_function_entry(self, function_name: str, parameters: Dict[str, Any] = None) -> None:
        """Log function entry with parameters."""
        self.info(
            f"Entering function: {function_name}",
            function_name=function_name,
            parameters=parameters or {},
            event_type="function_entry"
        )
    
    def log_function_exit(self, function_name: str, result: Any = None, duration_ms: float = None) -> None:
        """Log function exit with result and duration."""
        self.info(
            f"Exiting function: {function_name}",
            function_name=function_name,
            result_type=type(result).__name__ if result is not None else None,
            duration_ms=duration_ms,
            event_type="function_exit"
        )
    
    def log_http_request(self, method: str, url: str, status_code: int = None, 
                        duration_ms: float = None, user_id: str = None) -> None:
        """Log HTTP request details."""
        self.info(
            f"HTTP {method} {url}",
            http_method=method,
            url=url,
            status_code=status_code,
            duration_ms=duration_ms,
            user_id=user_id,
            event_type="http_request"
        )
    
    def log_business_event(self, event_name: str, entity_id: str = None, 
                          entity_type: str = None, properties: Dict[str, Any] = None) -> None:
        """Log business events."""
        self.info(
            f"Business event: {event_name}",
            event_name=event_name,
            entity_id=entity_id,
            entity_type=entity_type,
            event_type="business_event",
            **(properties or {})
        )
    
    def start_span(self, name: str):
        """Start a distributed tracing span."""
        if self._tracer:
            return self._tracer.span(name=name)
        return None


# Global logger instances for each component
def get_logger(name: str = None) -> WarehouseReturnsLogger:
    """
    Get logger instance for a component.
    
    Args:
        name: Logger name, defaults to calling module
    
    Returns:
        Configured logger instance
    """
    return WarehouseReturnsLogger(name)


# Decorator for automatic function logging
def log_function_calls(logger_name: str = None):
    """
    Decorator to automatically log function entry and exit.
    
    Args:
        logger_name: Custom logger name
    """
    def decorator(func):
        @wraps(func)
        def wrapper(*args, **kwargs):
            logger = get_logger(logger_name or func.__module__)
            function_name = f"{func.__module__}.{func.__name__}"
            
            # Log function entry
            logger.log_function_entry(
                function_name,
                {
                    'args_count': len(args),
                    'kwargs_keys': list(kwargs.keys()) if kwargs else []
                }
            )
            
            start_time = datetime.utcnow()
            
            try:
                # Execute function
                result = func(*args, **kwargs)
                
                # Calculate duration
                duration = (datetime.utcnow() - start_time).total_seconds() * 1000
                
                # Log successful exit
                logger.log_function_exit(function_name, result, duration)
                
                return result
                
            except Exception as e:
                # Calculate duration
                duration = (datetime.utcnow() - start_time).total_seconds() * 1000
                
                # Log error
                logger.error(
                    f"Function {function_name} failed after {duration:.2f}ms",
                    exception=e,
                    function_name=function_name,
                    duration_ms=duration,
                    event_type="function_error"
                )
                raise
        
        return wrapper
    return decorator


# Pre-configured loggers for common components
document_logger = get_logger('warehouse_returns.document_intelligence')
return_logger = get_logger('warehouse_returns.return_processing')
shared_logger = get_logger('warehouse_returns.shared')
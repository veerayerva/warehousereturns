"""
Test suite for the logging framework and function apps.
Demonstrates how to test functions that use the comprehensive logging system.
"""

import unittest
import json
import sys
import os
from unittest.mock import patch, Mock, MagicMock
from io import StringIO

# Add src to path for imports
sys.path.append(os.path.join(os.path.dirname(__file__), '../src'))

# Import logging components
from shared.config.logging_config import WarehouseReturnsLogger, StructuredFormatter


class TestLoggingFramework(unittest.TestCase):
    """Test the custom logging framework."""
    
    def setUp(self):
        """Set up test fixtures."""
        self.logger = WarehouseReturnsLogger('test_logger', log_level='DEBUG')
        
    def test_structured_formatter(self):
        """Test the structured JSON formatter."""
        formatter = StructuredFormatter()
        
        # Create a mock log record
        record = Mock()
        record.levelname = 'INFO'
        record.getMessage.return_value = 'Test message'
        record.name = 'test_logger'
        record.created = 1642248000.0  # Fixed timestamp
        record.pathname = '/app/test.py'
        record.lineno = 42
        record.funcName = 'test_function'
        record.exc_info = None
        
        # Format the record
        formatted = formatter.format(record)
        parsed = json.loads(formatted)
        
        # Verify structure
        self.assertEqual(parsed['level'], 'INFO')
        self.assertEqual(parsed['message'], 'Test message')
        self.assertEqual(parsed['logger'], 'test_logger')
        self.assertIn('timestamp', parsed)
        
    def test_business_event_logging(self):
        """Test business event logging."""
        with patch('logging.Logger.info') as mock_info:
            self.logger.log_business_event(
                'test_event',
                entity_id='TEST-123',
                entity_type='test_entity',
                properties={'key': 'value'}
            )
            
            # Verify the log call
            mock_info.assert_called_once()
            call_args = mock_info.call_args
            
            # Check that business event fields are in extra
            extra = call_args.kwargs
            self.assertEqual(extra['event_type'], 'test_event')
            self.assertEqual(extra['entity_id'], 'TEST-123')
            self.assertEqual(extra['entity_type'], 'test_entity')
            self.assertEqual(extra['business_properties'], {'key': 'value'})
            
    def test_error_logging_with_exception(self):
        """Test error logging with exception details."""
        with patch('logging.Logger.error') as mock_error:
            try:
                raise ValueError("Test exception")
            except Exception as e:
                self.logger.error("Test error message", exception=e)
                
            # Verify the error was logged
            mock_error.assert_called_once()
            call_args = mock_error.call_args
            
            # Check exception is in extra
            extra = call_args.kwargs
            self.assertIn('exception_type', extra)
            self.assertEqual(extra['exception_type'], 'ValueError')
            self.assertIn('exception_message', extra)
            
    def test_correlation_tracking(self):
        """Test correlation ID functionality."""
        correlation_id = 'test-correlation-123'
        
        with patch('logging.Logger.info') as mock_info:
            self.logger.set_correlation_id(correlation_id)
            self.logger.info("Test message")
            
            # Verify correlation ID is included
            call_args = mock_info.call_args
            extra = call_args.kwargs
            self.assertEqual(extra['correlation_id'], correlation_id)


class TestFunctionAppLogging(unittest.TestCase):
    """Test logging integration in function apps."""
    
    def setUp(self):
        """Set up test fixtures."""
        # Mock Azure Functions modules
        self.func_mock = Mock()
        sys.modules['azure.functions'] = self.func_mock
        
        # Mock the logging components
        self.logger_patcher = patch('shared.config.logging_config.get_logger')
        self.mock_get_logger = self.logger_patcher.start()
        self.mock_logger = Mock()
        self.mock_get_logger.return_value = self.mock_logger
        
    def tearDown(self):
        """Clean up test fixtures."""
        self.logger_patcher.stop()
        
    def test_document_processing_logging(self):
        """Test logging in document processing function."""
        # Import after mocking
        from document_intelligence.function_app import process_document
        
        # Create mock request with file upload
        mock_req = Mock()
        mock_req.files = {'document': Mock()}
        mock_req.files['document'].filename = 'test_document.pdf'
        mock_req.files['document'].read.return_value = b'mock file content'
        mock_req.files['document'].content_type = 'application/pdf'
        mock_req.files['document'].seek = Mock()
        mock_req.headers = {'content-type': 'multipart/form-data', 'content-length': '1024'}
        
        # Mock HTTP response
        mock_response = Mock()
        self.func_mock.HttpResponse.return_value = mock_response
        
        # Call the function
        result = process_document(mock_req)
        
        # Verify logging calls were made
        self.mock_logger.info.assert_called()
        self.mock_logger.log_business_event.assert_called()
        
        # Verify business events were logged
        business_event_calls = [call for call in self.mock_logger.log_business_event.call_args_list]
        self.assertGreater(len(business_event_calls), 0)
        
    def test_return_creation_logging(self):
        """Test logging in return creation function."""
        from return_processing.function_app import create_return
        
        # Create mock request
        mock_req = Mock()
        mock_req.get_json.return_value = {
            'order_id': 'ORDER-12345',
            'customer_id': 'CUST-67890',
            'return_reason': 'Damaged item',
            'items': [
                {
                    'product_id': 'PROD-001',
                    'quantity': 1,
                    'condition': 'damaged'
                }
            ]
        }
        
        # Mock datetime
        self.func_mock.datetime.utcnow.return_value.isoformat.return_value = '2024-01-15T10:30:00Z'
        
        # Mock HTTP response
        mock_response = Mock()
        self.func_mock.HttpResponse.return_value = mock_response
        
        # Call the function
        result = create_return(mock_req)
        
        # Verify logging calls
        self.mock_logger.info.assert_called()
        self.mock_logger.log_business_event.assert_called()
        
        # Check that return creation events were logged
        event_calls = self.mock_logger.log_business_event.call_args_list
        event_types = [call[0][0] for call in event_calls]  # First positional arg is event_type
        
        self.assertIn('return_request_created', event_types)
        self.assertIn('return_request_processed', event_types)
        
    def test_error_handling_and_logging(self):
        """Test error handling and logging."""
        from document_intelligence.function_app import process_document
        
        # Create mock request that will cause an error
        mock_req = Mock()
        mock_req.files = {}
        mock_req.get_json.side_effect = ValueError("Invalid JSON")
        
        # Mock HTTP response
        mock_response = Mock()
        self.func_mock.HttpResponse.return_value = mock_response
        
        # Call the function
        result = process_document(mock_req)
        
        # Verify error logging
        self.mock_logger.error.assert_called()
        
        # Check error details
        error_calls = self.mock_logger.error.call_args_list
        self.assertGreater(len(error_calls), 0)


class TestLoggingMiddleware(unittest.TestCase):
    """Test the HTTP logging middleware."""
    
    def setUp(self):
        """Set up test fixtures."""
        self.logger_patcher = patch('shared.middleware.logging_middleware.get_logger')
        self.mock_get_logger = self.logger_patcher.start()
        self.mock_logger = Mock()
        self.mock_get_logger.return_value = self.mock_logger
        
    def tearDown(self):
        """Clean up test fixtures."""
        self.logger_patcher.stop()
        
    def test_http_request_logging(self):
        """Test HTTP request/response logging middleware."""
        from shared.middleware.logging_middleware import create_http_logging_wrapper
        
        # Create a mock function to wrap
        @create_http_logging_wrapper("TestFunction")
        def mock_function(req):
            return Mock(status_code=200)
        
        # Create mock request
        mock_req = Mock()
        mock_req.method = 'POST'
        mock_req.url = 'https://test.azurewebsites.net/api/test'
        mock_req.headers = {'content-type': 'application/json'}
        
        # Call the wrapped function
        result = mock_function(mock_req)
        
        # Verify request logging
        info_calls = [call for call in self.mock_logger.info.call_args_list 
                     if 'HTTP request received' in str(call)]
        self.assertGreater(len(info_calls), 0)
        
        # Verify response logging
        info_calls = [call for call in self.mock_logger.info.call_args_list 
                     if 'HTTP request completed' in str(call)]
        self.assertGreater(len(info_calls), 0)


if __name__ == '__main__':
    # Set up test environment
    os.environ['AZURE_CLIENT_ID'] = 'test-client-id'
    os.environ['AZURE_CLIENT_SECRET'] = 'test-client-secret'
    os.environ['AZURE_TENANT_ID'] = 'test-tenant-id'
    os.environ['APPLICATIONINSIGHTS_CONNECTION_STRING'] = 'test-connection-string'
    
    # Run tests
    unittest.main(verbosity=2)
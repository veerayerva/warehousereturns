"""
Integration tests for PieceInfo API Function App.
Tests the complete HTTP endpoints with mocked external APIs.
"""

import unittest
import json
import sys
import os
from unittest.mock import patch, Mock, AsyncMock
import asyncio

# Add src to path for imports
sys.path.append(os.path.join(os.path.dirname(__file__), '../../src'))

# Mock Azure Functions before importing
func_mock = Mock()
func_mock.HttpRequest = Mock
func_mock.HttpResponse = Mock
func_mock.AuthLevel = Mock()
func_mock.AuthLevel.FUNCTION = Mock()
func_mock.AuthLevel.ANONYMOUS = Mock()
func_mock.datetime = Mock()
func_mock.datetime.utcnow.return_value.isoformat.return_value = '2024-01-15T10:30:00Z'

sys.modules['azure.functions'] = func_mock

# Now import the function app
from pieceinfo_api.function_app import get_piece_info, get_piece_info_batch, health_check


class TestPieceInfoFunctionApp(unittest.TestCase):
    """Test the PieceInfo API Function App endpoints."""
    
    def setUp(self):
        """Set up test fixtures."""
        # Sample external API responses
        self.piece_inventory_response = {
            "pieceInventoryKey": "170080637",
            "warehouseLocation": "WHKCTY", 
            "serialNumber": "SZVOU5GB1600294",
            "sku": "67007500",
            "vendor": "VIZIA",
            "family": "ELECTR",
            "purchaseReferenceNumber": "6610299377*2",
            "rackLocation": "R03-019-03"
        }
        
        self.product_master_response = {
            "sku": "67007500",
            "description": "ALL-IN-ONE SOUNDBAR",
            "modelNo": "SV210D-0806", 
            "vendor": "VIZIA",
            "brand": "VIZBC",
            "family": "ELECTR",
            "category": "EHMAUD",
            "group": "HMSBAR"
        }
        
        self.vendor_response = {
            "code": "VIZIA",
            "serialNumberRequired": "false",
            "name": "NIGHT & DAY",
            "addressLine1": "3901 N KINGSHIGHWAY BLVD",
            "addressLine2": "",
            "city": "SAINT LOUIS", 
            "state": "MO",
            "zipCode": "63115",
            "vendorReturn": "false",
            "repName": "John Nicholson",
            "primaryRepEmail": "jpnick@kc.rr.com",
            "secondaryRepEmail": "gmail.com",
            "execEmail": None
        }
        
        # Mock HTTP response
        self.mock_http_response = Mock()
        func_mock.HttpResponse.return_value = self.mock_http_response
        
    @patch('pieceinfo_api.function_app.aggregation_service')
    def test_get_piece_info_success(self, mock_aggregation_service):
        """Test successful piece info retrieval."""
        # Mock request
        mock_req = Mock()
        mock_req.route_params = {'piece_number': '170080637'}
        mock_req.params = {}
        
        # Mock aggregation service response
        mock_result = Mock()
        mock_result.json.return_value = json.dumps({
            "piece_inventory_key": "170080637",
            "sku": "67007500",
            "vendor_code": "VIZIA",
            "description": "ALL-IN-ONE SOUNDBAR"
        })
        mock_result.sku = "67007500"
        mock_result.vendor_code = "VIZIA"
        mock_result.vendor_name = "NIGHT & DAY"
        
        # Mock async function
        async def mock_get_aggregated():
            return mock_result
            
        mock_aggregation_service.get_aggregated_piece_info.return_value = mock_get_aggregated()
        
        # Call the function
        result = get_piece_info(mock_req)
        
        # Verify response was created with correct parameters
        func_mock.HttpResponse.assert_called_once()
        call_args = func_mock.HttpResponse.call_args
        
        # Check status code and content type
        self.assertEqual(call_args.kwargs.get('status_code'), 200)
        self.assertEqual(call_args.kwargs.get('mimetype'), 'application/json')
        
    def test_get_piece_info_missing_piece_number(self):
        """Test error handling when piece number is missing."""
        # Mock request without piece number
        mock_req = Mock()
        mock_req.route_params = {}
        mock_req.params = {}
        
        # Call the function
        result = get_piece_info(mock_req)
        
        # Verify error response
        func_mock.HttpResponse.assert_called_once()
        call_args = func_mock.HttpResponse.call_args
        
        # Check error status code
        self.assertEqual(call_args.kwargs.get('status_code'), 400)
        
        # Check error message in response
        response_data = call_args[0][0]  # First positional argument
        self.assertIn('required', response_data)
        
    @patch('pieceinfo_api.function_app.aggregation_service')
    def test_get_piece_info_not_found(self, mock_aggregation_service):
        """Test handling when piece is not found."""
        from pieceinfo_api.exceptions.api_exceptions import ExternalAPIException
        
        # Mock request
        mock_req = Mock()
        mock_req.route_params = {'piece_number': '999999999'}
        mock_req.params = {}
        
        # Mock aggregation service to raise exception
        async def mock_get_aggregated():
            raise ExternalAPIException("piece-inventory-location", status_code=404, response_text="Not found")
            
        mock_aggregation_service.get_aggregated_piece_info.return_value = mock_get_aggregated()
        
        # Call the function
        result = get_piece_info(mock_req)
        
        # Verify 404 response
        func_mock.HttpResponse.assert_called_once()
        call_args = func_mock.HttpResponse.call_args
        self.assertEqual(call_args.kwargs.get('status_code'), 404)
        
    def test_get_piece_info_batch_success(self):
        """Test successful batch piece info retrieval."""
        # Mock request
        mock_req = Mock()
        mock_req.get_json.return_value = {
            'piece_numbers': ['170080637', '170080638'],
            'correlation_id': 'test-batch-123'
        }
        
        # Mock aggregation service
        with patch('pieceinfo_api.function_app.aggregation_service') as mock_service:
            mock_result = Mock()
            mock_result.dict.return_value = {
                "piece_inventory_key": "170080637",
                "sku": "67007500"
            }
            
            async def mock_get_aggregated(piece_number, correlation_id):
                return mock_result
                
            mock_service.get_aggregated_piece_info.return_value = mock_get_aggregated("170080637", "test-batch-123")
            
            # Call the function
            result = get_piece_info_batch(mock_req)
            
            # Verify response
            func_mock.HttpResponse.assert_called_once()
            call_args = func_mock.HttpResponse.call_args
            self.assertEqual(call_args.kwargs.get('status_code'), 200)
            
    def test_get_piece_info_batch_invalid_json(self):
        """Test batch endpoint with invalid JSON."""
        # Mock request with invalid JSON
        mock_req = Mock()
        mock_req.get_json.side_effect = ValueError("Invalid JSON")
        
        # Call the function
        result = get_piece_info_batch(mock_req)
        
        # Verify error response
        func_mock.HttpResponse.assert_called_once()
        call_args = func_mock.HttpResponse.call_args
        self.assertEqual(call_args.kwargs.get('status_code'), 400)
        
    def test_get_piece_info_batch_empty_array(self):
        """Test batch endpoint with empty piece_numbers array."""
        # Mock request
        mock_req = Mock()
        mock_req.get_json.return_value = {
            'piece_numbers': []
        }
        
        # Call the function
        result = get_piece_info_batch(mock_req)
        
        # Verify error response
        func_mock.HttpResponse.assert_called_once()
        call_args = func_mock.HttpResponse.call_args
        self.assertEqual(call_args.kwargs.get('status_code'), 400)
        
    @patch.dict(os.environ, {'MAX_BATCH_SIZE': '2'})
    def test_get_piece_info_batch_size_limit(self):
        """Test batch endpoint with size limit exceeded."""
        # Mock request with too many items
        mock_req = Mock()
        mock_req.get_json.return_value = {
            'piece_numbers': ['1', '2', '3', '4', '5']  # Exceeds limit of 2
        }
        
        # Call the function
        result = get_piece_info_batch(mock_req)
        
        # Verify error response
        func_mock.HttpResponse.assert_called_once()
        call_args = func_mock.HttpResponse.call_args
        self.assertEqual(call_args.kwargs.get('status_code'), 400)
        
        # Check error message mentions batch size
        response_data = call_args[0][0]
        self.assertIn('Batch size', response_data)
        
    def test_health_check(self):
        """Test health check endpoint."""
        # Mock request
        mock_req = Mock()
        
        # Call the function
        result = health_check(mock_req)
        
        # Verify successful response
        func_mock.HttpResponse.assert_called_once()
        call_args = func_mock.HttpResponse.call_args
        
        self.assertEqual(call_args.kwargs.get('status_code'), 200)
        self.assertEqual(call_args.kwargs.get('mimetype'), 'application/json')
        
        # Verify health check content
        response_data = call_args[0][0]
        health_data = json.loads(response_data)
        
        self.assertEqual(health_data['status'], 'healthy')
        self.assertEqual(health_data['service'], 'pieceinfo-api')
        self.assertIn('components', health_data)
        self.assertIn('configuration', health_data)
        
    def test_correlation_id_handling(self):
        """Test that correlation ID is properly handled."""
        # Mock request with correlation ID
        mock_req = Mock()
        mock_req.route_params = {'piece_number': '170080637'}
        mock_req.params = {'correlation_id': 'test-correlation-456'}
        
        with patch('pieceinfo_api.function_app.aggregation_service') as mock_service:
            mock_result = Mock()
            mock_result.json.return_value = json.dumps({"test": "data"})
            mock_result.sku = "67007500"
            mock_result.vendor_code = "VIZIA"
            mock_result.vendor_name = "TEST"
            
            async def mock_get_aggregated(piece_number, correlation_id):
                # Verify correlation ID is passed
                self.assertEqual(correlation_id, 'test-correlation-456')
                return mock_result
                
            mock_service.get_aggregated_piece_info.return_value = mock_get_aggregated("170080637", "test-correlation-456")
            
            # Call the function
            result = get_piece_info(mock_req)


if __name__ == '__main__':
    # Set up test environment
    os.environ.setdefault('EXTERNAL_API_BASE_URL', 'https://apim-dev.nfm.com')
    os.environ.setdefault('API_TIMEOUT_SECONDS', '30')
    os.environ.setdefault('API_MAX_RETRIES', '3')
    os.environ.setdefault('MAX_BATCH_SIZE', '10')
    
    unittest.main()
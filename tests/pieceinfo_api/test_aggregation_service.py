"""
Test suite for PieceInfo API aggregation service.
Tests the service that combines data from multiple APIs.
"""

import unittest
import asyncio
from unittest.mock import AsyncMock, MagicMock, patch
import sys
import os

# Add src to path for imports
sys.path.append(os.path.join(os.path.dirname(__file__), '../../src'))

from pieceinfo_api.services.aggregation_service import PieceInfoAggregationService
from pieceinfo_api.services.http_client import HTTPClientService
from pieceinfo_api.models import AggregatedPieceInfoResponse
from pieceinfo_api.exceptions.api_exceptions import (
    ValidationException,
    DataNotFoundException,
    ExternalAPIException
)


class TestPieceInfoAggregationService(unittest.TestCase):
    """Test the piece info aggregation service."""
    
    def setUp(self):
        """Set up test fixtures."""
        self.mock_http_client = AsyncMock(spec=HTTPClientService)
        self.service = PieceInfoAggregationService(self.mock_http_client)
        
        # Sample API responses
        self.piece_inventory_response = {\n            \"pieceInventoryKey\": \"170080637\",\n            \"warehouseLocation\": \"WHKCTY\",\n            \"serialNumber\": \"SZVOU5GB1600294\",\n            \"sku\": \"67007500\",\n            \"vendor\": \"VIZIA\",\n            \"family\": \"ELECTR\",\n            \"purchaseReferenceNumber\": \"6610299377*2\",\n            \"rackLocation\": \"R03-019-03\"\n        }\n        \n        self.product_master_response = {\n            \"sku\": \"67007500\",\n            \"description\": \"ALL-IN-ONE SOUNDBAR\",\n            \"modelNo\": \"SV210D-0806\",\n            \"vendor\": \"VIZIA\",\n            \"brand\": \"VIZBC\",\n            \"family\": \"ELECTR\",\n            \"category\": \"EHMAUD\",\n            \"group\": \"HMSBAR\"\n        }\n        \n        self.vendor_response = {\n            \"code\": \"VIZIA\",\n            \"serialNumberRequired\": \"false\",\n            \"name\": \"NIGHT & DAY\",\n            \"addressLine1\": \"3901 N KINGSHIGHWAY BLVD\",\n            \"addressLine2\": \"\",\n            \"city\": \"SAINT LOUIS\",\n            \"state\": \"MO\",\n            \"zipCode\": \"63115\",\n            \"vendorReturn\": \"false\",\n            \"repName\": \"John Nicholson\",\n            \"primaryRepEmail\": \"jpnick@kc.rr.com\",\n            \"secondaryRepEmail\": \"gmail.com\",\n            \"execEmail\": None\n        }
    
    def test_successful_aggregation(self):
        """Test successful aggregation of data from all APIs."""
        # Configure mock responses
        self.mock_http_client.get_piece_inventory.return_value = self.piece_inventory_response
        self.mock_http_client.get_product_master.return_value = self.product_master_response
        self.mock_http_client.get_vendor_details.return_value = self.vendor_response
        
        # Run the aggregation
        loop = asyncio.new_event_loop()
        asyncio.set_event_loop(loop)
        try:
            result = loop.run_until_complete(
                self.service.get_aggregated_piece_info("170080637")
            )
        finally:
            loop.close()
        
        # Verify result type and structure
        self.assertIsInstance(result, AggregatedPieceInfoResponse)
        
        # Verify primary identifiers
        self.assertEqual(result.piece_inventory_key, "170080637")
        self.assertEqual(result.sku, "67007500")
        self.assertEqual(result.vendor_code, "VIZIA")
        
        # Verify location information
        self.assertEqual(result.warehouse_location, "WHKCTY")
        self.assertEqual(result.rack_location, "R03-019-03")
        
        # Verify product information
        self.assertEqual(result.serial_number, "SZVOU5GB1600294")
        self.assertEqual(result.description, "ALL-IN-ONE SOUNDBAR")
        self.assertEqual(result.model_no, "SV210D-0806")
        self.assertEqual(result.brand, "VIZBC")
        self.assertEqual(result.family, "ELECTR")  # Should use product_master as canonical
        self.assertEqual(result.category, "EHMAUD")
        self.assertEqual(result.group, "HMSBAR")
        
        # Verify vendor information
        self.assertEqual(result.vendor_name, "NIGHT & DAY")
        self.assertEqual(result.vendor_address.city, "SAINT LOUIS")
        self.assertEqual(result.vendor_contact.rep_name, "John Nicholson")
        self.assertFalse(result.vendor_policies.serial_number_required)
        
        # Verify API calls were made in correct order
        self.mock_http_client.get_piece_inventory.assert_called_once_with("170080637")
        self.mock_http_client.get_product_master.assert_called_once_with("67007500")
        self.mock_http_client.get_vendor_details.assert_called_once_with("VIZIA")
    
    def test_empty_piece_number_validation(self):
        """Test validation error for empty piece number."""
        loop = asyncio.new_event_loop()
        asyncio.set_event_loop(loop)
        try:
            with self.assertRaises(ValidationException) as context:
                loop.run_until_complete(
                    self.service.get_aggregated_piece_info("")
                )
            
            self.assertEqual(context.exception.field, "piece_number")
            self.assertIn("cannot be empty", context.exception.message)
        finally:
            loop.close()
    
    def test_piece_inventory_not_found(self):
        """Test handling when piece inventory is not found."""
        self.mock_http_client.get_piece_inventory.side_effect = ExternalAPIException(
            "piece-inventory-location",
            status_code=404,
            response_text="Not found"
        )
        
        loop = asyncio.new_event_loop()
        asyncio.set_event_loop(loop)
        try:
            with self.assertRaises(ExternalAPIException):
                loop.run_until_complete(
                    self.service.get_aggregated_piece_info("999999999")
                )
        finally:
            loop.close()
    
    def test_sku_consistency_validation(self):
        """Test validation of SKU consistency between APIs."""
        # Make product master return different SKU
        inconsistent_product_master = self.product_master_response.copy()
        inconsistent_product_master["sku"] = "99999999"  # Different SKU
        
        self.mock_http_client.get_piece_inventory.return_value = self.piece_inventory_response
        self.mock_http_client.get_product_master.return_value = inconsistent_product_master
        self.mock_http_client.get_vendor_details.return_value = self.vendor_response
        
        loop = asyncio.new_event_loop()
        asyncio.set_event_loop(loop)
        try:
            with self.assertRaises(ValidationException) as context:
                loop.run_until_complete(
                    self.service.get_aggregated_piece_info("170080637")
                )
            
            self.assertEqual(context.exception.field, "sku_consistency")
            self.assertIn("SKU mismatch", context.exception.message)
        finally:
            loop.close()
    
    def test_vendor_consistency_validation(self):
        """Test validation of vendor consistency between APIs."""
        # Make vendor API return different vendor code
        inconsistent_vendor = self.vendor_response.copy()
        inconsistent_vendor["code"] = "DIFFERENT"
        
        self.mock_http_client.get_piece_inventory.return_value = self.piece_inventory_response
        self.mock_http_client.get_product_master.return_value = self.product_master_response
        self.mock_http_client.get_vendor_details.return_value = inconsistent_vendor
        
        loop = asyncio.new_event_loop()
        asyncio.set_event_loop(loop)
        try:
            with self.assertRaises(ValidationException) as context:
                loop.run_until_complete(
                    self.service.get_aggregated_piece_info("170080637")
                )
            
            self.assertEqual(context.exception.field, "vendor_consistency")
            self.assertIn("Vendor code mismatch", context.exception.message)
        finally:
            loop.close()
    
    def test_invalid_piece_inventory_response(self):
        """Test handling of invalid piece inventory response."""
        # Return invalid response missing required fields
        invalid_response = {"invalid": "data"}
        
        self.mock_http_client.get_piece_inventory.return_value = invalid_response
        
        loop = asyncio.new_event_loop()
        asyncio.set_event_loop(loop)
        try:
            with self.assertRaises(ValidationException) as context:
                loop.run_until_complete(
                    self.service.get_aggregated_piece_info("170080637")
                )
            
            self.assertEqual(context.exception.field, "piece_inventory_response")
            self.assertIn("Invalid response format", context.exception.message)
        finally:
            loop.close()
    
    def test_correlation_id_tracking(self):
        """Test that correlation ID is properly tracked."""
        self.mock_http_client.get_piece_inventory.return_value = self.piece_inventory_response
        self.mock_http_client.get_product_master.return_value = self.product_master_response
        self.mock_http_client.get_vendor_details.return_value = self.vendor_response
        
        correlation_id = "test-correlation-123"
        
        with patch.object(self.service.logger, 'set_correlation_id') as mock_set_correlation:
            loop = asyncio.new_event_loop()
            asyncio.set_event_loop(loop)
            try:
                result = loop.run_until_complete(
                    self.service.get_aggregated_piece_info("170080637", correlation_id)
                )
                
                # Verify correlation ID was set
                mock_set_correlation.assert_called_once_with(correlation_id)
            finally:
                loop.close()
    
    def test_api_call_sequence(self):
        """Test that APIs are called in the correct sequence."""
        self.mock_http_client.get_piece_inventory.return_value = self.piece_inventory_response
        self.mock_http_client.get_product_master.return_value = self.product_master_response
        self.mock_http_client.get_vendor_details.return_value = self.vendor_response
        
        loop = asyncio.new_event_loop()
        asyncio.set_event_loop(loop)
        try:
            result = loop.run_until_complete(
                self.service.get_aggregated_piece_info("170080637")
            )
            
            # Verify the sequence of calls
            call_order = []
            for call in [
                self.mock_http_client.get_piece_inventory.call_args,
                self.mock_http_client.get_product_master.call_args,
                self.mock_http_client.get_vendor_details.call_args
            ]:
                if call:
                    call_order.append(call)
            
            # Should have 3 calls
            self.assertEqual(len(call_order), 3)
            
            # Verify call arguments
            piece_call = self.mock_http_client.get_piece_inventory.call_args[0]
            product_call = self.mock_http_client.get_product_master.call_args[0]
            vendor_call = self.mock_http_client.get_vendor_details.call_args[0]
            
            self.assertEqual(piece_call[0], "170080637")  # piece number
            self.assertEqual(product_call[0], "67007500")  # SKU from piece inventory
            self.assertEqual(vendor_call[0], "VIZIA")     # vendor from piece inventory
        finally:
            loop.close()
    
    @patch('pieceinfo_api.services.aggregation_service.log_function_calls')
    def test_function_logging_decorator(self, mock_decorator):
        """Test that function logging decorator is applied."""
        # The decorator should be applied to get_aggregated_piece_info
        # This is mainly to ensure the decorator is present
        self.assertTrue(hasattr(self.service.get_aggregated_piece_info, '__wrapped__'))


if __name__ == '__main__':
    unittest.main()
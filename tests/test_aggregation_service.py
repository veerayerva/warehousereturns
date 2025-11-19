"""
Unit Tests for Aggregation Service

This module contains comprehensive unit tests for the AggregationService class,
testing API orchestration, data aggregation, error handling, and validation logic.

Author: System Generated
Version: 1.0.0 (Production Ready)
"""

import pytest
import asyncio
from unittest.mock import Mock, AsyncMock, patch
from datetime import datetime
import httpx

# Import the module under test
import sys
import os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'src', 'pieceinfo_api'))

from services.aggregation_service import AggregationService

class TestAggregationService:
    """Test suite for AggregationService class."""
    
    # ===================================================================
    # INITIALIZATION TESTS
    # ===================================================================
    
    @pytest.mark.unit
    def test_service_initialization(self):
        """Test aggregation service initialization."""
        mock_http_client = Mock()
        service = AggregationService(mock_http_client)
        
        assert service.http_client == mock_http_client
        assert hasattr(service, 'logger')
    
    # ===================================================================
    # SUCCESSFUL AGGREGATION TESTS
    # ===================================================================
    
    @pytest.mark.unit
    @pytest.mark.asyncio
    async def test_successful_piece_info_aggregation(
        self,
        sample_piece_number,
        mock_http_client,
        mock_successful_http_responses,
        expected_aggregated_response
    ):
        """Test successful aggregation of piece information from all APIs."""
        # Configure mock HTTP client
        mock_successful_http_responses(mock_http_client)
        
        service = AggregationService(mock_http_client)
        
        result = await service.get_piece_info(sample_piece_number)
        
        # Verify the result structure matches expected response
        assert result['piece_inventory_key'] == expected_aggregated_response['piece_inventory_key']
        assert result['sku'] == expected_aggregated_response['sku']
        assert result['vendor_code'] == expected_aggregated_response['vendor_code']
        assert result['description'] == expected_aggregated_response['description']
        assert result['vendor_name'] == expected_aggregated_response['vendor_name']
        
        # Verify nested structures
        assert result['vendor_address'] == expected_aggregated_response['vendor_address']
        assert result['vendor_contact'] == expected_aggregated_response['vendor_contact']
        assert result['vendor_policies'] == expected_aggregated_response['vendor_policies']
        
        # Verify all three API calls were made
        assert mock_http_client.get.call_count == 3
        
        # Verify correct endpoints were called
        call_args = [call[0][0] for call in mock_http_client.get.call_args_list]
        assert any("piece-inventory-location" in arg for arg in call_args)
        assert any("product-master" in arg for arg in call_args)
        assert any("vendor" in arg for arg in call_args)
    
    @pytest.mark.unit
    @pytest.mark.asyncio
    async def test_aggregation_with_missing_optional_fields(self, mock_http_client):
        """Test aggregation handles missing optional fields gracefully."""
        # Mock responses with missing optional fields
        piece_inventory_response = {
            "pieceInventoryKey": "123456789",
            "sku": "TEST-SKU",
            "vendorCode": "TEST-VENDOR"
            # Missing optional fields like serialNumber, rackLocation, etc.
        }
        
        product_master_response = {
            "description": "Test Product"
            # Missing optional fields like modelNo, brand, etc.
        }
        
        vendor_details_response = {
            "name": "Test Vendor"
            # Missing optional fields like address, contact info, etc.
        }
        
        async def mock_get(endpoint):
            if "piece-inventory-location" in endpoint:
                return piece_inventory_response
            elif "product-master" in endpoint:
                return product_master_response
            elif "vendor" in endpoint:
                return vendor_details_response
            else:
                raise ValueError(f"Unexpected endpoint: {endpoint}")
        
        mock_http_client.get.side_effect = mock_get
        
        service = AggregationService(mock_http_client)
        result = await service.get_piece_info("123456789")
        
        # Verify required fields are present
        assert result['piece_inventory_key'] == "123456789"
        assert result['sku'] == "TEST-SKU"
        assert result['vendor_code'] == "TEST-VENDOR"
        assert result['description'] == "Test Product"
        assert result['vendor_name'] == "Test Vendor"
        
        # Verify missing optional fields have default values
        assert result['serial_number'] == ""
        assert result['rack_location'] == ""
        assert result['model_no'] == ""
        assert result['brand'] == ""
        assert result['vendor_address']['address_line1'] == ""
        assert result['vendor_contact']['rep_name'] == ""
        assert result['vendor_policies']['serial_number_required'] == False
    
    # ===================================================================
    # ERROR HANDLING TESTS
    # ===================================================================
    
    @pytest.mark.unit
    @pytest.mark.asyncio
    async def test_piece_inventory_api_failure(self, mock_http_client):
        """Test handling when piece inventory API fails."""
        mock_http_client.get.side_effect = httpx.HTTPStatusError(
            "Not Found", 
            request=Mock(), 
            response=Mock(status_code=404, text="Piece not found")
        )
        
        service = AggregationService(mock_http_client)
        
        with pytest.raises(Exception) as exc_info:
            await service.get_piece_info("INVALID-PIECE")
        
        # Verify error is properly propagated
        assert "Aggregation failed for piece INVALID-PIECE" in str(exc_info.value)
        
        # Verify only piece inventory API was called (failed on first call)
        assert mock_http_client.get.call_count == 1
    
    @pytest.mark.unit
    @pytest.mark.asyncio
    async def test_product_master_api_failure(self, mock_http_client):
        """Test handling when product master API fails."""
        # First call (piece inventory) succeeds, second call (product master) fails
        mock_responses = [
            {"pieceInventoryKey": "123", "sku": "TEST-SKU", "vendorCode": "VENDOR"},
            httpx.HTTPStatusError("Not Found", request=Mock(), response=Mock(status_code=404))
        ]
        
        mock_http_client.get.side_effect = mock_responses
        
        service = AggregationService(mock_http_client)
        
        with pytest.raises(Exception):
            await service.get_piece_info("123")
        
        # Verify two API calls were made (second one failed)
        assert mock_http_client.get.call_count == 2
    
    @pytest.mark.unit
    @pytest.mark.asyncio
    async def test_vendor_api_failure(self, mock_http_client):
        """Test handling when vendor API fails."""
        # First two calls succeed, third call (vendor) fails
        mock_responses = [
            {"pieceInventoryKey": "123", "sku": "TEST-SKU", "vendorCode": "VENDOR"},
            {"description": "Test Product"},
            httpx.HTTPStatusError("Not Found", request=Mock(), response=Mock(status_code=404))
        ]
        
        mock_http_client.get.side_effect = mock_responses
        
        service = AggregationService(mock_http_client)
        
        with pytest.raises(Exception):
            await service.get_piece_info("123")
        
        # Verify all three API calls were attempted
        assert mock_http_client.get.call_count == 3
    
    # ===================================================================
    # DATA VALIDATION TESTS
    # ===================================================================
    
    @pytest.mark.unit
    @pytest.mark.asyncio
    async def test_missing_required_fields_validation(self, mock_http_client):
        """Test validation of missing required fields from piece inventory."""
        # Mock piece inventory response missing required fields
        piece_inventory_response = {
            "pieceInventoryKey": "123456789"
            # Missing required 'sku' and 'vendorCode' fields
        }
        
        mock_http_client.get.return_value = piece_inventory_response
        
        service = AggregationService(mock_http_client)
        
        with pytest.raises(ValueError) as exc_info:
            await service.get_piece_info("123456789")
        
        # Verify validation error message
        assert "Missing required field" in str(exc_info.value)
    
    @pytest.mark.unit
    @pytest.mark.asyncio
    async def test_empty_required_fields_validation(self, mock_http_client):
        """Test validation of empty required fields."""
        piece_inventory_response = {
            "pieceInventoryKey": "123456789",
            "sku": "",  # Empty required field
            "vendorCode": "VENDOR"
        }
        
        mock_http_client.get.return_value = piece_inventory_response
        
        service = AggregationService(mock_http_client)
        
        with pytest.raises(ValueError) as exc_info:
            await service.get_piece_info("123456789")
        
        assert "is empty or None" in str(exc_info.value)
    
    # ===================================================================
    # BOOLEAN CONVERSION TESTS
    # ===================================================================
    
    @pytest.mark.unit
    def test_boolean_conversion_method(self):
        """Test the _convert_to_boolean utility method."""
        service = AggregationService(Mock())
        
        # Test various true values
        assert service._convert_to_boolean("true") == True
        assert service._convert_to_boolean("True") == True
        assert service._convert_to_boolean("TRUE") == True
        assert service._convert_to_boolean("1") == True
        assert service._convert_to_boolean(1) == True
        assert service._convert_to_boolean("yes") == True
        assert service._convert_to_boolean("on") == True
        assert service._convert_to_boolean(True) == True
        
        # Test various false values
        assert service._convert_to_boolean("false") == False
        assert service._convert_to_boolean("False") == False
        assert service._convert_to_boolean("FALSE") == False
        assert service._convert_to_boolean("0") == False
        assert service._convert_to_boolean(0) == False
        assert service._convert_to_boolean("no") == False
        assert service._convert_to_boolean("off") == False
        assert service._convert_to_boolean(False) == False
        assert service._convert_to_boolean(None) == False
        assert service._convert_to_boolean("") == False
        
        # Test edge cases
        assert service._convert_to_boolean("  true  ") == True  # With spaces
        assert service._convert_to_boolean("random_string") == False
        assert service._convert_to_boolean([]) == False
        assert service._convert_to_boolean({}) == False
    
    # ===================================================================
    # PERFORMANCE AND MONITORING TESTS
    # ===================================================================
    
    @pytest.mark.unit
    @pytest.mark.asyncio
    async def test_processing_time_logging(self, mock_http_client, caplog):
        """Test that processing time is properly logged."""
        mock_successful_http_responses = lambda client: setattr(
            client.get, 'side_effect',
            [
                {"pieceInventoryKey": "123", "sku": "SKU", "vendorCode": "VENDOR"},
                {"description": "Product"},
                {"name": "Vendor"}
            ]
        )
        
        mock_successful_http_responses(mock_http_client)
        
        service = AggregationService(mock_http_client)
        
        with caplog.at_level("INFO"):
            await service.get_piece_info("123")
        
        # Verify processing time is logged
        assert any("Successfully aggregated piece info for: 123 in" in record.message 
                  for record in caplog.records)
    
    # ===================================================================
    # ENDPOINT CONSTRUCTION TESTS
    # ===================================================================
    
    @pytest.mark.unit
    @pytest.mark.asyncio
    async def test_correct_endpoint_construction(self, mock_http_client):
        """Test that API endpoints are constructed correctly."""
        piece_number = "170080637"
        sku = "67007500"
        vendor_code = "VIZIA"
        
        mock_responses = [
            {"pieceInventoryKey": piece_number, "sku": sku, "vendorCode": vendor_code},
            {"description": "Product"},
            {"name": "Vendor"}
        ]
        
        mock_http_client.get.side_effect = mock_responses
        
        service = AggregationService(mock_http_client)
        await service.get_piece_info(piece_number)
        
        # Verify correct endpoints were called with correct parameters
        call_args = [call[0][0] for call in mock_http_client.get.call_args_list]
        
        # Check piece inventory endpoint
        assert f"ihubservices/product/piece-inventory-location/{piece_number}" in call_args[0]
        
        # Check product master endpoint
        assert f"ihubservices/product/product-master/{sku}" in call_args[1]
        
        # Check vendor endpoint
        assert f"ihubservices/product/vendor/{vendor_code}" in call_args[2]
    
    # ===================================================================
    # SEQUENTIAL API CALL TESTS
    # ===================================================================
    
    @pytest.mark.unit
    @pytest.mark.asyncio
    async def test_sequential_api_calls(self, mock_http_client):
        """Test that API calls are made in the correct sequence."""
        call_order = []
        
        async def track_calls(endpoint):
            if "piece-inventory-location" in endpoint:
                call_order.append("piece_inventory")
                return {"pieceInventoryKey": "123", "sku": "SKU", "vendorCode": "VENDOR"}
            elif "product-master" in endpoint:
                call_order.append("product_master")
                return {"description": "Product"}
            elif "vendor" in endpoint:
                call_order.append("vendor")
                return {"name": "Vendor"}
        
        mock_http_client.get.side_effect = track_calls
        
        service = AggregationService(mock_http_client)
        await service.get_piece_info("123")
        
        # Verify correct call order
        assert call_order == ["piece_inventory", "product_master", "vendor"]
    
    # ===================================================================
    # EDGE CASE TESTS
    # ===================================================================
    
    @pytest.mark.unit
    @pytest.mark.asyncio
    async def test_null_response_handling(self, mock_http_client):
        """Test handling of null/None responses from APIs."""
        mock_responses = [
            {"pieceInventoryKey": "123", "sku": "SKU", "vendorCode": "VENDOR"},
            None,  # Null response from product master
            {"name": "Vendor"}
        ]
        
        mock_http_client.get.side_effect = mock_responses
        
        service = AggregationService(mock_http_client)
        
        with pytest.raises(Exception):
            await service.get_piece_info("123")
    
    @pytest.mark.unit
    @pytest.mark.asyncio
    async def test_malformed_json_response(self, mock_http_client):
        """Test handling of malformed JSON responses."""
        # Simulate a response that's not a dictionary
        mock_responses = [
            "invalid_json_string",  # Not a dictionary
            {"description": "Product"},
            {"name": "Vendor"}
        ]
        
        mock_http_client.get.side_effect = mock_responses
        
        service = AggregationService(mock_http_client)
        
        with pytest.raises(Exception):
            await service.get_piece_info("123")
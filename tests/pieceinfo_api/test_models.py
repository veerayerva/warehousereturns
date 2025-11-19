"""
Test suite for PieceInfo API models.
Tests data validation, serialization, and model behavior.
"""

import unittest
from typing import Dict, Any
import json

from pieceinfo_api.models import (
    PieceInventoryResponse,
    ProductMasterResponse,
    VendorResponse,
    AggregatedPieceInfoResponse,
    VendorAddress,
    VendorContact,
    VendorPolicies,
    ErrorResponse
)
from pydantic import ValidationError


class TestAPIModels(unittest.TestCase):
    """Test API response models."""
    
    def setUp(self):
        """Set up test data."""
        self.piece_inventory_data = {
            "pieceInventoryKey": "170080637",
            "warehouseLocation": "WHKCTY",
            "serialNumber": "SZVOU5GB1600294",
            "sku": "67007500",
            "vendor": "VIZIA",
            "family": "ELECTR",
            "purchaseReferenceNumber": "6610299377*2",
            "rackLocation": "R03-019-03"
        }
        
        self.product_master_data = {
            "sku": "67007500",
            "description": "ALL-IN-ONE SOUNDBAR",
            "modelNo": "SV210D-0806",
            "vendor": "VIZIA",
            "brand": "VIZBC",
            "family": "ELECTR",
            "category": "EHMAUD",
            "group": "HMSBAR"
        }
        
        self.vendor_data = {
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
    
    def test_piece_inventory_response_valid(self):
        """Test valid piece inventory response parsing."""
        model = PieceInventoryResponse.parse_obj(self.piece_inventory_data)
        
        self.assertEqual(model.piece_inventory_key, "170080637")
        self.assertEqual(model.warehouse_location, "WHKCTY")
        self.assertEqual(model.serial_number, "SZVOU5GB1600294")
        self.assertEqual(model.sku, "67007500")
        self.assertEqual(model.vendor, "VIZIA")
        self.assertEqual(model.family, "ELECTR")
        self.assertEqual(model.purchase_reference_number, "6610299377*2")
        self.assertEqual(model.rack_location, "R03-019-03")
    
    def test_piece_inventory_response_missing_fields(self):
        """Test piece inventory response with missing required fields."""
        incomplete_data = self.piece_inventory_data.copy()
        del incomplete_data['sku']
        
        with self.assertRaises(ValidationError) as context:
            PieceInventoryResponse.parse_obj(incomplete_data)
        
        self.assertIn('sku', str(context.exception))
    
    def test_product_master_response_valid(self):
        """Test valid product master response parsing."""
        model = ProductMasterResponse.parse_obj(self.product_master_data)
        
        self.assertEqual(model.sku, "67007500")
        self.assertEqual(model.description, "ALL-IN-ONE SOUNDBAR")
        self.assertEqual(model.model_no, "SV210D-0806")
        self.assertEqual(model.vendor, "VIZIA")
        self.assertEqual(model.brand, "VIZBC")
        self.assertEqual(model.family, "ELECTR")
        self.assertEqual(model.category, "EHMAUD")
        self.assertEqual(model.group, "HMSBAR")
    
    def test_vendor_response_valid(self):
        """Test valid vendor response parsing."""
        model = VendorResponse.parse_obj(self.vendor_data)
        
        self.assertEqual(model.code, "VIZIA")
        self.assertEqual(model.serial_number_required, "false")
        self.assertEqual(model.name, "NIGHT & DAY")
        self.assertEqual(model.address_line1, "3901 N KINGSHIGHWAY BLVD")
        self.assertEqual(model.address_line2, "")
        self.assertEqual(model.city, "SAINT LOUIS")
        self.assertEqual(model.state, "MO")
        self.assertEqual(model.zip_code, "63115")
        self.assertEqual(model.vendor_return, "false")
        self.assertEqual(model.rep_name, "John Nicholson")
        self.assertEqual(model.primary_rep_email, "jpnick@kc.rr.com")
        self.assertEqual(model.secondary_rep_email, "gmail.com")
        self.assertIsNone(model.exec_email)
    
    def test_vendor_response_boolean_validation(self):
        """Test vendor response boolean string validation."""
        # Valid boolean strings
        valid_data = self.vendor_data.copy()
        valid_data['serialNumberRequired'] = "true"
        valid_data['vendorReturn'] = "true"
        
        model = VendorResponse.parse_obj(valid_data)
        self.assertEqual(model.serial_number_required, "true")
        self.assertEqual(model.vendor_return, "true")
        
        # Invalid boolean string
        invalid_data = self.vendor_data.copy()
        invalid_data['serialNumberRequired'] = "invalid"
        
        with self.assertRaises(ValidationError):
            VendorResponse.parse_obj(invalid_data)
    
    def test_aggregated_response_creation(self):
        """Test creating aggregated response from individual models."""
        piece_inventory = PieceInventoryResponse.parse_obj(self.piece_inventory_data)
        product_master = ProductMasterResponse.parse_obj(self.product_master_data)
        vendor = VendorResponse.parse_obj(self.vendor_data)
        
        # Create sub-models
        vendor_address = VendorAddress(
            address_line1=vendor.address_line1,
            address_line2=vendor.address_line2,
            city=vendor.city,
            state=vendor.state,
            zip_code=vendor.zip_code
        )
        
        vendor_contact = VendorContact(
            rep_name=vendor.rep_name,
            primary_rep_email=vendor.primary_rep_email,
            secondary_rep_email=vendor.secondary_rep_email,
            exec_email=vendor.exec_email
        )
        
        vendor_policies = VendorPolicies(
            serial_number_required=vendor.serial_number_required,
            vendor_return=vendor.vendor_return
        )
        
        # Create aggregated response
        aggregated = AggregatedPieceInfoResponse(
            piece_inventory_key=piece_inventory.piece_inventory_key,
            sku=piece_inventory.sku,
            vendor_code=piece_inventory.vendor,
            warehouse_location=piece_inventory.warehouse_location,
            rack_location=piece_inventory.rack_location,
            serial_number=piece_inventory.serial_number,
            description=product_master.description,
            model_no=product_master.model_no,
            brand=product_master.brand,
            family=product_master.family,
            category=product_master.category,
            group=product_master.group,
            purchase_reference_number=piece_inventory.purchase_reference_number,
            vendor_name=vendor.name,
            vendor_address=vendor_address,
            vendor_contact=vendor_contact,
            vendor_policies=vendor_policies
        )
        
        # Verify aggregated data
        self.assertEqual(aggregated.piece_inventory_key, "170080637")
        self.assertEqual(aggregated.sku, "67007500")
        self.assertEqual(aggregated.vendor_code, "VIZIA")
        self.assertEqual(aggregated.description, "ALL-IN-ONE SOUNDBAR")
        self.assertEqual(aggregated.vendor_name, "NIGHT & DAY")
        self.assertEqual(aggregated.vendor_address.city, "SAINT LOUIS")
        self.assertEqual(aggregated.vendor_contact.rep_name, "John Nicholson")
        self.assertFalse(aggregated.vendor_policies.serial_number_required)
        self.assertFalse(aggregated.vendor_policies.vendor_return)
    
    def test_vendor_policies_boolean_conversion(self):
        """Test VendorPolicies boolean conversion from strings."""
        # Test string to boolean conversion
        policies = VendorPolicies(
            serial_number_required="true",
            vendor_return="false"
        )
        
        self.assertTrue(policies.serial_number_required)
        self.assertFalse(policies.vendor_return)
        
        # Test actual boolean values
        policies2 = VendorPolicies(
            serial_number_required=True,
            vendor_return=False
        )
        
        self.assertTrue(policies2.serial_number_required)
        self.assertFalse(policies2.vendor_return)
    
    def test_error_response_model(self):
        """Test error response model."""
        error = ErrorResponse(
            error="validation_error",
            message="Test error message",
            details={"field": "test_field", "value": "invalid"},
            correlation_id="test-correlation-123"
        )
        
        self.assertEqual(error.error, "validation_error")
        self.assertEqual(error.message, "Test error message")
        self.assertEqual(error.details["field"], "test_field")
        self.assertEqual(error.correlation_id, "test-correlation-123")
    
    def test_json_serialization(self):
        """Test JSON serialization of models."""
        piece_inventory = PieceInventoryResponse.parse_obj(self.piece_inventory_data)
        
        # Test JSON serialization
        json_str = piece_inventory.json()
        self.assertIsInstance(json_str, str)
        
        # Test that JSON can be parsed back
        parsed_data = json.loads(json_str)
        self.assertEqual(parsed_data['piece_inventory_key'], "170080637")
        self.assertEqual(parsed_data['sku'], "67007500")
    
    def test_model_field_aliases(self):
        """Test that field aliases work correctly."""
        # Test using both field names and aliases
        data_with_aliases = {
            "pieceInventoryKey": "170080637",  # alias
            "warehouse_location": "WHKCTY",     # field name
            "serialNumber": "SZVOU5GB1600294", # alias
            "sku": "67007500",
            "vendor": "VIZIA",
            "family": "ELECTR",
            "purchaseReferenceNumber": "6610299377*2",  # alias
            "rack_location": "R03-019-03"               # field name
        }
        
        model = PieceInventoryResponse.parse_obj(data_with_aliases)
        self.assertEqual(model.piece_inventory_key, "170080637")
        self.assertEqual(model.warehouse_location, "WHKCTY")


if __name__ == '__main__':
    unittest.main()
"""
Model Structure Verification Script
Verifies that all models can be imported correctly after reorganization.
"""

import sys
import os

# Add src to path for imports
sys.path.append(os.path.join(os.path.dirname(__file__), '../src'))

def test_model_imports():
    """Test that all models can be imported correctly."""
    
    try:
        print("Testing model imports...")
        
        # Test individual model imports
        from pieceinfo_api.models.PieceInventoryModel import PieceInventoryResponse
        print("✓ PieceInventoryModel imported successfully")
        
        from pieceinfo_api.models.ProductMasterModel import ProductMasterResponse
        print("✓ ProductMasterModel imported successfully")
        
        from pieceinfo_api.models.VendorModel import VendorResponse
        print("✓ VendorModel imported successfully")
        
        from pieceinfo_api.models.VendorDetailsModel import VendorAddress, VendorContact, VendorPolicies
        print("✓ VendorDetailsModel imported successfully")
        
        from pieceinfo_api.models.AggregatedPieceInfoModel import AggregatedPieceInfoResponse
        print("✓ AggregatedPieceInfoModel imported successfully")
        
        from pieceinfo_api.models.ErrorResponseModel import ErrorResponse
        print("✓ ErrorResponseModel imported successfully")
        
        # Test package-level imports
        from pieceinfo_api.models import (
            PieceInventoryResponse,
            ProductMasterResponse,
            VendorResponse,
            VendorAddress,
            VendorContact,
            VendorPolicies,
            AggregatedPieceInfoResponse,
            ErrorResponse
        )
        print("✓ Package-level imports work correctly")
        
        # Test model instantiation
        sample_piece_inventory = {
            "pieceInventoryKey": "170080637",
            "warehouseLocation": "WHKCTY",
            "serialNumber": "SZVOU5GB1600294",
            "sku": "67007500",
            "vendor": "VIZIA",
            "family": "ELECTR",
            "purchaseReferenceNumber": "6610299377*2",
            "rackLocation": "R03-019-03"
        }
        
        piece_model = PieceInventoryResponse.parse_obj(sample_piece_inventory)
        print(f"✓ PieceInventoryResponse model created: {piece_model.sku}")
        
        # Test vendor policies boolean conversion
        vendor_policies = VendorPolicies(
            serial_number_required="true",
            vendor_return="false"
        )
        print(f"✓ VendorPolicies boolean conversion: serial_required={vendor_policies.serial_number_required}, returns={vendor_policies.vendor_return}")
        
        # Test error response
        error = ErrorResponse(
            error="test_error",
            message="Test error message",
            correlation_id="test-123"
        )
        print(f"✓ ErrorResponse model created: {error.error}")
        
        print("\n🎉 All model imports and instantiation tests passed!")
        return True
        
    except Exception as e:
        print(f"❌ Import test failed: {e}")
        import traceback
        traceback.print_exc()
        return False


if __name__ == "__main__":
    success = test_model_imports()
    if success:
        print("\n✅ Model reorganization completed successfully!")
        print("All models are properly separated and can be imported correctly.")
    else:
        print("\n❌ Model reorganization has issues that need to be resolved.")
        sys.exit(1)
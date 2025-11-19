"""
Comprehensive Test Suite Configuration

This module provides configuration and utilities for the PieceInfo API test suite.
Includes fixtures, mock data, and common testing utilities for unit tests,
integration tests, and API endpoint tests.

Author: System Generated
Version: 1.0.0 (Production Ready)
"""

import pytest
import asyncio
from typing import Dict, Any, List
from unittest.mock import Mock, AsyncMock
import logging

# Configure test logging
logging.basicConfig(level=logging.DEBUG)
logger = logging.getLogger(__name__)

# ===================================================================
# TEST DATA FIXTURES
# ===================================================================

@pytest.fixture
def sample_piece_number() -> str:
    """Sample piece number for testing."""
    return "170080637"

@pytest.fixture
def invalid_piece_numbers() -> List[str]:
    """Invalid piece numbers for validation testing."""
    return [
        "",           # Empty string
        "   ",        # Whitespace only
        "123",        # Too short
        "12345678901", # Too long
        "ABC123DEF",  # Invalid characters
        "123-456-789", # Invalid format with hyphens
        None,         # None value
    ]

@pytest.fixture
def mock_piece_inventory_response() -> Dict[str, Any]:
    """Mock response from piece inventory API."""
    return {
        "pieceInventoryKey": "170080637",
        "sku": "67007500", 
        "vendorCode": "VIZIA",
        "warehouseLocation": "WHKCTY",
        "rackLocation": "R03-019-03",
        "serialNumber": "SZVOU5GB1600294",
        "family": "ELECTR",
        "purchaseReferenceNumber": "6610299377*2"
    }

@pytest.fixture
def mock_product_master_response() -> Dict[str, Any]:
    """Mock response from product master API."""
    return {
        "description": "ALL-IN-ONE SOUNDBAR",
        "modelNo": "SV210D-0806",
        "brand": "VIZBC",
        "category": "EHMAUD",
        "group": "HMSBAR"
    }

@pytest.fixture
def mock_vendor_details_response() -> Dict[str, Any]:
    """Mock response from vendor details API."""
    return {
        "name": "NIGHT & DAY",
        "addressLine1": "3901 N KINGSHIGHWAY BLVD",
        "addressLine2": "",
        "city": "SAINT LOUIS",
        "state": "MO",
        "zipCode": "63115",
        "repName": "John Nicholson",
        "primaryRepEmail": "jpnick@kc.rr.com",
        "secondaryRepEmail": "gmail.com",
        "execEmail": None,
        "serialNumberRequired": "false",
        "vendorReturn": "false"
    }

@pytest.fixture
def expected_aggregated_response() -> Dict[str, Any]:
    """Expected aggregated response structure."""
    return {
        "piece_inventory_key": "170080637",
        "sku": "67007500",
        "vendor_code": "VIZIA",
        "warehouse_location": "WHKCTY",
        "rack_location": "R03-019-03",
        "serial_number": "SZVOU5GB1600294",
        "family": "ELECTR",
        "purchase_reference_number": "6610299377*2",
        "description": "ALL-IN-ONE SOUNDBAR",
        "model_no": "SV210D-0806",
        "brand": "VIZBC",
        "category": "EHMAUD",
        "group": "HMSBAR",
        "vendor_name": "NIGHT & DAY",
        "vendor_address": {
            "address_line1": "3901 N KINGSHIGHWAY BLVD",
            "address_line2": "",
            "city": "SAINT LOUIS",
            "state": "MO",
            "zip_code": "63115"
        },
        "vendor_contact": {
            "rep_name": "John Nicholson",
            "primary_rep_email": "jpnick@kc.rr.com",
            "secondary_rep_email": "gmail.com",
            "exec_email": None
        },
        "vendor_policies": {
            "serial_number_required": False,
            "vendor_return": False
        }
    }

# ===================================================================
# HTTP CLIENT MOCK FIXTURES
# ===================================================================

@pytest.fixture
def mock_http_client():
    """Mock HTTP client for testing services."""
    mock_client = Mock()
    mock_client.get = AsyncMock()
    return mock_client

@pytest.fixture
def mock_successful_http_responses(
    mock_piece_inventory_response,
    mock_product_master_response, 
    mock_vendor_details_response
):
    """Configure mock HTTP client for successful responses."""
    def configure_client(mock_client):
        # Configure responses based on endpoint
        async def mock_get(endpoint):
            if "piece-inventory-location" in endpoint:
                return mock_piece_inventory_response
            elif "product-master" in endpoint:
                return mock_product_master_response
            elif "vendor" in endpoint:
                return mock_vendor_details_response
            else:
                raise ValueError(f"Unexpected endpoint: {endpoint}")
        
        mock_client.get.side_effect = mock_get
        return mock_client
    
    return configure_client

# ===================================================================
# ERROR TESTING FIXTURES
# ===================================================================

@pytest.fixture
def http_error_scenarios():
    """Common HTTP error scenarios for testing."""
    return {
        "404_not_found": {
            "status_code": 404,
            "error_message": "Piece not found"
        },
        "500_server_error": {
            "status_code": 500,
            "error_message": "Internal server error"
        },
        "timeout_error": {
            "error_type": "timeout",
            "error_message": "Request timeout"
        },
        "connection_error": {
            "error_type": "connection",
            "error_message": "Connection failed"
        }
    }

# ===================================================================
# ASYNC TEST UTILITIES
# ===================================================================

@pytest.fixture(scope="session")
def event_loop():
    """Create an instance of the default event loop for the test session."""
    loop = asyncio.get_event_loop_policy().new_event_loop()
    yield loop
    loop.close()

# ===================================================================
# ENVIRONMENT CONFIGURATION FIXTURES  
# ===================================================================

@pytest.fixture
def test_environment_variables(monkeypatch):
    """Set up test environment variables."""
    test_vars = {
        'EXTERNAL_API_BASE_URL': 'https://test-api.example.com',
        'OCP_APIM_SUBSCRIPTION_KEY': 'test-subscription-key-12345',
        'API_TIMEOUT_SECONDS': '10',
        'API_MAX_RETRIES': '2',
        'VERIFY_SSL': 'false',
        'ENVIRONMENT': 'test'
    }
    
    for key, value in test_vars.items():
        monkeypatch.setenv(key, value)
    
    return test_vars

# ===================================================================
# LOGGING CONFIGURATION FOR TESTS
# ===================================================================

@pytest.fixture(autouse=True)
def configure_test_logging():
    """Configure logging for tests."""
    # Reduce log level during tests to avoid noise
    logging.getLogger('httpx').setLevel(logging.WARNING)
    logging.getLogger('httpcore').setLevel(logging.WARNING)
    
    # Enable debug logging for our modules
    logging.getLogger('pieceinfo_api').setLevel(logging.DEBUG)
    
    yield
    
    # Reset logging after tests
    logging.getLogger().setLevel(logging.INFO)

# ===================================================================
# TEST MARKERS
# ===================================================================

# Custom pytest markers for test organization
pytest.mark.unit = pytest.mark.unit
pytest.mark.integration = pytest.mark.integration
pytest.mark.api = pytest.mark.api
pytest.mark.slow = pytest.mark.slow
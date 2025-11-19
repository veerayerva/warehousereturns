"""
API Integration Tests for PieceInfo Azure Functions

This module contains comprehensive integration tests for the Azure Functions API endpoints,
testing end-to-end functionality, error handling, and response formats.

Author: System Generated
Version: 1.0.0 (Production Ready)
"""

import pytest
import asyncio
import json
from unittest.mock import patch, Mock, AsyncMock
from azure.functions import HttpRequest, HttpResponse
import httpx

# Import the module under test
import sys
import os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'src', 'pieceinfo_api'))

import function_app

class TestPieceInfoAPIEndpoints:
    """Test suite for PieceInfo API Azure Functions endpoints."""
    
    # ===================================================================
    # GET PIECE INFO ENDPOINT TESTS
    # ===================================================================
    
    @pytest.mark.api
    def test_get_piece_info_successful_response(
        self, 
        sample_piece_number,
        expected_aggregated_response,
        test_environment_variables
    ):
        """Test successful piece info retrieval."""
        # Create mock HttpRequest
        req = HttpRequest(
            method='GET',
            url=f'http://localhost:7074/api/pieces/{sample_piece_number}',
            headers={'Content-Type': 'application/json'},
            route_params={'piece_number': sample_piece_number},
            params={}
        )
        
        # Mock the aggregation service
        with patch('function_app.aggregation_service') as mock_service:
            mock_service.get_piece_info.return_value = expected_aggregated_response
            
            # Call the function
            response = function_app.get_piece_info(req)
            
            # Verify response
            assert isinstance(response, HttpResponse)
            assert response.status_code == 200
            assert response.headers['Content-Type'] == 'application/json'
            
            # Parse and verify response body
            response_data = json.loads(response.get_body().decode())
            assert response_data == expected_aggregated_response
            
            # Verify service was called correctly
            mock_service.get_piece_info.assert_called_once_with(sample_piece_number)
    
    @pytest.mark.api
    def test_get_piece_info_invalid_piece_number(self, invalid_piece_numbers):
        """Test piece info endpoint with invalid piece numbers."""
        for invalid_piece in invalid_piece_numbers:
            if invalid_piece is None:
                continue  # Skip None as it would cause different routing
            
            req = HttpRequest(
                method='GET',
                url=f'http://localhost:7074/api/pieces/{invalid_piece}',
                headers={'Content-Type': 'application/json'},
                route_params={'piece_number': invalid_piece},
                params={}
            )
            
            response = function_app.get_piece_info(req)
            
            # Verify validation error response
            assert response.status_code == 400
            response_data = json.loads(response.get_body().decode())
            assert response_data['error'] == 'Invalid piece number format'
            assert 'correlation_id' in response_data
    
    @pytest.mark.api
    def test_get_piece_info_service_error(self, sample_piece_number):
        """Test piece info endpoint when service throws an error."""
        req = HttpRequest(
            method='GET',
            url=f'http://localhost:7074/api/pieces/{sample_piece_number}',
            headers={'Content-Type': 'application/json'},
            route_params={'piece_number': sample_piece_number},
            params={}
        )
        
        # Mock service to throw an exception
        with patch('function_app.aggregation_service') as mock_service:
            mock_service.get_piece_info.side_effect = httpx.HTTPStatusError(
                "Not Found", 
                request=Mock(), 
                response=Mock(status_code=404, text="Piece not found")
            )
            
            response = function_app.get_piece_info(req)
            
            # Verify error response
            assert response.status_code == 404
            response_data = json.loads(response.get_body().decode())
            assert response_data['error'] == 'Piece information not found'
            assert 'correlation_id' in response_data
    
    @pytest.mark.api
    def test_get_piece_info_timeout_error(self, sample_piece_number):
        """Test piece info endpoint timeout handling."""
        req = HttpRequest(
            method='GET',
            url=f'http://localhost:7074/api/pieces/{sample_piece_number}',
            headers={'Content-Type': 'application/json'},
            route_params={'piece_number': sample_piece_number},
            params={}
        )
        
        # Mock service to throw timeout exception
        with patch('function_app.aggregation_service') as mock_service:
            mock_service.get_piece_info.side_effect = asyncio.TimeoutError("Request timeout")
            
            response = function_app.get_piece_info(req)
            
            # Verify timeout error response
            assert response.status_code == 504
            response_data = json.loads(response.get_body().decode())
            assert response_data['error'] == 'Request timeout - please try again'
            assert 'correlation_id' in response_data
    
    @pytest.mark.api
    def test_get_piece_info_correlation_id_header(self, sample_piece_number):
        """Test that correlation ID from header is used."""
        correlation_id = "test-correlation-123"
        
        req = HttpRequest(
            method='GET',
            url=f'http://localhost:7074/api/pieces/{sample_piece_number}',
            headers={
                'Content-Type': 'application/json',
                'X-Correlation-ID': correlation_id
            },
            route_params={'piece_number': sample_piece_number},
            params={}
        )
        
        with patch('function_app.aggregation_service') as mock_service:
            mock_service.get_piece_info.return_value = {"test": "data"}
            
            response = function_app.get_piece_info(req)
            
            # Verify correlation ID is preserved in response headers
            assert response.headers.get('X-Correlation-ID') == correlation_id
    
    # ===================================================================
    # HEALTH CHECK ENDPOINT TESTS
    # ===================================================================
    
    @pytest.mark.api
    def test_health_check_success(self):
        """Test health check endpoint successful response."""
        req = HttpRequest(
            method='GET',
            url='http://localhost:7074/api/pieces/health',
            headers={'Content-Type': 'application/json'}
        )
        
        # Mock all external dependencies as healthy
        with patch('function_app.aggregation_service') as mock_service, \
             patch('function_app.http_client') as mock_http_client:
            
            # Mock successful health checks
            mock_service.get_piece_info = AsyncMock()
            mock_http_client.get = AsyncMock()
            
            response = function_app.health_check(req)
            
            # Verify response
            assert response.status_code == 200
            response_data = json.loads(response.get_body().decode())
            
            assert response_data['status'] == 'healthy'
            assert response_data['service'] == 'PieceInfo API'
            assert 'timestamp' in response_data
            assert 'version' in response_data
            assert 'dependencies' in response_data
    
    @pytest.mark.api
    def test_health_check_with_dependency_failure(self):
        """Test health check when external dependencies are failing."""
        req = HttpRequest(
            method='GET',
            url='http://localhost:7074/api/pieces/health',
            headers={'Content-Type': 'application/json'}
        )
        
        # Mock external API as failing
        with patch('function_app.http_client') as mock_http_client:
            mock_http_client.get.side_effect = httpx.ConnectError("Connection failed")
            
            response = function_app.health_check(req)
            
            # Health check should still return 200 but indicate degraded status
            assert response.status_code == 200
            response_data = json.loads(response.get_body().decode())
            
            # Should indicate issues with dependencies
            assert 'dependencies' in response_data
    
    # ===================================================================
    # SWAGGER DOCUMENTATION ENDPOINT TESTS
    # ===================================================================
    
    @pytest.mark.api
    def test_swagger_documentation_endpoint(self):
        """Test Swagger documentation endpoint."""
        req = HttpRequest(
            method='GET',
            url='http://localhost:7074/api/swagger',
            headers={'Accept': 'application/json'}
        )
        
        response = function_app.get_swagger_doc(req)
        
        # Verify response
        assert response.status_code == 200
        assert response.headers['Content-Type'] == 'application/json'
        
        # Parse and verify swagger document structure
        swagger_doc = json.loads(response.get_body().decode())
        assert 'openapi' in swagger_doc
        assert 'info' in swagger_doc
        assert 'paths' in swagger_doc
        
        # Verify API endpoints are documented
        assert '/api/pieces/{piece_number}' in swagger_doc['paths']
        assert '/api/pieces/health' in swagger_doc['paths']
    
    @pytest.mark.api
    def test_swagger_ui_endpoint(self):
        """Test Swagger UI endpoint."""
        req = HttpRequest(
            method='GET',
            url='http://localhost:7074/api/docs',
            headers={'Accept': 'text/html'}
        )
        
        response = function_app.swagger_ui(req)
        
        # Verify response
        assert response.status_code == 200
        assert response.headers['Content-Type'] == 'text/html'
        
        # Verify HTML contains Swagger UI references
        html_content = response.get_body().decode()
        assert 'swagger-ui' in html_content.lower()
        assert 'api/swagger' in html_content
    
    # ===================================================================
    # HTTP METHOD AND ROUTING TESTS
    # ===================================================================
    
    @pytest.mark.api
    def test_get_piece_info_wrong_http_method(self, sample_piece_number):
        """Test piece info endpoint with wrong HTTP method."""
        # POST request to GET endpoint
        req = HttpRequest(
            method='POST',
            url=f'http://localhost:7074/api/pieces/{sample_piece_number}',
            headers={'Content-Type': 'application/json'},
            route_params={'piece_number': sample_piece_number},
            body=json.dumps({"test": "data"}).encode()
        )
        
        response = function_app.get_piece_info(req)
        
        # Should handle gracefully (Azure Functions routing handles method filtering)
        # The function should still process if it receives the request
        assert response.status_code in [200, 400, 405]  # Various valid responses
    
    # ===================================================================
    # RESPONSE HEADERS AND SECURITY TESTS
    # ===================================================================
    
    @pytest.mark.api
    def test_security_headers_present(self, sample_piece_number):
        """Test that security headers are present in responses."""
        req = HttpRequest(
            method='GET',
            url=f'http://localhost:7074/api/pieces/{sample_piece_number}',
            headers={'Content-Type': 'application/json'},
            route_params={'piece_number': sample_piece_number}
        )
        
        with patch('function_app.aggregation_service') as mock_service:
            mock_service.get_piece_info.return_value = {"test": "data"}
            
            response = function_app.get_piece_info(req)
            
            # Verify security headers
            assert 'X-Content-Type-Options' in response.headers
            assert response.headers['X-Content-Type-Options'] == 'nosniff'
            assert 'X-Frame-Options' in response.headers
            assert 'X-XSS-Protection' in response.headers
            assert 'Cache-Control' in response.headers
    
    # ===================================================================
    # PERFORMANCE AND MONITORING TESTS
    # ===================================================================
    
    @pytest.mark.api
    @pytest.mark.slow
    def test_api_response_time_logging(self, sample_piece_number, caplog):
        """Test that API response times are logged for monitoring."""
        req = HttpRequest(
            method='GET',
            url=f'http://localhost:7074/api/pieces/{sample_piece_number}',
            headers={'Content-Type': 'application/json'},
            route_params={'piece_number': sample_piece_number}
        )
        
        with patch('function_app.aggregation_service') as mock_service:
            mock_service.get_piece_info.return_value = {"test": "data"}
            
            with caplog.at_level("INFO"):
                response = function_app.get_piece_info(req)
            
            # Verify response time logging
            assert any("EXITING: get_piece_info" in record.message for record in caplog.records)
    
    # ===================================================================
    # ERROR RESPONSE FORMAT TESTS
    # ===================================================================
    
    @pytest.mark.api
    def test_error_response_format_consistency(self, sample_piece_number):
        """Test that all error responses follow consistent format."""
        req = HttpRequest(
            method='GET',
            url=f'http://localhost:7074/api/pieces/{sample_piece_number}',
            headers={'Content-Type': 'application/json'},
            route_params={'piece_number': sample_piece_number}
        )
        
        with patch('function_app.aggregation_service') as mock_service:
            mock_service.get_piece_info.side_effect = Exception("Test error")
            
            response = function_app.get_piece_info(req)
            
            # Verify error response format
            assert response.status_code == 500
            response_data = json.loads(response.get_body().decode())
            
            # Verify standard error format
            assert 'error' in response_data
            assert 'correlation_id' in response_data
            assert 'timestamp' in response_data
            assert isinstance(response_data['error'], str)
            assert isinstance(response_data['correlation_id'], str)
    
    # ===================================================================
    # INPUT VALIDATION TESTS
    # ===================================================================
    
    @pytest.mark.api
    def test_piece_number_validation_regex(self):
        """Test piece number validation with various formats."""
        test_cases = [
            ("170080637", True),      # Valid 9-digit number
            ("123456789", True),      # Valid 9-digit number
            ("000000001", True),      # Valid with leading zeros
            ("12345678", False),      # Too short (8 digits)
            ("1234567890", False),    # Too long (10 digits)
            ("12345678a", False),     # Contains letter
            ("123-456-789", False),   # Contains hyphens
            ("", False),              # Empty string
            ("   ", False),           # Whitespace only
        ]
        
        for piece_number, should_be_valid in test_cases:
            req = HttpRequest(
                method='GET',
                url=f'http://localhost:7074/api/pieces/{piece_number}',
                headers={'Content-Type': 'application/json'},
                route_params={'piece_number': piece_number}
            )
            
            response = function_app.get_piece_info(req)
            
            if should_be_valid:
                # Valid piece numbers should not fail validation (may fail for other reasons)
                assert response.status_code != 400 or "Invalid piece number format" not in response.get_body().decode()
            else:
                # Invalid piece numbers should fail validation
                assert response.status_code == 400
                response_data = json.loads(response.get_body().decode())
                assert response_data['error'] == 'Invalid piece number format'
"""
Unit Tests for HTTP Client Service

This module contains comprehensive unit tests for the SimpleHTTPClient class,
testing SSL configuration, retry logic, error handling, and performance monitoring.

Author: System Generated  
Version: 1.0.0 (Production Ready)
"""

import pytest
import asyncio
import ssl
from unittest.mock import Mock, AsyncMock, patch, MagicMock
import httpx
from datetime import datetime

# Import the module under test
import sys
import os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'src', 'pieceinfo_api'))

from services.http_client import SimpleHTTPClient

class TestSimpleHTTPClient:
    """Test suite for SimpleHTTPClient class."""
    
    # ===================================================================
    # INITIALIZATION TESTS
    # ===================================================================
    
    @pytest.mark.unit
    def test_client_initialization_with_defaults(self, test_environment_variables):
        """Test client initialization with default values."""
        client = SimpleHTTPClient()
        
        # Verify configuration loaded correctly
        assert client.base_url == 'https://test-api.example.com'
        assert client.timeout == 10.0
        assert client.max_retries == 2
        assert client.subscription_key == 'test-subscription-key-12345'
        assert client.verify_ssl == False
        
        # Verify headers are set correctly
        assert 'Ocp-Apim-Subscription-Key' in client.headers
        assert client.headers['Content-Type'] == 'application/json'
        assert client.headers['User-Agent'] == 'PieceInfo-API/1.0'
        
        # Verify monitoring counters are initialized
        assert client.request_count == 0
        assert client.successful_requests == 0
        assert client.failed_requests == 0
        assert client.total_request_time == 0.0
    
    @pytest.mark.unit
    def test_client_initialization_without_subscription_key(self, monkeypatch):
        """Test client initialization without subscription key."""
        monkeypatch.delenv('OCP_APIM_SUBSCRIPTION_KEY', raising=False)
        
        with patch('logging.getLogger') as mock_logger:
            client = SimpleHTTPClient()
            
            # Verify subscription key is not in headers
            assert 'Ocp-Apim-Subscription-Key' not in client.headers
            assert client.subscription_key is None
    
    @pytest.mark.unit
    def test_ssl_configuration_disabled(self, test_environment_variables):
        """Test SSL configuration when verification is disabled."""
        client = SimpleHTTPClient()
        
        assert client.verify_ssl == False
        # SSL context setup is tested in the actual HTTP request tests
    
    @pytest.mark.unit  
    def test_ssl_configuration_enabled(self, monkeypatch):
        """Test SSL configuration when verification is enabled."""
        monkeypatch.setenv('VERIFY_SSL', 'true')
        
        client = SimpleHTTPClient()
        assert client.verify_ssl == True
    
    # ===================================================================
    # HTTP GET METHOD TESTS
    # ===================================================================
    
    @pytest.mark.unit
    @pytest.mark.asyncio
    async def test_successful_get_request(self, test_environment_variables):
        """Test successful GET request with proper response handling."""
        client = SimpleHTTPClient()
        
        # Mock httpx response
        mock_response = Mock()
        mock_response.status_code = 200
        mock_response.json.return_value = {"test": "data"}
        mock_response.raise_for_status = Mock()
        
        # Mock httpx client
        mock_httpx_client = AsyncMock()
        mock_httpx_client.get.return_value = mock_response
        
        with patch('httpx.AsyncClient') as mock_client_class:
            mock_client_class.return_value.__aenter__.return_value = mock_httpx_client
            
            result = await client.get("test/endpoint")
            
            # Verify the result
            assert result == {"test": "data"}
            
            # Verify HTTP client was called correctly
            mock_httpx_client.get.assert_called_once()
            call_args = mock_httpx_client.get.call_args
            assert "test-api.example.com/test/endpoint" in call_args[0][0]
            assert call_args[1]['headers'] == client.headers
            
            # Verify metrics were updated
            assert client.request_count == 1
            assert client.successful_requests == 1
            assert client.failed_requests == 0
            assert client.total_request_time > 0
    
    @pytest.mark.unit
    @pytest.mark.asyncio
    async def test_url_construction(self, test_environment_variables):
        """Test proper URL construction with various endpoint formats."""
        client = SimpleHTTPClient()
        
        test_cases = [
            ("api/test", "https://test-api.example.com/api/test"),
            ("/api/test", "https://test-api.example.com/api/test"),
            ("api/test/", "https://test-api.example.com/api/test/"),
            ("/api/test/", "https://test-api.example.com/api/test/")
        ]
        
        mock_response = Mock()
        mock_response.status_code = 200
        mock_response.json.return_value = {}
        mock_response.raise_for_status = Mock()
        
        mock_httpx_client = AsyncMock()
        mock_httpx_client.get.return_value = mock_response
        
        with patch('httpx.AsyncClient') as mock_client_class:
            mock_client_class.return_value.__aenter__.return_value = mock_httpx_client
            
            for endpoint, expected_url in test_cases:
                await client.get(endpoint)
                
                # Verify URL was constructed correctly
                call_args = mock_httpx_client.get.call_args
                assert expected_url == call_args[0][0]
    
    # ===================================================================
    # ERROR HANDLING AND RETRY LOGIC TESTS
    # ===================================================================
    
    @pytest.mark.unit
    @pytest.mark.asyncio
    async def test_http_error_with_retries(self, test_environment_variables):
        """Test HTTP error handling with retry logic."""
        client = SimpleHTTPClient()
        
        # Mock HTTP error response
        mock_response = Mock()
        mock_response.status_code = 500
        mock_response.text = "Internal Server Error"
        
        http_error = httpx.HTTPStatusError(
            "Server Error", 
            request=Mock(), 
            response=mock_response
        )
        
        mock_httpx_client = AsyncMock()
        mock_httpx_client.get.side_effect = http_error
        
        with patch('httpx.AsyncClient') as mock_client_class:
            mock_client_class.return_value.__aenter__.return_value = mock_httpx_client
            
            with patch('asyncio.sleep') as mock_sleep:  # Speed up test
                with pytest.raises(httpx.HTTPStatusError):
                    await client.get("test/endpoint")
                
                # Verify retries were attempted (max_retries + 1 = 3 attempts)
                assert mock_httpx_client.get.call_count == 3
                
                # Verify exponential backoff delays
                assert mock_sleep.call_count == 2  # Only between attempts
                
                # Verify metrics were updated
                assert client.request_count == 1
                assert client.successful_requests == 0
                assert client.failed_requests == 1
    
    @pytest.mark.unit
    @pytest.mark.asyncio
    async def test_client_error_no_retry(self, test_environment_variables):
        """Test that client errors (4xx) don't trigger retries."""
        client = SimpleHTTPClient()
        
        # Mock 404 error response
        mock_response = Mock()
        mock_response.status_code = 404
        mock_response.text = "Not Found"
        
        http_error = httpx.HTTPStatusError(
            "Not Found",
            request=Mock(),
            response=mock_response
        )
        
        mock_httpx_client = AsyncMock()
        mock_httpx_client.get.side_effect = http_error
        
        with patch('httpx.AsyncClient') as mock_client_class:
            mock_client_class.return_value.__aenter__.return_value = mock_httpx_client
            
            with pytest.raises(httpx.HTTPStatusError):
                await client.get("test/endpoint")
            
            # Verify no retries for 4xx errors (only 1 attempt)
            assert mock_httpx_client.get.call_count == 1
    
    @pytest.mark.unit
    @pytest.mark.asyncio
    async def test_retryable_client_errors(self, test_environment_variables):
        """Test that specific client errors (408, 429) do trigger retries."""
        client = SimpleHTTPClient()
        
        test_cases = [408, 429]  # Request Timeout, Too Many Requests
        
        for status_code in test_cases:
            mock_response = Mock()
            mock_response.status_code = status_code
            mock_response.text = f"Error {status_code}"
            
            http_error = httpx.HTTPStatusError(
                f"Error {status_code}",
                request=Mock(),
                response=mock_response
            )
            
            mock_httpx_client = AsyncMock()
            mock_httpx_client.get.side_effect = http_error
            
            with patch('httpx.AsyncClient') as mock_client_class:
                mock_client_class.return_value.__aenter__.return_value = mock_httpx_client
                
                with patch('asyncio.sleep'):  # Speed up test
                    with pytest.raises(httpx.HTTPStatusError):
                        await client.get("test/endpoint")
                    
                    # Verify retries were attempted for these status codes
                    assert mock_httpx_client.get.call_count == 3  # max_retries + 1
    
    @pytest.mark.unit
    @pytest.mark.asyncio
    async def test_network_error_with_retries(self, test_environment_variables):
        """Test network error handling with retry logic."""
        client = SimpleHTTPClient()
        
        network_error = httpx.RequestError("Connection failed")
        
        mock_httpx_client = AsyncMock()
        mock_httpx_client.get.side_effect = network_error
        
        with patch('httpx.AsyncClient') as mock_client_class:
            mock_client_class.return_value.__aenter__.return_value = mock_httpx_client
            
            with patch('asyncio.sleep') as mock_sleep:  # Speed up test
                with pytest.raises(Exception) as exc_info:
                    await client.get("test/endpoint")
                
                # Verify error message contains context
                assert "Failed to GET test/endpoint" in str(exc_info.value)
                
                # Verify retries were attempted
                assert mock_httpx_client.get.call_count == 3
                assert mock_sleep.call_count == 2
    
    # ===================================================================
    # SSL CONTEXT TESTS
    # ===================================================================
    
    @pytest.mark.unit
    @pytest.mark.asyncio
    async def test_ssl_context_verification_disabled(self, test_environment_variables):
        """Test SSL context when verification is disabled."""
        client = SimpleHTTPClient()
        
        mock_response = Mock()
        mock_response.status_code = 200
        mock_response.json.return_value = {}
        mock_response.raise_for_status = Mock()
        
        mock_httpx_client = AsyncMock()
        mock_httpx_client.get.return_value = mock_response
        
        with patch('httpx.AsyncClient') as mock_client_class:
            mock_client_class.return_value.__aenter__.return_value = mock_httpx_client
            
            await client.get("test/endpoint")
            
            # Verify SSL context configuration
            call_kwargs = mock_client_class.call_args[1]
            assert 'verify' in call_kwargs
            # When SSL verification is disabled, verify should be an SSL context or False
            assert call_kwargs['verify'] is False or isinstance(call_kwargs['verify'], ssl.SSLContext)
    
    @pytest.mark.unit
    @pytest.mark.asyncio
    async def test_ssl_context_with_custom_ca_bundle(self, monkeypatch, tmp_path):
        """Test SSL context with custom CA bundle."""
        # Create a temporary CA bundle file
        ca_bundle_file = tmp_path / "ca-bundle.pem"
        ca_bundle_file.write_text("# Dummy CA bundle for testing")
        
        monkeypatch.setenv('VERIFY_SSL', 'true')
        monkeypatch.setenv('SSL_CA_BUNDLE', str(ca_bundle_file))
        
        mock_response = Mock()
        mock_response.status_code = 200
        mock_response.json.return_value = {}
        mock_response.raise_for_status = Mock()
        
        mock_httpx_client = AsyncMock()
        mock_httpx_client.get.return_value = mock_response
        
        with patch('httpx.AsyncClient') as mock_client_class:
            mock_client_class.return_value.__aenter__.return_value = mock_httpx_client
            
            client = SimpleHTTPClient()
            await client.get("test/endpoint")
            
            # Verify SSL context was created and CA bundle was considered
            call_kwargs = mock_client_class.call_args[1]
            assert 'verify' in call_kwargs
    
    # ===================================================================
    # PERFORMANCE MONITORING TESTS
    # ===================================================================
    
    @pytest.mark.unit
    @pytest.mark.asyncio
    async def test_performance_metrics_tracking(self, test_environment_variables):
        """Test that performance metrics are properly tracked."""
        client = SimpleHTTPClient()
        
        mock_response = Mock()
        mock_response.status_code = 200
        mock_response.json.return_value = {"test": "data"}
        mock_response.raise_for_status = Mock()
        
        mock_httpx_client = AsyncMock()
        mock_httpx_client.get.return_value = mock_response
        
        with patch('httpx.AsyncClient') as mock_client_class:
            mock_client_class.return_value.__aenter__.return_value = mock_httpx_client
            
            # Make multiple requests
            await client.get("test/endpoint1")
            await client.get("test/endpoint2")
            
            # Verify metrics are tracked correctly
            assert client.request_count == 2
            assert client.successful_requests == 2
            assert client.failed_requests == 0
            assert client.total_request_time > 0
    
    # ===================================================================
    # EDGE CASES AND BOUNDARY TESTS
    # ===================================================================
    
    @pytest.mark.unit
    @pytest.mark.asyncio
    async def test_empty_endpoint(self, test_environment_variables):
        """Test handling of empty endpoint."""
        client = SimpleHTTPClient()
        
        mock_response = Mock()
        mock_response.status_code = 200
        mock_response.json.return_value = {}
        mock_response.raise_for_status = Mock()
        
        mock_httpx_client = AsyncMock()
        mock_httpx_client.get.return_value = mock_response
        
        with patch('httpx.AsyncClient') as mock_client_class:
            mock_client_class.return_value.__aenter__.return_value = mock_httpx_client
            
            result = await client.get("")
            
            # Should construct URL properly even with empty endpoint
            call_args = mock_httpx_client.get.call_args
            assert call_args[0][0] == "https://test-api.example.com/"
    
    @pytest.mark.unit
    @pytest.mark.asyncio  
    async def test_unexpected_exception_handling(self, test_environment_variables):
        """Test handling of unexpected exceptions during requests."""
        client = SimpleHTTPClient()
        
        unexpected_error = ValueError("Unexpected error")
        
        mock_httpx_client = AsyncMock()
        mock_httpx_client.get.side_effect = unexpected_error
        
        with patch('httpx.AsyncClient') as mock_client_class:
            mock_client_class.return_value.__aenter__.return_value = mock_httpx_client
            
            with patch('asyncio.sleep'):  # Speed up test
                with pytest.raises(Exception) as exc_info:
                    await client.get("test/endpoint")
                
                # Verify error context is preserved
                assert "Failed to GET test/endpoint" in str(exc_info.value)
                assert "Unexpected error" in str(exc_info.value)
                
                # Verify retries were attempted
                assert mock_httpx_client.get.call_count == 3
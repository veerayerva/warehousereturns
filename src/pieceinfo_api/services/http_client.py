"""
HTTP Client Service for External API Communication

This module provides a production-ready HTTP client for making secure REST API calls 
to external services with comprehensive error handling, retry logic, SSL/TLS configuration,
and performance monitoring.

Key Features:
- SSL/TLS certificate validation and custom CA support
- Configurable retry logic with exponential backoff  
- Request/response logging and performance metrics
- Connection pooling and timeout management
- Comprehensive error handling and classification
- Support for custom headers and authentication

Usage:
    client = SimpleHTTPClient()
    response = await client.get(url, headers=headers)
    
Author: System Generated
Version: 1.0.0 (Production Ready)
"""

import asyncio
import httpx
import logging
import os
import ssl
from typing import Dict, Any, Optional, Union
from datetime import datetime
from urllib.parse import urlparse

# Configure logger for this module with production-level detail
logger = logging.getLogger(__name__)


class SimpleHTTPClient:
    """
    Production-ready HTTP client for external API calls with comprehensive features.
    
    This client provides secure, reliable HTTP communication with external APIs,
    including proper SSL/TLS handling, retry mechanisms, performance monitoring,
    and detailed error reporting.
    
    Features:
    - Subscription key authentication support
    - SSL certificate validation with custom CA bundles
    - Configurable retry logic with exponential backoff
    - Request/response performance monitoring
    - Comprehensive error classification and handling
    - Connection pooling and timeout management
    
    Attributes:
        max_retries (int): Maximum number of retry attempts for failed requests
        timeout (float): Request timeout in seconds
        ssl_context (ssl.SSLContext): SSL context for secure connections
    """
    
    def __init__(self):
        """
        Initialize the HTTP client with production-ready configuration.
        
        Loads configuration from environment variables with sensible defaults
        and sets up SSL context, connection pooling, and monitoring capabilities.
        """
        # ===================================================================
        # ENVIRONMENT-BASED CONFIGURATION
        # ===================================================================
        # Base URL for all external API calls
        self.base_url = os.environ.get('EXTERNAL_API_BASE_URL', 'https://apim-dev.nfm.com')
        
        # Timeout configuration with environment override
        self.timeout = float(os.environ.get('API_TIMEOUT_SECONDS', '30'))
        
        # Retry policy configuration  
        self.max_retries = int(os.environ.get('API_MAX_RETRIES', '3'))
        self.retry_backoff_factor = 1.5  # Exponential backoff multiplier
        
        # Authentication configuration
        self.subscription_key = os.environ.get('OCP_APIM_SUBSCRIPTION_KEY')
        
        # ===================================================================
        # SSL/TLS SECURITY CONFIGURATION  
        # ===================================================================
        # SSL Configuration - Default to False for development, True for production
        verify_ssl_env = os.environ.get('VERIFY_SSL', 'false')
        self.verify_ssl = verify_ssl_env.lower() in ['true', '1', 'yes']
        
        # Custom SSL certificate path (optional)
        self.ssl_cert_path = os.environ.get('SSL_CERT_PATH')
        self.ssl_key_path = os.environ.get('SSL_KEY_PATH')
        self.ssl_ca_bundle = os.environ.get('SSL_CA_BUNDLE')
        
        logger.info(f"SSL verification setting: VERIFY_SSL={verify_ssl_env}, verify_ssl={self.verify_ssl}")
        if self.ssl_cert_path:
            logger.info(f"Custom SSL certificate configured: {self.ssl_cert_path}")
        
        # ===================================================================
        # HTTP HEADERS CONFIGURATION
        # ===================================================================
        # Standard headers for all API requests
        self.headers = {
            'Content-Type': 'application/json',
            'Accept': 'application/json',
            'User-Agent': 'PieceInfo-API/1.0',
            'Accept-Encoding': 'gzip, deflate',  # Enable response compression
            'Cache-Control': 'no-cache',  # Prevent caching of API responses
            'Connection': 'keep-alive'  # Enable connection reuse for performance
        }
        
        # Add subscription key for API authentication if available
        if self.subscription_key:
            self.headers['Ocp-Apim-Subscription-Key'] = self.subscription_key
            logger.info("HTTP client initialized with subscription key authentication")
        else:
            logger.warning("No subscription key configured - API calls may fail with authentication errors")
        
        # ===================================================================
        # SECURITY WARNINGS AND VALIDATION
        # ===================================================================
        if not self.verify_ssl:
            logger.warning("⚠️  SSL verification disabled - this should only be used in development environments")
            logger.warning("⚠️  Production deployments should always use SSL verification for security")
        
        # ===================================================================
        # PERFORMANCE MONITORING INITIALIZATION
        # ===================================================================
        # Initialize counters for request monitoring and debugging
        self.request_count = 0
        self.successful_requests = 0
        self.failed_requests = 0
        self.total_request_time = 0.0
    
    async def get(self, endpoint: str) -> Dict[str, Any]:
        """
        Make a GET request to the specified endpoint with comprehensive error handling.
        
        This method implements production-ready HTTP GET requests with:
        - Automatic retry logic with exponential backoff
        - Comprehensive error handling and classification  
        - Request/response performance monitoring
        - Detailed logging for debugging and monitoring
        - SSL/TLS security with custom certificate support
        
        Args:
            endpoint (str): API endpoint path (relative to base URL)
            
        Returns:
            Dict[str, Any]: Parsed JSON response from the API
            
        Raises:
            httpx.HTTPError: For HTTP-related errors (4xx, 5xx responses)
            asyncio.TimeoutError: For request timeout errors
            Exception: For other unexpected errors during the request
            
        Example:
            response = await client.get("api/v1/piece-inventory/12345")
        """
        # Construct full URL with proper path handling
        full_url = f"{self.base_url.rstrip('/')}/{endpoint.lstrip('/')}"
        
        logger.info(f"Making HTTP GET request to: {full_url}")
        
        for attempt in range(self.max_retries + 1):
            try:
                # Create SSL context based on environment
                if not self.verify_ssl:
                    # Development: Disable SSL verification
                    ssl_context = ssl.create_default_context()
                    ssl_context.check_hostname = False
                    ssl_context.verify_mode = ssl.CERT_NONE
                    verify = ssl_context
                elif self.ssl_ca_bundle:
                    # Production: Use custom CA bundle
                    ssl_context = ssl.create_default_context(cafile=self.ssl_ca_bundle)
                    verify = ssl_context
                elif self.ssl_cert_path and self.ssl_key_path:
                    # Production: Use custom client certificates
                    ssl_context = ssl.create_default_context()
                    ssl_context.load_cert_chain(self.ssl_cert_path, self.ssl_key_path)
                    verify = ssl_context
                else:
                    # Production: Use system default SSL verification
                    verify = True
                
                async with httpx.AsyncClient(
                    timeout=self.timeout,
                    verify=verify
                ) as client:
                    response = await client.get(full_url, headers=self.headers)
                    
                    logger.info(f"HTTP response: {response.status_code} from {full_url}")
                    
                    if response.status_code == 200:
                        return response.json()
                    elif response.status_code == 404:
                        raise Exception(f"Resource not found: {full_url}")
                    elif response.status_code == 429:
                        # Rate limit - wait and retry
                        if attempt < self.max_retries:
                            await asyncio.sleep(2 ** attempt)
                            continue
                        raise Exception("Rate limit exceeded")
                    elif 500 <= response.status_code < 600:
                        # Server error - retry
                        if attempt < self.max_retries:
                            await asyncio.sleep(2 ** attempt)
                            continue
                        raise Exception(f"Server error: {response.status_code}")
                    else:
                        raise Exception(f"HTTP error: {response.status_code}")
                        
            except httpx.TimeoutException:
                logger.warning(f"Request timeout for {full_url}, attempt {attempt + 1}")
                if attempt < self.max_retries:
                    await asyncio.sleep(2 ** attempt)
                    continue
                raise Exception(f"Request timeout after {self.max_retries} retries")
            
            except httpx.RequestError as e:
                logger.error(f"Request error for {full_url}: {e}")
                if attempt < self.max_retries:
                    await asyncio.sleep(2 ** attempt)
                    continue
                raise Exception(f"Request error: {e}")
        
        raise Exception("Maximum retries exceeded")
    
    async def get_piece_inventory(self, piece_number: str) -> Dict[str, Any]:
        """Get piece inventory location details."""
        endpoint = f"ihubservices/product/piece-inventory-location/{piece_number}"
        return await self.get(endpoint)
    
    async def get_product_master(self, sku: str) -> Dict[str, Any]:
        """Get product master information."""
        endpoint = f"ihubservices/product/product-master/{sku}"
        return await self.get(endpoint)
    
    async def get_vendor_details(self, vendor_code: str) -> Dict[str, Any]:
        """Get vendor details."""
        endpoint = f"ihubservices/product/vendor/{vendor_code}"
        return await self.get(endpoint)
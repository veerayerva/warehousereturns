"""
PieceInfo Aggregation Service - Production Ready

This service orchestrates data collection from three external APIs to provide
comprehensive piece information. It handles the complex business logic of:
- Fetching piece inventory location data
- Retrieving product master information using extracted SKU
- Getting vendor details using extracted vendor code  
- Aggregating all data into a unified response structure

The service implements proper error handling, logging, and data validation
to ensure reliable operation in production environments.

Author: Warehouse Returns Team
Version: 1.0.0
"""

import asyncio
import logging
from typing import Dict, Any, Optional
from datetime import datetime
from .http_client import SimpleHTTPClient

# Configure logger for this module
logger = logging.getLogger(__name__)


class SimpleAggregationService:
    """
    Production-ready service for aggregating piece information from multiple external APIs.
    
    This service follows a sequential data collection pattern:
    1. Fetch piece inventory data using the piece number
    2. Extract SKU and vendor code from inventory data  
    3. Fetch product master data using the SKU
    4. Fetch vendor details using the vendor code
    5. Aggregate all data into a unified response structure
    
    The service handles errors gracefully and provides detailed logging for
    monitoring and troubleshooting purposes.
    """
    
    def __init__(self):
        """
        Initialize the aggregation service with HTTP client.
        
        The HTTP client is configured with SSL settings, retry logic,
        and subscription key authentication based on environment variables.
        """
        try:
            self.http_client = SimpleHTTPClient()
            logger.info("SimpleAggregationService initialized successfully")
        except Exception as e:
            logger.error(f"Failed to initialize SimpleAggregationService: {e}")
            raise
    
    async def get_aggregated_piece_info(self, piece_number: str) -> Dict[str, Any]:
        """
        Get comprehensive aggregated piece information by orchestrating multiple API calls.
        
        This method implements the core business logic for piece information aggregation:
        1. Validates input parameters
        2. Fetches piece inventory data (warehouse location, serial numbers)
        3. Extracts SKU and vendor code for subsequent API calls
        4. Fetches product master data (descriptions, models, categories)
        5. Fetches vendor details (contact info, addresses, policies)
        6. Combines all data into a standardized response format
        
        Args:
            piece_number: The unique piece inventory identifier (3-50 characters)
            
        Returns:
            Dict containing aggregated piece information with the following structure:
            - piece_inventory_key: Original piece number
            - sku: Stock keeping unit identifier  
            - vendor_code: Vendor identifier code
            - warehouse_location: Physical warehouse location
            - rack_location: Specific rack/shelf location
            - serial_number: Device serial number
            - description: Product description
            - vendor_name: Vendor company name
            - vendor_address: Complete vendor address
            - vendor_contact: Vendor contact information
            - vendor_policies: Vendor return and serial number policies
            
        Raises:
            Exception: When piece number not found, API calls fail, or required data missing
        """
        start_time = datetime.utcnow()
        logger.info(f"Starting aggregation process for piece: {piece_number}")
        
        # Input validation
        if not piece_number or not isinstance(piece_number, str):
            raise ValueError("piece_number must be a non-empty string")
        
        piece_number = piece_number.strip()
        if len(piece_number) < 3:
            raise ValueError("piece_number must be at least 3 characters long")
        
        try:
            # ===================================================================
            # STEP 1: Get piece inventory location data
            # ===================================================================
            logger.info(f"Step 1/3: Fetching piece inventory data for {piece_number}")
            piece_inventory = await self.http_client.get_piece_inventory(piece_number)
            
            # Validate piece inventory response
            if not isinstance(piece_inventory, dict):
                raise ValueError("Invalid piece inventory response format")
            
            # Extract critical fields required for subsequent API calls
            sku = piece_inventory.get('sku')
            vendor_code = piece_inventory.get('vendor')
            
            # Validate extracted data
            if not sku:
                logger.error(f"SKU not found in piece inventory data for {piece_number}")
                raise ValueError("SKU not found in piece inventory data - cannot proceed with product lookup")
            
            if not vendor_code:
                logger.error(f"Vendor code not found in piece inventory data for {piece_number}")  
                raise ValueError("Vendor code not found in piece inventory data - cannot proceed with vendor lookup")
            
            logger.info(f"Successfully extracted SKU: {sku}, Vendor: {vendor_code}")
            
            # ===================================================================
            # STEP 2: Get product master data using extracted SKU
            # ===================================================================
            logger.info(f"Step 2/3: Fetching product master data for SKU: {sku}")
            product_master = await self.http_client.get_product_master(sku)
            
            # Validate product master response
            if not isinstance(product_master, dict):
                logger.warning(f"Invalid product master response format for SKU: {sku}")
                product_master = {}  # Continue with empty data rather than failing
            
            # ===================================================================  
            # STEP 3: Get vendor details using extracted vendor code
            # ===================================================================
            logger.info(f"Step 3/3: Fetching vendor details for vendor: {vendor_code}")
            vendor_details = await self.http_client.get_vendor_details(vendor_code)
            
            # Validate vendor details response
            if not isinstance(vendor_details, dict):
                logger.warning(f"Invalid vendor details response format for vendor: {vendor_code}")
                vendor_details = {}  # Continue with empty data rather than failing
            
            # ===================================================================
            # STEP 4: Aggregate and structure the response data
            # ===================================================================
            logger.info(f"Step 4/4: Aggregating data from all sources for piece: {piece_number}")
            
            # Build the aggregated response with data from all three APIs
            # Using .get() with defaults to handle missing or null values gracefully
            aggregated_data = {
                # =============== PIECE INVENTORY DATA ===============
                "piece_inventory_key": piece_inventory.get('pieceInventoryKey', piece_number),
                "sku": sku,  # Already validated as required
                "vendor_code": vendor_code,  # Already validated as required  
                "warehouse_location": piece_inventory.get('warehouseLocation', ''),
                "rack_location": piece_inventory.get('rackLocation', ''),
                "serial_number": piece_inventory.get('serialNumber', ''),
                "family": piece_inventory.get('family', ''),
                "purchase_reference_number": piece_inventory.get('purchaseReferenceNumber', ''),
                
                # =============== PRODUCT MASTER DATA ===============
                "description": product_master.get('description', ''),
                "model_no": product_master.get('modelNo', ''),
                "brand": product_master.get('brand', ''),
                "category": product_master.get('category', ''),
                "group": product_master.get('group', ''),
                
                # =============== VENDOR DETAILS DATA ===============
                "vendor_name": vendor_details.get('name', ''),
                
                # Vendor address as nested object with all fields defaulted
                "vendor_address": {
                    "address_line1": vendor_details.get('addressLine1', ''),
                    "address_line2": vendor_details.get('addressLine2', ''),
                    "city": vendor_details.get('city', ''),
                    "state": vendor_details.get('state', ''),
                    "zip_code": vendor_details.get('zipCode', '')
                },
                
                # Vendor contact information with email fallbacks
                "vendor_contact": {
                    "rep_name": vendor_details.get('repName', ''),
                    "primary_rep_email": vendor_details.get('primaryRepEmail', ''),
                    "secondary_rep_email": vendor_details.get('secondaryRepEmail', ''),
                    "exec_email": vendor_details.get('execEmail', None)  # Explicitly None for missing exec email
                },
                
                # Vendor policies with proper boolean conversion
                "vendor_policies": {
                    "serial_number_required": self._convert_to_boolean(vendor_details.get('serialNumberRequired', 'false')),
                    "vendor_return": self._convert_to_boolean(vendor_details.get('vendorReturn', 'false'))
                }
            }
            
            # Calculate processing time for monitoring
            end_time = datetime.utcnow()
            processing_time = (end_time - start_time).total_seconds()
            
            logger.info(f"Successfully aggregated piece info for: {piece_number} in {processing_time:.2f} seconds")
            return aggregated_data
            
        except ValueError as ve:
            # Re-raise validation errors with context
            logger.error(f"Validation error for piece {piece_number}: {ve}")
            raise
            
        except Exception as e:
            # Log detailed error information for troubleshooting
            end_time = datetime.utcnow()
            processing_time = (end_time - start_time).total_seconds()
            
            logger.error(f"Failed to aggregate piece info for {piece_number} after {processing_time:.2f} seconds: {e}", 
                        exc_info=True)
            
            # Re-raise with more context for the caller
            raise Exception(f"Aggregation failed for piece {piece_number}: {str(e)}")
    
    def _convert_to_boolean(self, value: Any) -> bool:
        """
        Convert various string/value representations to boolean.
        
        Handles common boolean representations found in API responses:
        - 'true', 'True', 'TRUE' -> True
        - 'false', 'False', 'FALSE' -> False  
        - '1', 1 -> True
        - '0', 0 -> False
        - None, empty string -> False
        
        Args:
            value: The value to convert to boolean
            
        Returns:
            Boolean representation of the input value
        """
        if value is None:
            return False
            
        if isinstance(value, bool):
            return value
            
        if isinstance(value, (int, float)):
            return bool(value)
            
        if isinstance(value, str):
            return value.lower().strip() in ('true', '1', 'yes', 'on')
            
        return bool(value)
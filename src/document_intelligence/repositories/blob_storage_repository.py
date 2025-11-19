"""
Blob Storage Repository

Manages storage of low-confidence documents to Azure Blob Storage for manual review,
reprocessing, and model training. Provides organized storage with metadata tracking.
"""

import os
import json
import asyncio
from typing import Optional, Dict, Any, Tuple, BinaryIO
from datetime import datetime, timedelta
from azure.storage.blob.aio import BlobServiceClient, ContainerClient
from azure.storage.blob import BlobProperties
from azure.core.exceptions import AzureError, ResourceNotFoundError

# Simple logging setup
import logging

# Import models
from models import (
    ErrorResponse,
    ErrorCode,
    AnalysisStatus,
    FieldExtractionStatus
)


class BlobStorageRepository:
    """
    Repository for managing document storage in Azure Blob Storage.
    
    Handles storage of low-confidence documents for manual review and retraining,
    with organized container structure and comprehensive metadata tracking.
    
    Container Structure:
    - low-confidence/
      - pending-review/
        - {date}/
          - {analysis_id}/
            - document.{ext}
            - metadata.json
      - reviewed/
        - {date}/
          - {analysis_id}/
            - document.{ext} 
            - metadata.json
            - review_results.json
      - retraining/
        - {date}/
          - {analysis_id}/
            - document.{ext}
            - metadata.json
            - training_annotations.json
    
    Attributes:
        connection_string (str): Azure Storage connection string
        container_name (str): Primary container for document storage
        blob_service_client (BlobServiceClient): Azure Blob Service client
        logger: Structured logger instance
        max_retry_attempts (int): Maximum retry attempts for storage operations
        retry_delay_seconds (int): Base delay between retry attempts
    """

    def __init__(
        self,
        connection_string: Optional[str] = None,
        container_name: str = "document-intelligence",
        max_retry_attempts: int = 3,
        retry_delay_seconds: int = 2
    ):
        """
        Initialize the Blob Storage repository.
        
        Args:
            connection_string (Optional[str]): Azure Storage connection string
            container_name (str): Name of the primary container
            max_retry_attempts (int): Maximum retry attempts for operations
            retry_delay_seconds (int): Base delay between retries
            
        Raises:
            ValueError: If connection string is not provided and not in environment
        """
        # Get logger for this repository
        self.logger = logging.getLogger(f'{__name__}.BlobStorageRepository')
        
        # Get configuration from environment or parameters
        self.connection_string = connection_string or os.getenv('AZURE_STORAGE_CONNECTION_STRING')
        self.container_name = container_name
        
        # Validate required configuration
        if not self.connection_string:
            error_msg = "Azure Storage connection string is required. Set AZURE_STORAGE_CONNECTION_STRING environment variable."
            self.logger.error("Missing Azure Storage connection string configuration")
            raise ValueError(error_msg)
        
        # Initialize Azure Blob Storage client
        try:
            self.blob_service_client = BlobServiceClient.from_connection_string(
                self.connection_string
            )
            self.logger.info(
                "Blob Storage repository initialized successfully",
                container_name=self.container_name
            )
        except Exception as e:
            self.logger.error(
                "Failed to initialize Blob Storage client",
                container_name=self.container_name,
                exception=e
            )
            raise
        
        # Repository configuration
        self.max_retry_attempts = max_retry_attempts
        self.retry_delay_seconds = retry_delay_seconds

    async def store_low_confidence_document(
        self,
        analysis_id: str,
        document_data: bytes,
        filename: str,
        content_type: str,
        analysis_metadata: Dict[str, Any],
        correlation_id: Optional[str] = None
    ) -> Tuple[Optional[Dict[str, str]], Optional[ErrorResponse]]:
        """
        Store a low-confidence document for manual review and retraining.
        
        Args:
            analysis_id (str): Unique analysis identifier
            document_data (bytes): Document file content
            filename (str): Original document filename
            content_type (str): MIME type of the document
            analysis_metadata (Dict[str, Any]): Analysis results and metadata
            correlation_id (Optional[str]): Correlation ID for tracing
            
        Returns:
            Tuple[Optional[Dict[str, str]], Optional[ErrorResponse]]:
                Storage information dict and error (if any)
        """
        self.logger.info(
            "Storing low-confidence document for review",
            analysis_id=analysis_id,
            filename=filename,
            content_type=content_type,
            file_size_bytes=len(document_data),
            correlation_id=correlation_id
        )
        
        try:
            # Ensure container exists
            await self._ensure_container_exists()
            
            # Generate storage paths
            date_prefix = datetime.utcnow().strftime("%Y/%m/%d")
            base_path = f"low-confidence/pending-review/{date_prefix}/{analysis_id}"
            
            # Extract file extension from filename
            file_extension = os.path.splitext(filename)[1] if '.' in filename else ''
            if not file_extension and content_type:
                # Infer extension from content type
                extension_map = {
                    'image/jpeg': '.jpg',
                    'image/png': '.png', 
                    'image/tiff': '.tiff',
                    'application/pdf': '.pdf'
                }
                file_extension = extension_map.get(content_type, '.bin')
            
            document_blob_path = f"{base_path}/document{file_extension}"
            metadata_blob_path = f"{base_path}/metadata.json"
            
            # Prepare metadata
            storage_metadata = {
                "analysis_id": analysis_id,
                "original_filename": filename,
                "content_type": content_type,
                "file_size_bytes": len(document_data),
                "stored_at": datetime.utcnow().isoformat(),
                "correlation_id": correlation_id,
                "status": "pending_review",
                "analysis_results": analysis_metadata,
                "storage_paths": {
                    "document": document_blob_path,
                    "metadata": metadata_blob_path
                }
            }
            
            # Store with retry logic
            for attempt in range(1, self.max_retry_attempts + 1):
                try:
                    # Get container client
                    container_client = self.blob_service_client.get_container_client(
                        self.container_name
                    )
                    
                    # Upload document file
                    await container_client.upload_blob(
                        name=document_blob_path,
                        data=document_data,
                        content_type=content_type,
                        metadata={
                            "analysis_id": analysis_id,
                            "original_filename": filename,
                            "correlation_id": correlation_id or "",
                            "stored_at": datetime.utcnow().isoformat()
                        },
                        overwrite=True
                    )
                    
                    # Upload metadata file  
                    metadata_json = json.dumps(storage_metadata, indent=2, default=str)
                    await container_client.upload_blob(
                        name=metadata_blob_path,
                        data=metadata_json.encode('utf-8'),
                        content_type='application/json',
                        metadata={
                            "analysis_id": analysis_id,
                            "type": "metadata",
                            "correlation_id": correlation_id or ""
                        },
                        overwrite=True
                    )
                    
                    self.logger.info(
                        "Low-confidence document stored successfully",
                        analysis_id=analysis_id,
                        document_path=document_blob_path,
                        metadata_path=metadata_blob_path,
                        correlation_id=correlation_id
                    )
                    
                    # Return storage information
                    storage_info = {
                        "container_name": self.container_name,
                        "document_blob_path": document_blob_path,
                        "metadata_blob_path": metadata_blob_path,
                        "storage_url": f"https://{self._get_storage_account_name()}.blob.core.windows.net/{self.container_name}/{document_blob_path}",
                        "stored_at": storage_metadata["stored_at"]
                    }
                    
                    return storage_info, None
                    
                except AzureError as e:
                    if attempt < self.max_retry_attempts:
                        delay = self.retry_delay_seconds * (2 ** (attempt - 1))
                        self.logger.warning(
                            f"Blob storage error, retrying in {delay} seconds",
                            attempt=attempt,
                            analysis_id=analysis_id,
                            error_message=str(e),
                            correlation_id=correlation_id
                        )
                        await asyncio.sleep(delay)
                        continue
                    
                    # Max retries exceeded
                    self.logger.error(
                        "Blob storage failed after maximum retries",
                        analysis_id=analysis_id,
                        max_attempts=self.max_retry_attempts,
                        error_message=str(e),
                        correlation_id=correlation_id
                    )
                    
                    error_response = ErrorResponse(
                        error_code=ErrorCode.BLOB_STORAGE_ERROR,
                        message="Failed to store document for review",
                        details=str(e),
                        correlation_id=correlation_id,
                        suggested_action="Please retry the request or contact support"
                    )
                    return None, error_response
            
            # This should not be reached
            error_response = ErrorResponse(
                error_code=ErrorCode.BLOB_STORAGE_ERROR,
                message="Document storage failed after all retry attempts",
                correlation_id=correlation_id
            )
            return None, error_response
            
        except Exception as e:
            self.logger.error(
                "Unexpected error during document storage",
                analysis_id=analysis_id,
                exception=e,
                correlation_id=correlation_id
            )
            
            error_response = ErrorResponse(
                error_code=ErrorCode.INTERNAL_ERROR,
                message="Unexpected error during document storage",
                details=str(e),
                correlation_id=correlation_id
            )
            return None, error_response

    async def retrieve_document_metadata(
        self,
        analysis_id: str,
        correlation_id: Optional[str] = None
    ) -> Tuple[Optional[Dict[str, Any]], Optional[ErrorResponse]]:
        """
        Retrieve metadata for a stored document by analysis ID.
        
        Args:
            analysis_id (str): Analysis identifier to search for
            correlation_id (Optional[str]): Correlation ID for tracing
            
        Returns:
            Tuple[Optional[Dict[str, Any]], Optional[ErrorResponse]]:
                Document metadata dict and error (if any)
        """
        self.logger.info(
            "Retrieving document metadata",
            analysis_id=analysis_id,
            correlation_id=correlation_id
        )
        
        try:
            # Search in different storage paths (pending-review, reviewed, retraining)
            search_paths = [
                "low-confidence/pending-review",
                "low-confidence/reviewed", 
                "low-confidence/retraining"
            ]
            
            container_client = self.blob_service_client.get_container_client(
                self.container_name
            )
            
            for search_path in search_paths:
                try:
                    # List blobs with analysis_id prefix
                    blobs = container_client.list_blobs(
                        name_starts_with=f"{search_path}/",
                        include=['metadata']
                    )
                    
                    async for blob in blobs:
                        if (analysis_id in blob.name and 
                            blob.name.endswith('metadata.json')):
                            
                            # Download and parse metadata
                            blob_client = container_client.get_blob_client(blob.name)
                            metadata_content = await blob_client.download_blob()
                            metadata_text = await metadata_content.readall()
                            metadata = json.loads(metadata_text.decode('utf-8'))
                            
                            if metadata.get('analysis_id') == analysis_id:
                                self.logger.info(
                                    "Document metadata found",
                                    analysis_id=analysis_id,
                                    blob_path=blob.name,
                                    correlation_id=correlation_id
                                )
                                return metadata, None
                                
                except ResourceNotFoundError:
                    continue  # Search in next path
                except Exception as e:
                    self.logger.warning(
                        f"Error searching in path {search_path}",
                        analysis_id=analysis_id,
                        error_message=str(e),
                        correlation_id=correlation_id
                    )
                    continue
            
            # Document not found in any path
            self.logger.warning(
                "Document metadata not found",
                analysis_id=analysis_id,
                correlation_id=correlation_id
            )
            
            error_response = ErrorResponse(
                error_code=ErrorCode.FIELD_NOT_FOUND,
                message=f"Document metadata not found for analysis ID: {analysis_id}",
                correlation_id=correlation_id
            )
            return None, error_response
            
        except Exception as e:
            self.logger.error(
                "Error retrieving document metadata", 
                analysis_id=analysis_id,
                exception=e,
                correlation_id=correlation_id
            )
            
            error_response = ErrorResponse(
                error_code=ErrorCode.BLOB_STORAGE_ERROR,
                message="Error retrieving document metadata",
                details=str(e),
                correlation_id=correlation_id
            )
            return None, error_response

    async def list_pending_review_documents(
        self,
        days_back: int = 30,
        correlation_id: Optional[str] = None
    ) -> Tuple[Optional[list], Optional[ErrorResponse]]:
        """
        List documents pending manual review.
        
        Args:
            days_back (int): Number of days to look back for documents
            correlation_id (Optional[str]): Correlation ID for tracing
            
        Returns:
            Tuple[Optional[list], Optional[ErrorResponse]]:
                List of pending documents and error (if any)
        """
        self.logger.info(
            "Listing documents pending review",
            days_back=days_back,
            correlation_id=correlation_id
        )
        
        try:
            container_client = self.blob_service_client.get_container_client(
                self.container_name
            )
            
            pending_documents = []
            
            # Calculate date range to search
            end_date = datetime.utcnow()
            start_date = end_date - timedelta(days=days_back)
            
            # List blobs in pending-review path
            blobs = container_client.list_blobs(
                name_starts_with="low-confidence/pending-review/",
                include=['metadata']
            )
            
            async for blob in blobs:
                if blob.name.endswith('metadata.json'):
                    try:
                        # Download and parse metadata
                        blob_client = container_client.get_blob_client(blob.name)
                        metadata_content = await blob_client.download_blob()
                        metadata_text = await metadata_content.readall()
                        metadata = json.loads(metadata_text.decode('utf-8'))
                        
                        # Check if within date range
                        stored_at = datetime.fromisoformat(
                            metadata.get('stored_at', '').replace('Z', '+00:00')
                        )
                        
                        if start_date <= stored_at <= end_date:
                            pending_documents.append({
                                "analysis_id": metadata.get('analysis_id'),
                                "original_filename": metadata.get('original_filename'),
                                "stored_at": metadata.get('stored_at'),
                                "file_size_bytes": metadata.get('file_size_bytes'),
                                "confidence_score": metadata.get('analysis_results', {}).get('serial_field', {}).get('confidence', 0.0),
                                "blob_path": blob.name
                            })
                            
                    except Exception as e:
                        self.logger.warning(
                            "Error processing metadata blob",
                            blob_name=blob.name,
                            error_message=str(e),
                            correlation_id=correlation_id
                        )
                        continue
            
            self.logger.info(
                "Pending review documents listed",
                count=len(pending_documents),
                days_back=days_back,
                correlation_id=correlation_id
            )
            
            return pending_documents, None
            
        except Exception as e:
            self.logger.error(
                "Error listing pending review documents",
                exception=e,
                correlation_id=correlation_id
            )
            
            error_response = ErrorResponse(
                error_code=ErrorCode.BLOB_STORAGE_ERROR,
                message="Error listing pending review documents",
                details=str(e),
                correlation_id=correlation_id
            )
            return None, error_response

    async def _ensure_container_exists(self):
        """
        Ensure the storage container exists, create if it doesn't.
        
        Raises:
            AzureError: If container creation fails
        """
        try:
            container_client = self.blob_service_client.get_container_client(
                self.container_name
            )
            
            # Check if container exists, create if not
            try:
                await container_client.get_container_properties()
                self.logger.debug(f"Container '{self.container_name}' exists")
            except ResourceNotFoundError:
                await container_client.create_container()
                self.logger.info(f"Container '{self.container_name}' created")
                
        except AzureError as e:
            self.logger.error(
                "Error ensuring container exists",
                container_name=self.container_name,
                exception=e
            )
            raise

    def _get_storage_account_name(self) -> str:
        """
        Extract storage account name from connection string.
        
        Returns:
            str: Storage account name
        """
        try:
            # Parse connection string to extract account name
            parts = dict(part.split('=', 1) for part in self.connection_string.split(';') if '=' in part)
            return parts.get('AccountName', 'unknown')
        except Exception:
            return 'unknown'

    async def health_check(self) -> Dict[str, Any]:
        """
        Perform health check on Blob Storage connectivity.
        
        Returns:
            Dict[str, Any]: Health check results
        """
        try:
            # Test container accessibility
            container_client = self.blob_service_client.get_container_client(
                self.container_name
            )
            
            # Simple connectivity test
            properties = await container_client.get_container_properties()
            
            health_status = {
                "service": "blob_storage",
                "status": "healthy",
                "container_name": self.container_name,
                "timestamp": datetime.utcnow().isoformat(),
                "container_exists": True,
                "last_modified": properties.last_modified.isoformat() if properties.last_modified else None
            }
            
            self.logger.info("Blob Storage health check completed", status="healthy")
            return health_status
            
        except Exception as e:
            self.logger.error("Blob Storage health check failed", exception=e)
            return {
                "service": "blob_storage",
                "status": "unhealthy",
                "error": str(e),
                "timestamp": datetime.utcnow().isoformat(),
                "container_name": self.container_name
            }
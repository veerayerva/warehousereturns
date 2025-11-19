"""
Blob Storage Repository

Manages storage of low-confidence documents to Azure Blob Storage for manual review,
reprocessing, and model training. Provides organized storage with metadata tracking.
"""

import os
import json
import time
from typing import Optional, Dict, Any, Tuple, BinaryIO
from datetime import datetime, timedelta
from azure.storage.blob import BlobServiceClient, ContainerClient, BlobProperties
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
        
        # Debug environment variables
        all_env_vars = list(os.environ.keys())
        azure_env_vars = [key for key in all_env_vars if 'AZURE' in key or 'STORAGE' in key or 'BLOB' in key]
        
        self.logger.info(
            f"[BLOB-REPO-CONFIG] Configuration loaded - "
            f"Container: {self.container_name}, "
            f"Connection-String-Length: {len(self.connection_string) if self.connection_string else 0}, "
            f"Connection-String-From: {'parameter' if connection_string else 'environment'}, "
            f"Total-Env-Vars: {len(all_env_vars)}, "
            f"Azure-Related-Env-Vars: {azure_env_vars[:10]}..."  # Show first 10
        )
        
        # Validate required configuration
        if not self.connection_string:
            error_msg = "Azure Storage connection string is required. Set AZURE_STORAGE_CONNECTION_STRING environment variable."
            self.logger.error(
                f"[BLOB-REPO-CONFIG] Missing Azure Storage connection string - "
                f"Env-Var-Set: {bool(os.getenv('AZURE_STORAGE_CONNECTION_STRING'))}, "
                f"Available-Azure-Vars: {azure_env_vars}"
            )
            raise ValueError(error_msg)
        
        # Initialize Azure Blob Storage client
        try:
            self.blob_service_client = BlobServiceClient.from_connection_string(
                self.connection_string
            )
            self.logger.info(
                f"[BLOB-REPO-INIT] Blob Storage repository initialized successfully - "
                f"Container: {self.container_name}, "
                f"Storage-Account: {self._get_storage_account_name()}, "
                f"Connection-String-Length: {len(self.connection_string) if self.connection_string else 0}"
            )
        except Exception as e:
            self.logger.error(
                f"[BLOB-REPO-INIT] Failed to initialize Blob Storage client - "
                f"Container: {self.container_name}, "
                f"Exception: {str(e)}, "
                f"Exception-Type: {type(e).__name__}, "
                f"Connection-String-Set: {bool(self.connection_string)}"
            )
            raise
        
        # Repository configuration
        self.max_retry_attempts = max_retry_attempts
        self.retry_delay_seconds = retry_delay_seconds

    def store_low_confidence_document(
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
        
        This method implements the core document storage workflow for continuous improvement:
        1. Creates organized storage structure with date-based hierarchy
        2. Uploads original document with proper content type and metadata
        3. Stores comprehensive analysis metadata as JSON for review workflows
        4. Implements retry logic with exponential backoff for resilience
        5. Returns storage URLs and paths for tracking and retrieval
        
        The storage structure enables efficient organization and retrieval:
        - Documents are stored by date for temporal organization
        - Each analysis gets a unique folder with document + metadata
        - Metadata includes original analysis results for comparison after review
        - Storage paths support migration through review workflow stages
        
        Args:
            analysis_id (str): 
                Unique identifier for this document analysis session.
                Used as the primary key for storage organization and retrieval.
                Format: Typically UUID-based for global uniqueness.
                
            document_data (bytes): 
                Raw binary content of the original document.
                Stored as-is to preserve original document for manual review.
                Size should be validated before calling this method.
                
            filename (str): 
                Original filename from the document upload.
                Used for file extension detection and metadata tracking.
                Preserved for human-readable storage organization.
                
            content_type (str): 
                MIME type of the document (e.g., 'application/pdf').
                Used for proper blob storage content-type headers.
                Enables browsers to handle downloads correctly.
                
            analysis_metadata (Dict[str, Any]): 
                Complete analysis results and processing metadata including:
                - Extracted field values and confidence scores
                - Model information and API version used
                - Processing timestamps and performance metrics
                - Business context and correlation information
                
            correlation_id (Optional[str]): 
                Request correlation identifier for distributed tracing.
                Links storage operations to original processing request.
                Used for debugging and audit trail purposes.
                
        Returns:
            Tuple[Optional[Dict[str, str]], Optional[ErrorResponse]]:
                Success case: (storage_info_dict, None)
                - storage_info contains URLs, paths, timestamps for tracking
                - Includes both document and metadata storage locations
                
                Failure case: (None, error_response)
                - error_response contains categorized error with retry guidance
                - Includes correlation_id for troubleshooting support
                
        Storage Structure Created:
            container/low-confidence/pending-review/YYYY/MM/DD/analysis_id/
            ├── document.{ext}    # Original document file
            └── metadata.json     # Analysis results and processing metadata
                
        Example:
            >>> storage_info, error = await repo.store_low_confidence_document(
            ...     analysis_id="analysis-12345",
            ...     document_data=pdf_bytes,
            ...     filename="return_doc.pdf", 
            ...     content_type="application/pdf",
            ...     analysis_metadata={"serial_field": {"value": "SN123", "confidence": 0.65}},
            ...     correlation_id="req-abc123"
            ... )
            >>> if storage_info:
            ...     print(f"Stored at: {storage_info['storage_url']}")
        """
        self.logger.info(
            f"[BLOB-REPO-STORE] Starting low-confidence document storage - "
            f"Analysis-ID: {analysis_id}, "
            f"Filename: {filename}, "
            f"Content-Type: {content_type}, "
            f"File-Size: {len(document_data)} bytes, "
            f"Container: {self.container_name}, "
            f"Max-Retry-Attempts: {self.max_retry_attempts}, "
            f"Correlation-ID: {correlation_id}"
        )
        
        try:
            # Ensure container exists
            self.logger.info(
                f"[BLOB-REPO-STORE] Ensuring container exists - "
                f"Container: {self.container_name}, "
                f"Analysis-ID: {analysis_id}"
            )
            self._ensure_container_exists()
            
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
            
            self.logger.info(
                f"[BLOB-REPO-STORE] Generated storage paths - "
                f"Analysis-ID: {analysis_id}, "
                f"Document-Path: {document_blob_path}, "
                f"Metadata-Path: {metadata_blob_path}, "
                f"File-Extension: {file_extension}"
            )
            
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
                self.logger.info(
                    f"[BLOB-REPO-STORE] Starting upload attempt - "
                    f"Analysis-ID: {analysis_id}, "
                    f"Attempt: {attempt}/{self.max_retry_attempts}"
                )
                
                try:
                    # Get container client
                    container_client = self.blob_service_client.get_container_client(
                        self.container_name
                    )
                    
                    self.logger.info(
                        f"[BLOB-REPO-STORE] Uploading document file - "
                        f"Analysis-ID: {analysis_id}, "
                        f"Document-Path: {document_blob_path}, "
                        f"File-Size: {len(document_data)} bytes"
                    )
                    
                    # Upload document file
                    container_client.upload_blob(
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
                    
                    self.logger.info(
                        f"[BLOB-REPO-STORE] Uploading metadata file - "
                        f"Analysis-ID: {analysis_id}, "
                        f"Metadata-Path: {metadata_blob_path}"
                    )
                    
                    # Upload metadata file  
                    metadata_json = json.dumps(storage_metadata, indent=2, default=str)
                    container_client.upload_blob(
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
                        f"[BLOB-REPO-STORE] Low-confidence document stored successfully - "
                        f"Analysis-ID: {analysis_id}, "
                        f"Document-Path: {document_blob_path}, "
                        f"Metadata-Path: {metadata_blob_path}, "
                        f"Attempt: {attempt}, "
                        f"Correlation-ID: {correlation_id}"
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
                            f"[BLOB-REPO-STORE] Azure storage error, retrying - "
                            f"Attempt: {attempt}/{self.max_retry_attempts}, "
                            f"Retry-Delay: {delay}s, "
                            f"Analysis-ID: {analysis_id}, "
                            f"Error: {str(e)}, "
                            f"Error-Type: {type(e).__name__}, "
                            f"Correlation-ID: {correlation_id}"
                        )
                        time.sleep(delay)
                        continue
                    
                    # Max retries exceeded
                    self.logger.error(
                        f"[BLOB-REPO-STORE] Blob storage failed after maximum retries - "
                        f"Analysis-ID: {analysis_id}, "
                        f"Max-Attempts: {self.max_retry_attempts}, "
                        f"Error: {str(e)}, "
                        f"Error-Type: {type(e).__name__}, "
                        f"Correlation-ID: {correlation_id}"
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
                f"Unexpected error during document storage - "
                f"Analysis-ID: {analysis_id}, "
                f"Exception: {e}, "
                f"Correlation-ID: {correlation_id}"
            )
            
            error_response = ErrorResponse(
                error_code=ErrorCode.INTERNAL_ERROR,
                message="Unexpected error during document storage",
                details=str(e),
                correlation_id=correlation_id
            )
            return None, error_response

    def retrieve_document_metadata(
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
                    
                    for blob in blobs:
                        if (analysis_id in blob.name and 
                            blob.name.endswith('metadata.json')):
                            
                            # Download and parse metadata
                            blob_client = container_client.get_blob_client(blob.name)
                            metadata_content = blob_client.download_blob()
                            metadata_text = metadata_content.readall()
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

    def list_pending_review_documents(
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
            
            for blob in blobs:
                if blob.name.endswith('metadata.json'):
                    try:
                        # Download and parse metadata
                        blob_client = container_client.get_blob_client(blob.name)
                        metadata_content = blob_client.download_blob()
                        metadata_text = metadata_content.readall()
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

    def _ensure_container_exists(self):
        """
        Ensure the storage container exists, create if it doesn't.
        
        Raises:
            AzureError: If container creation fails
        """
        try:
            container_client = self.blob_service_client.get_container_client(
                self.container_name
            )
            
            self.logger.info(
                f"[BLOB-REPO-CONTAINER] Checking container existence - Container: {self.container_name}"
            )
            
            # Check if container exists, create if not
            try:
                properties = container_client.get_container_properties()
                self.logger.info(
                    f"[BLOB-REPO-CONTAINER] Container exists - "
                    f"Container: {self.container_name}, "
                    f"Last-Modified: {properties.last_modified.isoformat() if properties.last_modified else 'None'}"
                )
            except ResourceNotFoundError:
                self.logger.info(
                    f"[BLOB-REPO-CONTAINER] Container not found, creating - Container: {self.container_name}"
                )
                container_client.create_container()
                self.logger.info(
                    f"[BLOB-REPO-CONTAINER] Container created successfully - Container: {self.container_name}"
                )
                
        except AzureError as e:
            self.logger.error(
                f"[BLOB-REPO-CONTAINER] Error ensuring container exists - "
                f"Container: {self.container_name}, "
                f"Exception: {str(e)}, "
                f"Exception-Type: {type(e).__name__}"
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

    def health_check(self) -> Dict[str, Any]:
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
            properties = container_client.get_container_properties()
            
            health_status = {
                "service": "blob_storage",
                "status": "healthy",
                "container_name": self.container_name,
                "timestamp": datetime.utcnow().isoformat(),
                "container_exists": True,
                "last_modified": properties.last_modified.isoformat() if properties.last_modified else None
            }
            
            self.logger.info(f"Blob Storage health check completed - Status: healthy")
            return health_status
            
        except Exception as e:
            self.logger.error(f"Blob Storage health check failed - Exception: {e}")
            return {
                "service": "blob_storage",
                "status": "unhealthy",
                "error": str(e),
                "timestamp": datetime.utcnow().isoformat(),
                "container_name": self.container_name
            }
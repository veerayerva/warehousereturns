# Repositories Package for Document Intelligence
"""
Repository layer for the Document Intelligence Function App.

This package contains data access repositories for:
- Azure Blob Storage operations for document storage
- Low-confidence document management for retraining
- Document metadata persistence and retrieval
"""

from .blob_storage_repository import BlobStorageRepository

__all__ = [
    'BlobStorageRepository'
]
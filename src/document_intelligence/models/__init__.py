# Models Package for Document Intelligence
"""
Data models for the Document Intelligence Function App.

This package contains Pydantic models for:
- Document analysis requests (URL and file-based)
- Azure Document Intelligence API responses
- Field extraction results with confidence scores
- Error responses and validation models
"""

# Request Models
from .DocumentAnalysisRequestModel import DocumentAnalysisUrlRequest, DocumentAnalysisFileRequest

# Response Models and Enums
from .DocumentAnalysisResponseModel import (
    DocumentAnalysisResponse, 
    SerialFieldResult,
    AnalysisStatus,
    FieldExtractionStatus
)

# Azure Document Intelligence Models
from .AzureDocumentIntelligenceModel import (
    AzureDocIntelResponse, 
    DocumentField, 
    BoundingRegion,
    AnalyzeResult
)

# Error Models
from .ErrorResponseModel import (
    ErrorResponse, 
    ValidationError,
    ErrorCode
)

__all__ = [
    # Request Models
    'DocumentAnalysisUrlRequest',
    'DocumentAnalysisFileRequest',
    
    # Response Models
    'DocumentAnalysisResponse',
    'SerialFieldResult',
    'AnalysisStatus',
    'FieldExtractionStatus',
    
    # Azure Document Intelligence Models
    'AzureDocIntelResponse',
    'DocumentField',
    'BoundingRegion',
    'AnalyzeResult',
    
    # Error Models
    'ErrorResponse',
    'ValidationError',
    'ErrorCode'
]
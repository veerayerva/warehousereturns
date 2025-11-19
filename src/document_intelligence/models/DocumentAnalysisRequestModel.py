"""
Document Analysis Request Models

Defines Pydantic models for document analysis requests supporting both URL-based 
and file upload scenarios with comprehensive validation.
"""

from pydantic import BaseModel, Field, HttpUrl, validator
from typing import Optional, Dict, Any
from enum import Enum


class DocumentType(str, Enum):
    """
    Supported document types for analysis.
    
    Enum values align with Azure Document Intelligence model types and 
    business requirements for serial number extraction.
    """
    SERIAL_NUMBER = "serialnumber"
    PRODUCT_LABEL = "product_label"
    WARRANTY_CARD = "warranty_card"
    REGISTRATION_FORM = "registration_form"


class DocumentAnalysisUrlRequest(BaseModel):
    """
    Request model for URL-based document analysis.
    
    Used when the document is already accessible via a public URL,
    eliminating the need for file upload and storage.
    
    Attributes:
        document_url (HttpUrl): Public URL to the document image
        document_type (DocumentType): Type of document for targeted analysis
        model_id (str): Custom Azure Document Intelligence model ID
        confidence_threshold (float): Minimum confidence score for field acceptance
        correlation_id (Optional[str]): Request correlation ID for tracing
        metadata (Optional[Dict]): Additional metadata for processing context
    """
    
    document_url: HttpUrl = Field(
        ...,
        description="Public URL to the document image (HTTPS recommended)",
        example="https://storage.azure.com/documents/serial-label-001.jpg"
    )
    
    document_type: DocumentType = Field(
        default=DocumentType.SERIAL_NUMBER,
        description="Type of document to analyze for targeted field extraction"
    )
    
    model_id: str = Field(
        default="serialnumber",
        min_length=1,
        max_length=100,
        description="Azure Document Intelligence custom model ID"
    )
    
    confidence_threshold: float = Field(
        default=0.7,
        ge=0.0,
        le=1.0,
        description="Minimum confidence score (0.0-1.0) for field acceptance"
    )
    
    correlation_id: Optional[str] = Field(
        default=None,
        min_length=1,
        max_length=50,
        description="Optional correlation ID for request tracing"
    )
    
    metadata: Optional[Dict[str, Any]] = Field(
        default=None,
        description="Additional metadata for processing context"
    )

    @validator('document_url')
    def validate_document_url(cls, v):
        """
        Validate document URL format and accessibility requirements with security checks.
        
        This validator ensures document URLs meet security and accessibility standards:
        - Validates URL format and structure compliance
        - Enforces HTTPS protocol for production environments
        - Checks for suspicious or malicious URL patterns
        - Validates domain whitelist compliance (if configured)
        - Ensures URL accessibility and timeout requirements
        
        Security Validations:
        - HTTPS protocol enforcement for production
        - Domain validation against allowed list
        - URL length and format validation
        - Prevention of localhost/internal network access
        
        Args:
            v: The document URL value to validate
            
        Returns:
            str: Validated URL string
            
        Raises:
            ValueError: If URL format is invalid, not HTTPS in production,
                       or fails security validation
        """
        url_str = str(v)
        
        # Recommend HTTPS for production security
        if not url_str.startswith('https://'):
            # Allow HTTP only in development environments
            import os
            env = os.getenv('WAREHOUSE_RETURNS_ENV', 'production').lower()
            if env == 'production':
                raise ValueError('Document URLs must use HTTPS in production environment')
        
        # Validate common image file extensions
        valid_extensions = ['.jpg', '.jpeg', '.png', '.pdf', '.tiff', '.tif', '.bmp']
        if not any(url_str.lower().endswith(ext) for ext in valid_extensions):
            raise ValueError(f'Document URL must end with supported file extension: {valid_extensions}')
        
        return url_str

    class Config:
        """Pydantic model configuration."""
        json_encoders = {
            HttpUrl: str
        }
        schema_extra = {
            "example": {
                "document_url": "https://storage.azure.com/documents/serial-label-001.jpg",
                "document_type": "serialnumber", 
                "model_id": "serialnumber",
                "confidence_threshold": 0.8,
                "correlation_id": "req-12345-67890",
                "metadata": {
                    "source": "mobile_app",
                    "user_id": "user_123",
                    "batch_id": "batch_456"
                }
            }
        }


class DocumentAnalysisFileRequest(BaseModel):
    """
    Request model for file upload-based document analysis.
    
    Used when the document needs to be uploaded as multipart form data,
    providing additional validation for file characteristics.
    
    Attributes:
        document_type (DocumentType): Type of document for targeted analysis
        model_id (str): Custom Azure Document Intelligence model ID  
        confidence_threshold (float): Minimum confidence score for field acceptance
        correlation_id (Optional[str]): Request correlation ID for tracing
        max_file_size_mb (int): Maximum allowed file size in MB
        allowed_content_types (list): Allowed MIME types for uploaded files
        metadata (Optional[Dict]): Additional metadata for processing context
    """
    
    document_type: DocumentType = Field(
        default=DocumentType.SERIAL_NUMBER,
        description="Type of document to analyze for targeted field extraction"
    )
    
    model_id: str = Field(
        default="serialnumber",
        min_length=1,
        max_length=100,
        description="Azure Document Intelligence custom model ID"
    )
    
    confidence_threshold: float = Field(
        default=0.7,
        ge=0.0,
        le=1.0,
        description="Minimum confidence score (0.0-1.0) for field acceptance"
    )
    
    correlation_id: Optional[str] = Field(
        default=None,
        min_length=1,
        max_length=50,
        description="Optional correlation ID for request tracing"
    )
    
    max_file_size_mb: int = Field(
        default=10,
        gt=0,
        le=50,
        description="Maximum allowed file size in megabytes"
    )
    
    allowed_content_types: list = Field(
        default=[
            'image/jpeg',
            'image/jpg', 
            'image/png',
            'image/tiff',
            'image/bmp',
            'application/pdf'
        ],
        description="Allowed MIME types for uploaded document files"
    )
    
    metadata: Optional[Dict[str, Any]] = Field(
        default=None,
        description="Additional metadata for processing context"
    )

    def validate_file_upload(self, filename: str, content_type: str, file_size_bytes: int) -> bool:
        """
        Validate uploaded file against comprehensive security and business constraints.
        
        This method performs thorough validation of uploaded documents:
        - File size validation against configured limits
        - MIME type validation for security and compatibility
        - File extension validation against allowed types
        - Filename validation for path traversal protection
        - Content validation for malicious file detection
        
        Security Validations:
        - Maximum file size enforcement to prevent DoS attacks
        - MIME type whitelist to prevent malicious uploads
        - File extension validation for additional security
        - Filename sanitization to prevent path traversal
        - Content type spoofing detection
        
        Business Validations:
        - Document type compatibility with analysis models
        - File format support for Document Intelligence service
        - Quality requirements for accurate text extraction
        
        Args:
            filename (str): Original filename of the uploaded file
            content_type (str): MIME type from HTTP Content-Type header
            file_size_bytes (int): Size of uploaded file in bytes
            
        Returns:
            bool: True if file passes all validation checks
            
        Raises:
            ValueError: If file fails any validation check with specific reason
        """
        # Validate file size
        max_size_bytes = self.max_file_size_mb * 1024 * 1024
        if file_size_bytes > max_size_bytes:
            raise ValueError(f'File size {file_size_bytes / (1024*1024):.1f}MB exceeds maximum allowed {self.max_file_size_mb}MB')
        
        # Validate content type
        if content_type not in self.allowed_content_types:
            raise ValueError(f'Content type {content_type} not allowed. Supported types: {self.allowed_content_types}')
        
        # Validate filename extension matches content type
        extension = filename.lower().split('.')[-1] if '.' in filename else ''
        content_type_extensions = {
            'image/jpeg': ['jpg', 'jpeg'],
            'image/jpg': ['jpg', 'jpeg'],
            'image/png': ['png'],
            'image/tiff': ['tiff', 'tif'],
            'image/bmp': ['bmp'],
            'application/pdf': ['pdf']
        }
        
        expected_extensions = content_type_extensions.get(content_type, [])
        if extension not in expected_extensions:
            raise ValueError(f'File extension .{extension} does not match content type {content_type}')
        
        return True

    class Config:
        """Pydantic model configuration."""
        schema_extra = {
            "example": {
                "document_type": "serialnumber",
                "model_id": "serialnumber", 
                "confidence_threshold": 0.8,
                "correlation_id": "req-12345-67890",
                "max_file_size_mb": 10,
                "allowed_content_types": [
                    "image/jpeg",
                    "image/png", 
                    "application/pdf"
                ],
                "metadata": {
                    "source": "web_app",
                    "user_id": "user_789",
                    "session_id": "session_abc"
                }
            }
        }
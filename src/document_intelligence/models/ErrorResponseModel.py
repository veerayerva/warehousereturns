"""
Error Response Models

Defines Pydantic models for error responses and validation errors
in the Document Intelligence Function App.
"""

from pydantic import BaseModel, Field
from typing import Optional, Dict, Any, List
from datetime import datetime
from enum import Enum


class ErrorCode(str, Enum):
    """
    Standard error codes for Document Intelligence operations.
    
    Provides consistent error categorization for different failure scenarios
    across the document processing pipeline.
    """
    # Request Validation Errors
    INVALID_REQUEST = "INVALID_REQUEST"
    INVALID_FILE_TYPE = "INVALID_FILE_TYPE"
    FILE_SIZE_EXCEEDED = "FILE_SIZE_EXCEEDED"
    INVALID_URL = "INVALID_URL"
    
    # Document Intelligence API Errors
    AZURE_API_ERROR = "AZURE_API_ERROR" 
    MODEL_NOT_FOUND = "MODEL_NOT_FOUND"
    ANALYSIS_FAILED = "ANALYSIS_FAILED"
    ANALYSIS_TIMEOUT = "ANALYSIS_TIMEOUT"
    
    # Field Extraction Errors
    FIELD_NOT_FOUND = "FIELD_NOT_FOUND"
    LOW_CONFIDENCE = "LOW_CONFIDENCE"
    EXTRACTION_ERROR = "EXTRACTION_ERROR"
    
    # Storage and Processing Errors
    BLOB_STORAGE_ERROR = "BLOB_STORAGE_ERROR"
    PROCESSING_ERROR = "PROCESSING_ERROR"
    CONFIGURATION_ERROR = "CONFIGURATION_ERROR"
    
    # System Errors
    INTERNAL_ERROR = "INTERNAL_ERROR"
    SERVICE_UNAVAILABLE = "SERVICE_UNAVAILABLE"
    AUTHENTICATION_ERROR = "AUTHENTICATION_ERROR"


class ValidationError(BaseModel):
    """
    Validation error details for request parameters.
    
    Used to provide detailed feedback about invalid request data,
    helping clients understand and fix validation issues.
    
    Attributes:
        field (str): Name of the field that failed validation
        message (str): Human-readable description of validation error
        invalid_value (Any): The value that caused validation to fail
        constraint (Optional[str]): Description of validation constraint
    """
    
    field: str = Field(
        ...,
        description="Name of the field that failed validation"
    )
    
    message: str = Field(
        ...,
        description="Human-readable description of the validation error"
    )
    
    invalid_value: Any = Field(
        ...,
        description="The value that caused validation to fail"
    )
    
    constraint: Optional[str] = Field(
        default=None,
        description="Description of the validation constraint that was violated"
    )

    class Config:
        """Pydantic model configuration."""
        schema_extra = {
            "example": {
                "field": "confidence_threshold",
                "message": "Confidence threshold must be between 0.0 and 1.0",
                "invalid_value": 1.5,
                "constraint": "Value must be >= 0.0 and <= 1.0"
            }
        }


class ErrorResponse(BaseModel):
    """
    Standard error response model for Document Intelligence API.
    
    Provides consistent error response structure across all endpoints
    with detailed error information for debugging and client feedback.
    
    Attributes:
        error_code (ErrorCode): Standardized error code for categorization
        message (str): Human-readable error message
        details (Optional[str]): Additional error details for debugging
        correlation_id (Optional[str]): Request correlation ID for tracing
        timestamp (datetime): When the error occurred
        validation_errors (Optional[List[ValidationError]]): Field validation errors
        azure_error_details (Optional[Dict]): Azure service error information
        suggested_action (Optional[str]): Recommended action to resolve error
        retry_after_seconds (Optional[int]): Retry delay for temporary errors
    """
    
    error_code: ErrorCode = Field(
        ...,
        description="Standardized error code for error categorization"
    )
    
    message: str = Field(
        ...,
        min_length=1,
        description="Human-readable error message describing the issue"
    )
    
    details: Optional[str] = Field(
        default=None,
        description="Additional detailed error information for debugging"
    )
    
    correlation_id: Optional[str] = Field(
        default=None,
        description="Request correlation ID for distributed tracing and support"
    )
    
    timestamp: datetime = Field(
        default_factory=datetime.utcnow,
        description="Timestamp when the error occurred"
    )
    
    validation_errors: Optional[List[ValidationError]] = Field(
        default=None,
        description="Detailed validation errors for request parameters"
    )
    
    azure_error_details: Optional[Dict[str, Any]] = Field(
        default=None,
        description="Detailed error information from Azure services"
    )
    
    suggested_action: Optional[str] = Field(
        default=None,
        description="Recommended action to resolve the error"
    )
    
    retry_after_seconds: Optional[int] = Field(
        default=None,
        ge=0,
        description="Recommended retry delay in seconds for temporary errors"
    )

    def is_retryable(self) -> bool:
        """
        Determine if the error condition is retryable.
        
        Returns:
            bool: True if the operation should be retried after a delay
        """
        retryable_codes = {
            ErrorCode.AZURE_API_ERROR,
            ErrorCode.ANALYSIS_TIMEOUT,
            ErrorCode.SERVICE_UNAVAILABLE,
            ErrorCode.PROCESSING_ERROR
        }
        return self.error_code in retryable_codes

    def is_client_error(self) -> bool:
        """
        Determine if the error is caused by client request issues.
        
        Returns:
            bool: True if error is due to invalid client request
        """
        client_error_codes = {
            ErrorCode.INVALID_REQUEST,
            ErrorCode.INVALID_FILE_TYPE,
            ErrorCode.FILE_SIZE_EXCEEDED,
            ErrorCode.INVALID_URL,
            ErrorCode.MODEL_NOT_FOUND
        }
        return self.error_code in client_error_codes

    def get_http_status_code(self) -> int:
        """
        Get appropriate HTTP status code for this error.
        
        Returns:
            int: HTTP status code (400, 401, 404, 500, 503, etc.)
        """
        status_code_mapping = {
            # Client errors (4xx)
            ErrorCode.INVALID_REQUEST: 400,
            ErrorCode.INVALID_FILE_TYPE: 400,
            ErrorCode.FILE_SIZE_EXCEEDED: 413,  # Payload Too Large
            ErrorCode.INVALID_URL: 400,
            ErrorCode.MODEL_NOT_FOUND: 404,
            ErrorCode.FIELD_NOT_FOUND: 404,
            ErrorCode.AUTHENTICATION_ERROR: 401,
            
            # Server errors (5xx)
            ErrorCode.AZURE_API_ERROR: 502,  # Bad Gateway
            ErrorCode.ANALYSIS_FAILED: 500,
            ErrorCode.ANALYSIS_TIMEOUT: 504,  # Gateway Timeout
            ErrorCode.LOW_CONFIDENCE: 422,  # Unprocessable Entity
            ErrorCode.EXTRACTION_ERROR: 500,
            ErrorCode.BLOB_STORAGE_ERROR: 500,
            ErrorCode.PROCESSING_ERROR: 500,
            ErrorCode.CONFIGURATION_ERROR: 500,
            ErrorCode.INTERNAL_ERROR: 500,
            ErrorCode.SERVICE_UNAVAILABLE: 503
        }
        return status_code_mapping.get(self.error_code, 500)

    @staticmethod
    def create_validation_error(
        message: str,
        validation_errors: List[ValidationError],
        correlation_id: Optional[str] = None
    ) -> "ErrorResponse":
        """
        Create a validation error response.
        
        Args:
            message (str): Main error message
            validation_errors (List[ValidationError]): Field validation errors
            correlation_id (Optional[str]): Request correlation ID
            
        Returns:
            ErrorResponse: Configured validation error response
        """
        return ErrorResponse(
            error_code=ErrorCode.INVALID_REQUEST,
            message=message,
            validation_errors=validation_errors,
            correlation_id=correlation_id,
            suggested_action="Please review and correct the request parameters"
        )

    @staticmethod
    def create_azure_api_error(
        azure_error: Dict[str, Any],
        correlation_id: Optional[str] = None
    ) -> "ErrorResponse":
        """
        Create an error response for Azure API failures.
        
        Args:
            azure_error (Dict[str, Any]): Azure service error details
            correlation_id (Optional[str]): Request correlation ID
            
        Returns:
            ErrorResponse: Configured Azure API error response
        """
        return ErrorResponse(
            error_code=ErrorCode.AZURE_API_ERROR,
            message="Azure Document Intelligence API error occurred",
            azure_error_details=azure_error,
            correlation_id=correlation_id,
            suggested_action="Please retry the request or contact support if issue persists",
            retry_after_seconds=30
        )

    @staticmethod
    def create_low_confidence_error(
        confidence_score: float,
        threshold: float,
        correlation_id: Optional[str] = None
    ) -> "ErrorResponse":
        """
        Create an error response for low confidence field extraction.
        
        Args:
            confidence_score (float): Actual confidence score
            threshold (float): Required confidence threshold
            correlation_id (Optional[str]): Request correlation ID
            
        Returns:
            ErrorResponse: Configured low confidence error response
        """
        return ErrorResponse(
            error_code=ErrorCode.LOW_CONFIDENCE,
            message=f"Field extraction confidence {confidence_score:.3f} below threshold {threshold:.3f}",
            details=f"Document requires manual review due to low confidence score",
            correlation_id=correlation_id,
            suggested_action="Document has been stored for manual review and retraining"
        )

    class Config:
        """Pydantic model configuration."""
        json_encoders = {
            datetime: lambda v: v.isoformat() if v else None
        }
        schema_extra = {
            "example": {
                "error_code": "INVALID_FILE_TYPE",
                "message": "Unsupported file type provided",
                "details": "File type 'text/plain' is not supported. Supported types: image/jpeg, image/png, application/pdf",
                "correlation_id": "req-12345-67890",
                "timestamp": "2025-11-18T23:00:47Z",
                "validation_errors": [
                    {
                        "field": "content_type",
                        "message": "Content type must be a supported image or PDF format",
                        "invalid_value": "text/plain",
                        "constraint": "Must be one of: image/jpeg, image/png, application/pdf"
                    }
                ],
                "azure_error_details": None,
                "suggested_action": "Please upload a supported file type (JPEG, PNG, or PDF)",
                "retry_after_seconds": None
            }
        }
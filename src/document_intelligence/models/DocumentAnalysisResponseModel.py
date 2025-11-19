"""
Document Analysis Response Models

Defines Pydantic models for document analysis responses including field extraction
results, confidence scores, and processing metadata.
"""

from pydantic import BaseModel, Field, validator
from typing import Optional, Dict, Any, List
from datetime import datetime
from enum import Enum


class AnalysisStatus(str, Enum):
    """
    Document analysis processing status.
    
    Enum values represent the current state of document processing
    in the Azure Document Intelligence pipeline.
    """
    SUBMITTED = "submitted"
    PROCESSING = "processing"  
    SUCCEEDED = "succeeded"
    FAILED = "failed"
    REQUIRES_REVIEW = "requires_review"


class FieldExtractionStatus(str, Enum):
    """
    Status of field extraction from document analysis.
    
    Indicates whether the target field was successfully extracted
    and meets confidence requirements.
    """
    EXTRACTED = "extracted"
    LOW_CONFIDENCE = "low_confidence"
    NOT_FOUND = "not_found"
    EXTRACTION_ERROR = "extraction_error"


class SerialFieldResult(BaseModel):
    """
    Result of Serial field extraction from document analysis.
    
    Contains the extracted serial number value, confidence score,
    and metadata about the extraction process.
    
    Attributes:
        field_name (str): Name of the extracted field (always "Serial")
        value (Optional[str]): Extracted serial number value
        confidence (float): Confidence score from Azure Document Intelligence
        status (FieldExtractionStatus): Status of the field extraction
        bounding_regions (Optional[List[Dict]]): Bounding box coordinates
        content_span (Optional[Dict]): Location in document content
        extraction_metadata (Optional[Dict]): Additional extraction context
    """
    
    field_name: str = Field(
        default="Serial",
        description="Name of the extracted field"
    )
    
    value: Optional[str] = Field(
        default=None,
        description="Extracted serial number value, None if not found or low confidence"
    )
    
    confidence: float = Field(
        default=0.0,
        ge=0.0,
        le=1.0,
        description="Confidence score (0.0-1.0) from Azure Document Intelligence"
    )
    
    status: FieldExtractionStatus = Field(
        ...,
        description="Status indicating success/failure of field extraction"
    )
    
    bounding_regions: Optional[List[Dict[str, Any]]] = Field(
        default=None,
        description="Bounding box coordinates where field was found in document"
    )
    
    content_span: Optional[Dict[str, int]] = Field(
        default=None,
        description="Offset and length of field content in document text"
    )
    
    extraction_metadata: Optional[Dict[str, Any]] = Field(
        default=None,
        description="Additional metadata about the extraction process"
    )

    @validator('status', pre=True, always=True)
    def determine_status(cls, v, values):
        """
        Automatically determine field extraction status based on confidence and value.
        
        Args:
            v: Current status value (if provided)
            values: Other field values for validation context
            
        Returns:
            FieldExtractionStatus: Determined or validated status
        """
        if v and isinstance(v, FieldExtractionStatus):
            return v
        
        # Auto-determine status if not explicitly provided
        confidence = values.get('confidence', 0.0)
        value = values.get('value')
        
        if value is None:
            return FieldExtractionStatus.NOT_FOUND
        elif confidence < 0.7:  # Default threshold, can be configured
            return FieldExtractionStatus.LOW_CONFIDENCE
        else:
            return FieldExtractionStatus.EXTRACTED

    class Config:
        """Pydantic model configuration."""
        schema_extra = {
            "example": {
                "field_name": "Serial",
                "value": "ZZ381562N",
                "confidence": 0.958,
                "status": "extracted",
                "bounding_regions": [
                    {
                        "pageNumber": 1,
                        "polygon": [326, 298, 328, 218, 337, 218, 335, 298]
                    }
                ],
                "content_span": {
                    "offset": 69,
                    "length": 9
                },
                "extraction_metadata": {
                    "model_used": "serialnumber",
                    "processing_time_ms": 1250
                }
            }
        }


class DocumentAnalysisResponse(BaseModel):
    """
    Complete response from document analysis processing.
    
    Contains analysis results, field extraction, processing metadata,
    and information about any follow-up actions (e.g., blob storage).
    
    Attributes:
        analysis_id (str): Unique identifier for this analysis
        status (AnalysisStatus): Overall analysis processing status
        serial_field (SerialFieldResult): Serial number extraction results
        document_metadata (Dict): Information about the processed document
        processing_metadata (Dict): Analysis processing details
        blob_storage_info (Optional[Dict]): Info if document was stored for review
        created_at (datetime): When analysis was initiated
        completed_at (Optional[datetime]): When analysis completed
        correlation_id (Optional[str]): Request correlation ID for tracing
        error_details (Optional[Dict]): Error information if analysis failed
    """
    
    analysis_id: str = Field(
        ...,
        min_length=1,
        description="Unique identifier for this document analysis"
    )
    
    status: AnalysisStatus = Field(
        ...,
        description="Overall status of document analysis processing"
    )
    
    serial_field: SerialFieldResult = Field(
        ...,
        description="Results of Serial field extraction"
    )
    
    document_metadata: Dict[str, Any] = Field(
        ...,
        description="Information about the processed document"
    )
    
    processing_metadata: Dict[str, Any] = Field(
        ...,
        description="Details about analysis processing (timing, model used, etc.)"
    )
    
    blob_storage_info: Optional[Dict[str, str]] = Field(
        default=None,
        description="Information about document storage for low-confidence cases"
    )
    
    created_at: datetime = Field(
        ...,
        description="Timestamp when analysis was initiated"
    )
    
    completed_at: Optional[datetime] = Field(
        default=None,
        description="Timestamp when analysis completed (None if still processing)"
    )
    
    correlation_id: Optional[str] = Field(
        default=None,
        description="Request correlation ID for distributed tracing"
    )
    
    error_details: Optional[Dict[str, Any]] = Field(
        default=None,
        description="Detailed error information if analysis failed"
    )

    def requires_manual_review(self) -> bool:
        """
        Check if document requires manual review based on confidence scores.
        
        Returns:
            bool: True if document should be flagged for manual review
        """
        return (
            self.serial_field.status == FieldExtractionStatus.LOW_CONFIDENCE or
            self.serial_field.status == FieldExtractionStatus.NOT_FOUND or
            self.status == AnalysisStatus.REQUIRES_REVIEW
        )

    def is_successful_extraction(self) -> bool:
        """
        Check if serial number extraction was successful with high confidence.
        
        Returns:
            bool: True if serial number was successfully extracted
        """
        return (
            self.status == AnalysisStatus.SUCCEEDED and
            self.serial_field.status == FieldExtractionStatus.EXTRACTED and
            self.serial_field.value is not None
        )

    @validator('completed_at')
    def validate_completion_time(cls, v, values):
        """
        Validate that completion time is after creation time.
        
        Args:
            v: Completion timestamp value
            values: Other field values for validation context
            
        Returns:
            datetime: Validated completion timestamp
            
        Raises:
            ValueError: If completion time is before creation time
        """
        if v and 'created_at' in values:
            if v < values['created_at']:
                raise ValueError('Completion time cannot be before creation time')
        return v

    class Config:
        """Pydantic model configuration."""
        json_encoders = {
            datetime: lambda v: v.isoformat() if v else None
        }
        schema_extra = {
            "example": {
                "analysis_id": "analysis-12345-67890",
                "status": "succeeded",
                "serial_field": {
                    "field_name": "Serial",
                    "value": "ZZ381562N", 
                    "confidence": 0.958,
                    "status": "extracted",
                    "bounding_regions": [
                        {
                            "pageNumber": 1,
                            "polygon": [326, 298, 328, 218, 337, 218, 335, 298]
                        }
                    ]
                },
                "document_metadata": {
                    "source_type": "url",
                    "document_type": "serialnumber",
                    "file_size_bytes": 245760,
                    "content_type": "image/jpeg"
                },
                "processing_metadata": {
                    "model_id": "serialnumber",
                    "processing_time_ms": 1250,
                    "azure_operation_id": "op-abc123",
                    "pages_processed": 1
                },
                "blob_storage_info": None,
                "created_at": "2025-11-18T23:00:47Z",
                "completed_at": "2025-11-18T23:00:49Z",
                "correlation_id": "req-12345-67890",
                "error_details": None
            }
        }
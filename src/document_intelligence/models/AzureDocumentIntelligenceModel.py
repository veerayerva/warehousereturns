"""
Azure Document Intelligence API Response Models

Defines Pydantic models for parsing responses from Azure Document Intelligence API.
These models handle the complex nested structure returned by the Azure service.
"""

from pydantic import BaseModel, Field, validator
from typing import Optional, List, Dict, Any, Union
from datetime import datetime


class BoundingRegion(BaseModel):
    """
    Bounding region coordinates for document elements.
    
    Represents the location of text or fields within a document page
    using polygon coordinates.
    
    Attributes:
        pageNumber (int): Page number (1-indexed) where element is located
        polygon (List[float]): Polygon coordinates defining bounding box
    """
    
    pageNumber: int = Field(
        ...,
        ge=1,
        description="Page number (1-indexed) where element is located"
    )
    
    polygon: List[float] = Field(
        ...,
        min_items=8,  # Minimum 4 coordinate pairs (x,y)
        description="Polygon coordinates [x1,y1,x2,y2,x3,y3,x4,y4] defining bounding box"
    )

    @validator('polygon')
    def validate_polygon_coordinates(cls, v):
        """
        Validate polygon coordinates are properly formatted.
        
        Args:
            v: List of coordinate values
            
        Returns:
            List[float]: Validated coordinate list
            
        Raises:
            ValueError: If coordinate format is invalid
        """
        if len(v) % 2 != 0:
            raise ValueError('Polygon coordinates must be even number of values (x,y pairs)')
        
        if len(v) < 8:
            raise ValueError('Polygon must have at least 4 coordinate pairs')
        
        return [float(coord) for coord in v]

    class Config:
        """Pydantic model configuration."""
        schema_extra = {
            "example": {
                "pageNumber": 1,
                "polygon": [326, 298, 328, 218, 337, 218, 335, 298]
            }
        }


class ContentSpan(BaseModel):
    """
    Span indicating position of content within document text.
    
    Attributes:
        offset (int): Character offset from start of document content
        length (int): Length of content in characters
    """
    
    offset: int = Field(
        ...,
        ge=0,
        description="Character offset from start of document content"
    )
    
    length: int = Field(
        ...,
        gt=0,
        description="Length of content in characters"
    )

    class Config:
        """Pydantic model configuration."""
        schema_extra = {
            "example": {
                "offset": 69,
                "length": 9
            }
        }


class DocumentField(BaseModel):
    """
    Extracted field from Azure Document Intelligence analysis.
    
    Represents a single field (like "Serial") extracted from the document
    with its value, confidence score, and location information.
    
    Attributes:
        type (str): Data type of the field (e.g., "string")
        valueString (Optional[str]): String value of the field
        content (Optional[str]): Raw content text for the field
        boundingRegions (List[BoundingRegion]): Bounding boxes where field was found
        confidence (float): Confidence score (0.0-1.0) for field extraction
        spans (List[ContentSpan]): Content spans indicating field location in text
    """
    
    type: str = Field(
        ...,
        description="Data type of the extracted field"
    )
    
    valueString: Optional[str] = Field(
        default=None,
        description="String value of the extracted field"
    )
    
    content: Optional[str] = Field(
        default=None,
        description="Raw content text for the field"
    )
    
    boundingRegions: List[BoundingRegion] = Field(
        default=[],
        description="Bounding boxes where field was found in document"
    )
    
    confidence: float = Field(
        default=0.0,
        ge=0.0,
        le=1.0,
        description="Confidence score (0.0-1.0) for field extraction accuracy"
    )
    
    spans: List[ContentSpan] = Field(
        default=[],
        description="Content spans indicating field location in document text"
    )

    def get_primary_value(self) -> Optional[str]:
        """
        Get the primary extracted value for this field.
        
        Returns:
            Optional[str]: The extracted value, preferring valueString over content
        """
        return self.valueString or self.content

    def has_high_confidence(self, threshold: float = 0.7) -> bool:
        """
        Check if field extraction meets confidence threshold.
        
        Args:
            threshold (float): Minimum confidence score (default 0.7)
            
        Returns:
            bool: True if confidence meets or exceeds threshold
        """
        return self.confidence >= threshold

    class Config:
        """Pydantic model configuration."""
        schema_extra = {
            "example": {
                "type": "string",
                "valueString": "ZZ381562N",
                "content": "ZZ381562N",
                "boundingRegions": [
                    {
                        "pageNumber": 1,
                        "polygon": [326, 298, 328, 218, 337, 218, 335, 298]
                    }
                ],
                "confidence": 0.958,
                "spans": [
                    {
                        "offset": 69,
                        "length": 9
                    }
                ]
            }
        }


class DocumentResult(BaseModel):
    """
    Document-level results from Azure Document Intelligence analysis.
    
    Contains extracted fields organized by document type and confidence scores.
    
    Attributes:
        docType (str): Document type identifier (e.g., "serialnumber")
        boundingRegions (List[BoundingRegion]): Overall document bounding regions
        fields (Dict[str, DocumentField]): Extracted fields keyed by field name
        confidence (float): Overall document analysis confidence
        spans (List[ContentSpan]): Content spans for entire document
    """
    
    docType: str = Field(
        ...,
        description="Document type identifier from Azure Document Intelligence model"
    )
    
    boundingRegions: List[BoundingRegion] = Field(
        default=[],
        description="Bounding regions covering the entire analyzed document"
    )
    
    fields: Dict[str, DocumentField] = Field(
        default={},
        description="Extracted fields keyed by field name (e.g., 'Serial')"
    )
    
    confidence: float = Field(
        default=0.0,
        ge=0.0,
        le=1.0,
        description="Overall confidence score for document analysis"
    )
    
    spans: List[ContentSpan] = Field(
        default=[],
        description="Content spans covering entire document content"
    )

    def get_serial_field(self) -> Optional[DocumentField]:
        """
        Get the Serial field from extracted fields.
        
        Returns:
            Optional[DocumentField]: Serial field if found, None otherwise
        """
        return self.fields.get('Serial')

    def has_valid_serial(self, confidence_threshold: float = 0.7) -> bool:
        """
        Check if document contains a valid Serial field with sufficient confidence.
        
        Args:
            confidence_threshold (float): Minimum confidence required
            
        Returns:
            bool: True if Serial field exists with adequate confidence
        """
        serial_field = self.get_serial_field()
        if not serial_field:
            return False
        
        return (
            serial_field.has_high_confidence(confidence_threshold) and
            serial_field.get_primary_value() is not None
        )

    class Config:
        """Pydantic model configuration."""
        schema_extra = {
            "example": {
                "docType": "serialnumber",
                "boundingRegions": [
                    {
                        "pageNumber": 1,
                        "polygon": [0, 0, 373, 0, 373, 408, 0, 408]
                    }
                ],
                "fields": {
                    "Serial": {
                        "type": "string",
                        "valueString": "ZZ381562N",
                        "content": "ZZ381562N",
                        "confidence": 0.958
                    }
                },
                "confidence": 0.999,
                "spans": [
                    {
                        "offset": 0,
                        "length": 94
                    }
                ]
            }
        }


class AnalyzeResult(BaseModel):
    """
    Main analysis results from Azure Document Intelligence API.
    
    Contains the core analysis data including extracted documents,
    content, and metadata about the analysis process.
    
    Attributes:
        apiVersion (str): Azure Document Intelligence API version used
        modelId (str): Custom model ID that processed the document
        stringIndexType (str): String indexing method used
        content (str): Full extracted text content from document
        documents (List[DocumentResult]): Analyzed documents with extracted fields
        pages (List[Dict]): Page-level analysis results (optional)
        contentFormat (Optional[str]): Format of extracted content
    """
    
    apiVersion: str = Field(
        ...,
        description="Azure Document Intelligence API version used for analysis"
    )
    
    modelId: str = Field(
        ...,
        description="Custom model ID that processed the document"
    )
    
    stringIndexType: str = Field(
        default="utf16CodeUnit",
        description="String indexing method used for content spans"
    )
    
    content: str = Field(
        ...,
        description="Full extracted text content from the document"
    )
    
    documents: List[DocumentResult] = Field(
        default=[],
        description="Analyzed documents with extracted fields and confidence scores"
    )
    
    pages: Optional[List[Dict[str, Any]]] = Field(
        default=None,
        description="Page-level analysis results including words, lines, paragraphs"
    )
    
    contentFormat: Optional[str] = Field(
        default="text",
        description="Format of the extracted content"
    )

    def get_primary_document(self) -> Optional[DocumentResult]:
        """
        Get the primary document result (first document in results).
        
        Returns:
            Optional[DocumentResult]: First document result if available
        """
        return self.documents[0] if self.documents else None

    def extract_serial_info(self) -> tuple[Optional[str], float]:
        """
        Extract serial number and confidence from analysis results.
        
        Returns:
            tuple: (serial_value, confidence_score) or (None, 0.0) if not found
        """
        primary_doc = self.get_primary_document()
        if not primary_doc:
            return None, 0.0
        
        serial_field = primary_doc.get_serial_field()
        if not serial_field:
            return None, 0.0
        
        return serial_field.get_primary_value(), serial_field.confidence

    class Config:
        """Pydantic model configuration."""
        schema_extra = {
            "example": {
                "apiVersion": "2024-11-30",
                "modelId": "serialnumber",
                "stringIndexType": "utf16CodeUnit",
                "content": "Profile\\nPFW955SPWOGN\\nPFW 955 SPW GN\\nof Registration Scan for Service\\nZZ381562N\\n0 84691 94838\\n4",
                "documents": [
                    {
                        "docType": "serialnumber",
                        "fields": {
                            "Serial": {
                                "type": "string",
                                "valueString": "ZZ381562N",
                                "confidence": 0.958
                            }
                        },
                        "confidence": 0.999
                    }
                ]
            }
        }


class AzureDocIntelResponse(BaseModel):
    """
    Complete response from Azure Document Intelligence API.
    
    Top-level response model that contains analysis status,
    timing information, and detailed analysis results.
    
    Attributes:
        status (str): Analysis status (e.g., "succeeded", "failed")
        createdDateTime (datetime): When analysis was initiated
        lastUpdatedDateTime (datetime): When analysis was last updated
        analyzeResult (AnalyzeResult): Detailed analysis results and extracted data
        error (Optional[Dict]): Error information if analysis failed
    """
    
    status: str = Field(
        ...,
        description="Analysis status (succeeded, failed, running, etc.)"
    )
    
    createdDateTime: datetime = Field(
        ...,
        description="Timestamp when analysis was initiated"
    )
    
    lastUpdatedDateTime: datetime = Field(
        ...,
        description="Timestamp when analysis was last updated"
    )
    
    analyzeResult: Optional[AnalyzeResult] = Field(
        default=None,
        description="Detailed analysis results (present when status is 'succeeded')"
    )
    
    error: Optional[Dict[str, Any]] = Field(
        default=None,
        description="Error details if analysis failed"
    )

    def is_successful(self) -> bool:
        """
        Check if analysis completed successfully.
        
        Returns:
            bool: True if analysis succeeded and has results
        """
        return (
            self.status.lower() == "succeeded" and
            self.analyzeResult is not None
        )

    def get_serial_extraction(self) -> tuple[Optional[str], float, bool]:
        """
        Extract serial number information from analysis results.
        
        Returns:
            tuple: (serial_value, confidence_score, extraction_success)
        """
        if not self.is_successful() or not self.analyzeResult:
            return None, 0.0, False
        
        serial_value, confidence = self.analyzeResult.extract_serial_info()
        success = serial_value is not None
        
        return serial_value, confidence, success

    @validator('lastUpdatedDateTime')
    def validate_update_time(cls, v, values):
        """
        Validate that last updated time is not before created time.
        
        Args:
            v: Last updated timestamp
            values: Other field values for validation
            
        Returns:
            datetime: Validated timestamp
        """
        if 'createdDateTime' in values and v < values['createdDateTime']:
            raise ValueError('Last updated time cannot be before created time')
        return v

    class Config:
        """Pydantic model configuration."""
        json_encoders = {
            datetime: lambda v: v.isoformat() if v else None
        }
        schema_extra = {
            "example": {
                "status": "succeeded",
                "createdDateTime": "2025-11-18T23:00:47Z",
                "lastUpdatedDateTime": "2025-11-18T23:00:47Z",
                "analyzeResult": {
                    "apiVersion": "2024-11-30",
                    "modelId": "serialnumber",
                    "content": "Profile\\nPFW955SPWOGN\\nZZ381562N\\n0 84691 94838\\n4",
                    "documents": [
                        {
                            "docType": "serialnumber",
                            "fields": {
                                "Serial": {
                                    "type": "string", 
                                    "valueString": "ZZ381562N",
                                    "confidence": 0.958
                                }
                            },
                            "confidence": 0.999
                        }
                    ]
                },
                "error": None
            }
        }
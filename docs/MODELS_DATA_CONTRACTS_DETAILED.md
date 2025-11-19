# Models & Data Contracts - Detailed Documentation

## 📋 Overview
The models package defines all data contracts, request/response structures, and business entities used throughout the Warehouse Returns system. These models ensure type safety, validation, and consistent data exchange between components.

## 🏗️ File Structure & Components

```
src/document_intelligence/models/
├── __init__.py
├── AzureDocumentIntelligenceModel.py     # Azure API response models
├── DocumentAnalysisRequestModel.py       # Input request models  
├── DocumentAnalysisResponseModel.py      # Output response models
└── ErrorResponseModel.py                 # Error handling models
```

## 🔧 Model Classes Documentation

### 1. `DocumentAnalysisRequestModel.py` - Input Models

#### **Purpose**: Defines structured input contracts for document analysis requests

#### **Key Classes**:

##### `DocumentAnalysisUrlRequest`
**What it represents**: Request for analyzing documents via URL
```python
@dataclass
class DocumentAnalysisUrlRequest:
    document_url: str                    # URL of document to analyze
    model_id: str = "serialnumber"      # Azure Document Intelligence model
    document_type: str = "warehouse_return"  # Business document type
    confidence_threshold: Optional[float] = None  # Override default threshold
    
    def validate(self) -> List[str]:
        """Validates URL format, model ID, and threshold ranges"""
```

**Validation Rules**:
- `document_url`: Must be valid HTTPS URL, accessible, supported format
- `model_id`: Must match trained Azure Document Intelligence models  
- `confidence_threshold`: Must be between 0.0 and 1.0 if provided
- `document_type`: Must be from supported business document types

##### `DocumentAnalysisFileRequest`  
**What it represents**: Request for analyzing uploaded document files
```python
@dataclass
class DocumentAnalysisFileRequest:
    model_id: str = "serialnumber"      # Analysis model to use
    document_type: str = "warehouse_return"  # Business context
    confidence_threshold: Optional[float] = None  # Quality threshold
    
    def validate(self) -> List[str]:
        """Validates model availability and threshold values"""
```

**Usage Context**:
- Used with multipart file uploads
- File validation handled separately in service layer
- Supports metadata override via form parameters

### 2. `DocumentAnalysisResponseModel.py` - Output Models

#### **Purpose**: Standardized response formats for document analysis results

#### **Key Classes**:

##### `SerialFieldResult`
**What it represents**: Extracted serial number field with confidence and metadata
```python
@dataclass
class SerialFieldResult:
    value: Optional[str]                 # Extracted serial number text
    confidence: float                    # AI confidence score (0.0-1.0)
    extraction_status: FieldExtractionStatus  # Success/failure status
    bounding_regions: List[Dict[str, Any]] = field(default_factory=list)
    spans: List[Dict[str, Any]] = field(default_factory=list)
    
    def is_high_confidence(self, threshold: float = 0.7) -> bool:
        """Determines if extraction meets confidence threshold"""
        
    def get_normalized_value(self) -> Optional[str]:
        """Returns cleaned, standardized serial number format"""
```

**Status Values**:
- `SUCCESS`: Field extracted successfully with acceptable confidence
- `LOW_CONFIDENCE`: Field extracted but below confidence threshold
- `NOT_FOUND`: Field not detected in document
- `EXTRACTION_ERROR`: Technical error during field extraction

**Bounding Regions**: Geographic location of text in document
```python
{
    "page_number": 1,
    "polygon": [{"x": 100, "y": 200}, {"x": 300, "y": 200}, ...],
    "confidence": 0.85
}
```

**Spans**: Character-level text location information  
```python
{
    "offset": 1250,        # Character offset in document text
    "length": 12,          # Length of extracted text
    "text": "SN123456789"  # Raw extracted text
}
```

##### `DocumentAnalysisResponse`
**What it represents**: Complete analysis results with metadata and storage information
```python
@dataclass  
class DocumentAnalysisResponse:
    analysis_id: str                     # Unique analysis identifier
    status: AnalysisStatus              # Overall processing status
    serial_field: SerialFieldResult     # Primary extraction result
    model_used: str                     # Azure model identifier
    confidence_threshold: float         # Applied confidence threshold
    processing_time_ms: Optional[float] # Performance metrics
    blob_storage_info: Optional[Dict[str, str]]  # Storage details if applicable
    correlation_id: Optional[str]       # Request tracking ID
    created_at: datetime               # Analysis timestamp
    
    def to_dict(self) -> Dict[str, Any]:
        """Converts to JSON-serializable dictionary"""
        
    def is_storage_required(self) -> bool:
        """Determines if document should be stored for review"""
```

**Analysis Status Values**:
- `COMPLETED`: Analysis finished successfully
- `FAILED`: Analysis could not be completed
- `PENDING`: Analysis in progress (for async operations)

**Blob Storage Info Structure**:
```python
{
    "container_name": "warehouse-returns-doc-intel",
    "document_blob_path": "low-confidence/pending-review/2024/01/15/doc-12345/document.pdf",
    "metadata_blob_path": "low-confidence/pending-review/2024/01/15/doc-12345/metadata.json", 
    "storage_url": "https://storage.blob.core.windows.net/container/path",
    "stored_at": "2024-01-15T10:30:00.000Z"
}
```

### 3. `AzureDocumentIntelligenceModel.py` - Azure API Models

#### **Purpose**: Models for Azure Document Intelligence API request/response handling

#### **Key Classes**:

##### `AzureDocIntelResponse`
**What it represents**: Structured response from Azure Document Intelligence API
```python
@dataclass
class AzureDocIntelResponse:
    status: str                         # Azure analysis status
    analyze_result: Optional[Dict[str, Any]]  # Raw Azure analysis results
    model_id: str                       # Model used for analysis
    api_version: str                    # Azure API version
    created_date_time: Optional[datetime]  # Analysis start time
    last_updated_date_time: Optional[datetime]  # Analysis completion time
    
    def get_field_value(self, field_name: str) -> Optional[Dict[str, Any]]:
        """Extracts specific field from Azure results"""
        
    def get_confidence_score(self, field_name: str) -> float:
        """Gets confidence score for specific field"""
        
    def has_successful_analysis(self) -> bool:
        """Checks if Azure analysis completed successfully"""
```

**Azure Result Structure**: Maps to Azure Document Intelligence API response format
```python
{
    "status": "succeeded",
    "createdDateTime": "2024-01-15T10:30:00.000Z",
    "lastUpdatedDateTime": "2024-01-15T10:30:15.000Z", 
    "analyzeResult": {
        "apiVersion": "2024-11-30",
        "modelId": "serialnumber",
        "documents": [{
            "docType": "warehouse_return",
            "fields": {
                "Serial": {
                    "type": "string",
                    "valueString": "SN123456789",
                    "confidence": 0.85,
                    "boundingRegions": [...],
                    "spans": [...]
                }
            }
        }]
    }
}
```

### 4. `ErrorResponseModel.py` - Error Handling Models

#### **Purpose**: Standardized error responses with detailed context and correlation

#### **Key Classes**:

##### `ErrorCode` Enumeration
**What it represents**: Categorized error types for consistent error handling
```python
class ErrorCode(Enum):
    # Validation Errors
    INVALID_REQUEST = "invalid_request"
    INVALID_FILE_FORMAT = "invalid_file_format"
    FILE_TOO_LARGE = "file_too_large"
    
    # Processing Errors  
    DOCUMENT_INTELLIGENCE_ERROR = "document_intelligence_error"
    FIELD_EXTRACTION_ERROR = "field_extraction_error"
    CONFIDENCE_TOO_LOW = "confidence_too_low"
    
    # Storage Errors
    BLOB_STORAGE_ERROR = "blob_storage_error"
    STORAGE_UNAVAILABLE = "storage_unavailable"
    
    # System Errors
    INTERNAL_ERROR = "internal_error"
    SERVICE_UNAVAILABLE = "service_unavailable"
    TIMEOUT_ERROR = "timeout_error"
```

##### `ErrorResponse`
**What it represents**: Structured error information with actionable guidance
```python
@dataclass
class ErrorResponse:
    error_code: ErrorCode               # Categorized error type
    message: str                        # Human-readable error description
    details: Optional[str] = None       # Technical error details
    correlation_id: Optional[str] = None  # Request tracking ID
    suggested_action: Optional[str] = None  # User guidance for resolution
    timestamp: datetime = field(default_factory=datetime.utcnow)
    
    def to_dict(self) -> Dict[str, Any]:
        """Converts to JSON response format"""
        
    def to_http_response(self, status_code: int = 400) -> func.HttpResponse:
        """Creates Azure Functions HTTP response"""
```

**Error Response Examples**:

**Validation Error**:
```json
{
    "error_code": "invalid_file_format",
    "message": "Unsupported file format. Please upload PDF, JPEG, PNG, or TIFF files.",
    "details": "Received content type: application/msword",
    "correlation_id": "req-12345", 
    "suggested_action": "Convert document to PDF format and retry upload",
    "timestamp": "2024-01-15T10:30:00.000Z"
}
```

**Processing Error**:
```json
{
    "error_code": "document_intelligence_error",
    "message": "Document analysis failed due to service error",
    "details": "Azure Document Intelligence API returned HTTP 503",
    "correlation_id": "req-12345",
    "suggested_action": "Please retry the request in a few moments",
    "timestamp": "2024-01-15T10:30:00.000Z"
}
```

## 🔧 Model Usage Patterns

### Request Processing Flow
```python
# 1. Parse and validate input
request = DocumentAnalysisFileRequest(
    model_id="serialnumber",
    document_type="warehouse_return"
)
validation_errors = request.validate()
if validation_errors:
    return ErrorResponse(ErrorCode.INVALID_REQUEST, "Validation failed")

# 2. Process document  
azure_response = await doc_intel_service.analyze_document(file_data, request)

# 3. Convert to business model
serial_result = SerialFieldResult(
    value=azure_response.get_field_value("Serial"),
    confidence=azure_response.get_confidence_score("Serial"),
    extraction_status=FieldExtractionStatus.SUCCESS
)

# 4. Create response
response = DocumentAnalysisResponse(
    analysis_id=generate_id(),
    status=AnalysisStatus.COMPLETED,
    serial_field=serial_result,
    model_used=request.model_id
)
```

### Error Handling Pattern
```python
try:
    # Processing logic
    result = await process_document(request)
    return result.to_dict()
    
except ValidationException as e:
    error = ErrorResponse(
        error_code=ErrorCode.INVALID_REQUEST,
        message="Request validation failed",
        details=str(e),
        correlation_id=correlation_id
    )
    return error.to_http_response(400)
    
except AzureServiceException as e:
    error = ErrorResponse(
        error_code=ErrorCode.DOCUMENT_INTELLIGENCE_ERROR,
        message="Document analysis service error", 
        details=str(e),
        correlation_id=correlation_id,
        suggested_action="Please retry the request"
    )
    return error.to_http_response(503)
```

## 📊 Data Validation & Serialization

### Input Validation
- **Type Safety**: Dataclass type hints enforce correct types
- **Range Validation**: Confidence thresholds, file sizes within limits
- **Format Validation**: URLs, file extensions, model IDs
- **Business Rules**: Document types, model compatibility

### JSON Serialization  
- **Custom Serializers**: Handle datetime, enum, and complex types
- **Field Mapping**: Convert between internal and external field names
- **Null Handling**: Graceful handling of optional and missing fields
- **Backward Compatibility**: Support for API version evolution

### Response Standardization
All API responses follow consistent structure:
```json
{
    "success": true,
    "data": { /* business data */ },
    "metadata": {
        "correlation_id": "req-12345",
        "timestamp": "2024-01-15T10:30:00.000Z",
        "processing_time_ms": 1250
    },
    "error": null
}
```

This model framework ensures type safety, consistent validation, and clear data contracts across the entire Warehouse Returns system while providing flexibility for future enhancements and integrations.
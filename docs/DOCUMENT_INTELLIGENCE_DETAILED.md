# Document Intelligence Function App - Detailed Documentation

## 📋 Overview
The Document Intelligence Function App is the core component for processing warehouse return documents. It uses Azure Document Intelligence (formerly Form Recognizer) to extract serial numbers from various document formats with confidence scoring and automatic blob storage for low-confidence documents.

## 🏗️ File Structure & Components

```
src/document_intelligence/
├── function_app.py                    # Main Azure Functions entry point
├── services/
│   ├── document_intelligence_service.py    # Azure AI service integration
│   └── document_processing_service.py      # Business logic orchestration
├── repositories/
│   └── blob_storage_repository.py          # Blob storage operations
├── models/
│   ├── AzureDocumentIntelligenceModel.py   # Azure API response models
│   ├── DocumentAnalysisRequestModel.py     # Input request models
│   ├── DocumentAnalysisResponseModel.py    # Output response models
│   └── ErrorResponseModel.py               # Error handling models
└── tests/
    └── conftest.py                         # Test configuration
```

## 🔧 Core Functions Documentation

### 1. `function_app.py` - Main Entry Point

#### **Purpose**: Azure Functions HTTP triggers and application initialization

#### **Key Functions**:

##### `get_processing_service() -> DocumentProcessingService`
**What it does**: Creates and manages a singleton instance of the document processing service
- Initializes the service on first use
- Handles initialization errors gracefully
- Provides service-level logging with `[SERVICE-INIT]` prefix
- Returns the same instance for subsequent calls

**When it's used**: Called by all HTTP endpoints to get the processing service

##### `process_document(req: func.HttpRequest) -> func.HttpResponse`
**What it does**: Main document processing endpoint that handles both file uploads and URL-based requests
- **HTTP Method**: POST
- **Route**: `/api/process-document`
- **Authentication**: Function level
- **Input Formats**: 
  - JSON body with `document_url` for URL processing
  - Multipart form data for file uploads
  - Form fields: `document_content` (base64), `document_name`, `file_id`

**Processing Steps**:
1. Generate correlation ID for request tracking
2. Log incoming request with `[HTTP-REQUEST]` prefix
3. Determine request type (URL vs file upload vs form data)
4. Route to appropriate processing method
5. Handle errors and return standardized responses
6. Add security headers to all responses

**Example Request (Form Data)**:
```json
{
    "document_content": "base64-encoded-document-data",
    "document_name": "return_document.pdf", 
    "file_id": "RET-2024-001"
}
```

**Example Response**:
```json
{
    "analysis_id": "doc-12345",
    "status": "completed",
    "serial_field": {
        "value": "SN123456789",
        "confidence": 0.85,
        "extraction_status": "success"
    },
    "blob_storage_info": null
}
```

##### `get_analysis_result(req: func.HttpRequest) -> func.HttpResponse`
**What it does**: Retrieves previously stored analysis results by analysis ID
- **HTTP Method**: GET
- **Route**: `/api/analysis/{analysis_id}`
- **Authentication**: Function level

**Processing Steps**:
1. Extract analysis_id from URL path
2. Query blob storage repository for metadata
3. Return analysis results with storage information
4. Handle not-found scenarios gracefully

##### `health_check(req: func.HttpRequest) -> func.HttpResponse`
**What it does**: Provides health monitoring for the service and its dependencies
- **HTTP Method**: GET
- **Route**: `/api/health`
- **Authentication**: Anonymous

**Health Checks**:
1. Document Intelligence service connectivity
2. Blob storage accessibility  
3. Environment configuration validation
4. Service initialization status

**Response Format**:
```json
{
    "status": "healthy",
    "timestamp": "2024-01-15T10:30:00.000Z",
    "services": {
        "document_intelligence": {"status": "healthy"},
        "blob_storage": {"status": "healthy"}
    }
}
```

### 2. `services/document_processing_service.py` - Business Logic Orchestrator

#### **Purpose**: Orchestrates the complete document analysis workflow

#### **Key Functions**:

##### `__init__(doc_intel_service, blob_repository, confidence_threshold, enable_blob_storage)`
**What it does**: Initializes the processing service with all dependencies
- Sets up Azure Document Intelligence service
- Configures blob storage repository with correct container name (`warehouse-returns-doc-intel`)
- Loads configuration from environment variables
- Provides comprehensive initialization logging with `[BLOB-STORAGE-INIT]` prefix

**Configuration Loading**:
- `CONFIDENCE_THRESHOLD`: Default 0.7 for document acceptance
- `ENABLE_BLOB_STORAGE`: Controls low-confidence document storage  
- `BLOB_CONTAINER_PREFIX`: Container name for blob storage
- `AZURE_STORAGE_CONNECTION_STRING`: Storage account connection

##### `analyze_document_from_bytes(document_bytes, filename, content_type, correlation_id)`
**What it does**: Processes uploaded document files and returns analysis results
- Accepts binary document data with metadata
- Performs Azure Document Intelligence analysis
- Evaluates confidence scores against threshold
- Routes low-confidence documents to blob storage
- Returns comprehensive analysis results

**Processing Workflow**:
1. **Validation**: Check file size, content type, data integrity
2. **Analysis**: Send to Azure Document Intelligence API
3. **Evaluation**: Compare confidence against threshold (0.7)
4. **Decision Logic**: 
   - High confidence (≥0.7): Return results immediately
   - Low confidence (<0.7): Store in blob storage for review
5. **Response**: Include analysis results and storage information

**Blob Storage Decision Matrix**:
```
extraction_success=true  + confidence<0.7 + blob_enabled=true  → Store in blob
extraction_success=true  + confidence≥0.7 + any_blob_setting  → Return immediately  
extraction_success=false + any_confidence + any_blob_setting  → Return error
```

##### `analyze_document_from_url(document_url, request, correlation_id)`
**What it does**: Processes documents from URLs (currently returns mock data for testing)
- Downloads document from provided URL
- Processes similar to file upload workflow
- Includes URL validation and accessibility checks

**Logging Strategy**:
- `[BLOB-STORAGE-DECISION]`: Documents confidence evaluation and storage decisions
- `[AZURE-API-REQUEST]`: Tracks Azure Document Intelligence API calls  
- `[PROCESSING-ERROR]`: Captures processing failures with detailed context
- `[PROCESSING-SUCCESS]`: Logs successful analysis completion

### 3. `services/document_intelligence_service.py` - Azure AI Integration

#### **Purpose**: Handles direct integration with Azure Document Intelligence API

#### **Key Functions**:

##### `__init__(endpoint, api_key, api_version, default_model_id)`
**What it does**: Initializes Azure Document Intelligence client
- Creates authenticated client using Azure Key Credential
- Configures API version and default model settings
- Validates connection parameters
- Sets up retry configuration for API calls

##### `analyze_document_from_bytes(document_bytes, request, filename, content_type, correlation_id)`
**What it does**: Sends document to Azure Document Intelligence and processes response
- Prepares document for API submission
- Configures analysis request with model ID and options
- Handles API responses and errors
- Converts Azure response to internal model format

**API Integration Details**:
- **Model**: Uses custom "serialnumber" model trained for warehouse documents
- **Fields**: Extracts "Serial" field with confidence scoring
- **Formats**: Supports PDF, JPEG, PNG, TIFF document types
- **Timeout**: 300 seconds for analysis completion
- **Retry**: 3 attempts with exponential backoff

##### `_convert_azure_response(azure_result)`
**What it does**: Converts Azure API response to internal response model
- Extracts field values and confidence scores
- Processes bounding regions and text spans
- Handles missing or malformed response data
- Provides structured field extraction results

### 4. `repositories/blob_storage_repository.py` - Storage Operations

#### **Purpose**: Manages Azure Blob Storage operations for low-confidence documents

#### **Key Functions**:

##### `__init__(connection_string, container_name, max_retry_attempts, retry_delay_seconds)`
**What it does**: Initializes blob storage client and configuration
- Creates Azure Blob Service Client from connection string
- Sets container name (now correctly uses `warehouse-returns-doc-intel`)
- Configures retry behavior for storage operations
- Validates environment variables and connectivity
- Provides detailed initialization logging with `[BLOB-REPO-CONFIG]`

##### `store_low_confidence_document(analysis_id, document_data, filename, content_type, analysis_metadata, correlation_id)`
**What it does**: Stores low-confidence documents with organized structure and metadata
- **Storage Path**: `low-confidence/pending-review/{date}/{analysis_id}/`
- **Files Created**:
  - `document.{ext}`: Original document file
  - `metadata.json`: Complete analysis metadata and results

**Storage Process**:
1. **Container Check**: Ensures container exists, creates if needed
2. **Path Generation**: Creates date-organized storage paths
3. **Metadata Preparation**: Builds comprehensive metadata JSON
4. **Upload**: Stores both document and metadata with retry logic
5. **Verification**: Confirms successful storage and returns storage info

**Metadata Structure**:
```json
{
    "analysis_id": "doc-12345",
    "original_filename": "return_doc.pdf", 
    "content_type": "application/pdf",
    "file_size_bytes": 1048576,
    "stored_at": "2024-01-15T10:30:00.000Z",
    "correlation_id": "req-abcd-1234",
    "status": "pending_review",
    "analysis_results": {
        "serial_field": {
            "value": "SN123",
            "confidence": 0.65
        }
    },
    "storage_paths": {
        "document": "low-confidence/pending-review/2024/01/15/doc-12345/document.pdf",
        "metadata": "low-confidence/pending-review/2024/01/15/doc-12345/metadata.json"
    }
}
```

##### `retrieve_document_metadata(analysis_id, correlation_id)`
**What it does**: Retrieves stored document metadata by analysis ID
- Searches across different storage paths (pending-review, reviewed, retraining)
- Downloads and parses metadata JSON files
- Returns structured metadata for analysis results
- Handles not-found scenarios gracefully

##### `list_pending_review_documents(days_back, correlation_id)`
**What it does**: Lists documents awaiting manual review
- Scans pending-review storage path for documents
- Filters by date range (configurable days back)
- Returns summary information for each document
- Supports administrative review workflows

##### `_ensure_container_exists()`
**What it does**: Ensures storage container exists and creates if needed
- Checks for container existence
- Creates container with appropriate permissions
- Logs container operations with `[BLOB-REPO-CONTAINER]` prefix
- Handles Azure storage exceptions

**Retry Strategy**:
- **Max Attempts**: 3 retries for transient failures
- **Backoff**: Exponential delay (2, 4, 8 seconds)
- **Errors Handled**: Network timeouts, Azure service errors, throttling
- **Logging**: Detailed retry attempt logging with `[BLOB-REPO-STORE]`

## 📊 Integration Patterns

### Request Flow
```
HTTP Request → function_app.py → DocumentProcessingService → DocumentIntelligenceService → Azure AI API
                    ↓                        ↓                           ↓
            Security Headers    Confidence Evaluation    BlobStorageRepository → Azure Blob Storage
                    ↓                        ↓                           ↓
            Structured Response ← Business Logic ← Storage Confirmation ← Storage Success
```

### Error Handling
- **Validation Errors**: Invalid input, file size, content type
- **Service Errors**: Azure API failures, network timeouts
- **Storage Errors**: Blob storage connectivity, permissions
- **Business Errors**: Low confidence, extraction failures

### Logging Strategy
- **Request Tracking**: Correlation IDs throughout request lifecycle
- **Service Boundaries**: Clear logging at service entry/exit points  
- **Error Context**: Comprehensive error details with correlation
- **Business Events**: Document analysis outcomes, storage decisions

## 🔧 Configuration & Environment

### Required Environment Variables
```bash
# Azure Document Intelligence
DOCUMENT_INTELLIGENCE_ENDPOINT=https://your-service.cognitiveservices.azure.com/
DOCUMENT_INTELLIGENCE_KEY=your-api-key
DOCUMENT_INTELLIGENCE_API_VERSION=2024-11-30
DEFAULT_MODEL_ID=serialnumber

# Blob Storage  
AZURE_STORAGE_CONNECTION_STRING=DefaultEndpointsProtocol=https;AccountName=...
BLOB_CONTAINER_PREFIX=warehouse-returns-doc-intel
ENABLE_BLOB_STORAGE=true

# Processing Configuration
CONFIDENCE_THRESHOLD=0.7
MAX_FILE_SIZE_MB=50
SUPPORTED_CONTENT_TYPES=application/pdf,image/jpeg,image/png,image/tiff

# Logging & Monitoring
LOG_LEVEL=INFO
ENABLE_STRUCTURED_LOGGING=true
APPINSIGHTS_INSTRUMENTATIONKEY=your-app-insights-key
```

This comprehensive system provides robust document processing with automatic quality evaluation and storage management for continuous improvement workflows.
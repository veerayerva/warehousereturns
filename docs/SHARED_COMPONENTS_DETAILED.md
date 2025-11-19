# Shared Components - Detailed Documentation

## 📋 Overview
The shared components provide common utilities, configurations, and middleware used across all Azure Function Apps in the Warehouse Returns system. These components ensure consistency, maintainability, and proper observability across the entire application.

## 🏗️ File Structure & Components

```
src/shared/
├── config/
│   ├── __init__.py
│   └── logging_config.py          # Centralized logging framework
├── middleware/
│   ├── __init__.py
│   └── logging_middleware.py      # HTTP request/response logging
├── exceptions/                    # Custom exception types (future)
└── utils/                        # Common utility functions (future)
```

## 🔧 Core Components Documentation

### 1. `config/logging_config.py` - Centralized Logging Framework

#### **Purpose**: Provides structured, consistent logging across all function apps with business event tracking and correlation

#### **Key Classes & Functions**:

##### `WarehouseReturnsLogger` Class
**What it does**: Extended logger class with warehouse-specific business event logging
- Inherits from standard Python logger
- Adds business event tracking capabilities
- Provides structured logging with consistent formats
- Supports correlation ID tracking across service boundaries

**Key Methods**:

##### `get_logger(name: str) -> WarehouseReturnsLogger`
**What it does**: Factory function to create configured logger instances
- Creates logger with warehouse returns naming convention
- Applies consistent formatting and handlers
- Configures log levels from environment variables
- Sets up Application Insights integration if available

**Usage Example**:
```python
from shared.config.logging_config import get_logger

logger = get_logger('warehouse_returns.document_intelligence')
logger.info("Document processing started", correlation_id="req-12345")
```

##### `log_business_event(event_name, entity_type, entity_id, correlation_id, properties)`
**What it does**: Logs structured business events for analytics and monitoring
- **Event Tracking**: Captures key business workflow events
- **Entity Context**: Associates events with business entities (documents, returns, pieces)
- **Property Enrichment**: Adds custom properties for detailed analysis
- **Correlation**: Links events across service boundaries

**Business Event Examples**:
```python
# Document processing events
logger.log_business_event(
    event_name="document_processing_started",
    entity_type="document", 
    entity_id="doc-12345",
    correlation_id="req-abcd",
    properties={"file_size": 1048576, "content_type": "application/pdf"}
)

# Return workflow events  
logger.log_business_event(
    event_name="return_status_changed",
    entity_type="return",
    entity_id="RET-2024-001", 
    correlation_id="req-xyz",
    properties={"old_status": "pending", "new_status": "approved"}
)
```

##### `log_function_entry(function_name, parameters)` & `log_function_exit(function_name, result, duration_ms)`
**What it does**: Provides consistent function-level logging for performance monitoring
- **Entry Logging**: Captures function start with parameters
- **Exit Logging**: Captures completion with results and duration
- **Performance Tracking**: Measures execution time automatically
- **Parameter Sanitization**: Safely logs parameters without sensitive data

**Usage Pattern**:
```python
def process_document(document_data, correlation_id):
    logger.log_function_entry("process_document", {"data_size": len(document_data)})
    start_time = time.time()
    
    try:
        # Function logic here
        result = perform_analysis(document_data)
        
        duration_ms = (time.time() - start_time) * 1000
        logger.log_function_exit("process_document", result, duration_ms)
        return result
    except Exception as e:
        logger.error("Function failed", function_name="process_document", error=str(e))
        raise
```

##### `log_api_request(method, url, status_code, duration_ms, correlation_id)` & `log_api_response(...)`
**What it does**: Structured logging for HTTP API calls (both inbound and outbound)
- **Request Logging**: Captures HTTP method, URL, headers, correlation ID
- **Response Logging**: Records status codes, response times, content types
- **Performance Metrics**: Tracks API call duration and throughput
- **Error Context**: Detailed error information for failed requests

#### **Configuration Features**:

##### Environment-Based Configuration
**What it does**: Automatically configures logging based on environment variables
```bash
LOG_LEVEL=INFO                    # Controls log verbosity  
ENABLE_STRUCTURED_LOGGING=true   # Enables JSON-formatted logs
APPINSIGHTS_INSTRUMENTATIONKEY=key # Application Insights integration
WAREHOUSE_RETURNS_ENV=development # Environment context in logs
```

##### Application Insights Integration
**What it does**: Seamlessly integrates with Azure Application Insights for centralized monitoring
- **Automatic Setup**: Configures OpenCensus tracing integration
- **Custom Metrics**: Sends business events as custom telemetry
- **Dependency Tracking**: Tracks external API calls and Azure service usage
- **Performance Counters**: Monitors function execution performance

##### Correlation ID Management
**What it does**: Provides consistent request tracking across all components
- **Generation**: Creates unique correlation IDs for each request
- **Propagation**: Passes correlation IDs through all service calls
- **Logging**: Includes correlation IDs in all log entries
- **Tracing**: Links related log entries across service boundaries

### 2. `middleware/logging_middleware.py` - HTTP Request/Response Logging

#### **Purpose**: Provides consistent HTTP request and response logging for Azure Functions

#### **Key Classes & Functions**:

##### `LoggingMiddleware` Class
**What it does**: Middleware that wraps Azure Function HTTP handlers with comprehensive logging
- Logs incoming HTTP requests with headers and parameters
- Tracks request processing time and performance
- Logs outgoing HTTP responses with status codes
- Handles errors and exceptions during request processing

**Key Methods**:

##### `__init__(function_name: str)`
**What it does**: Initializes middleware for a specific Azure Function
- Sets up function-specific logger instance
- Configures correlation ID generation
- Prepares request/response logging templates
- Integrates with centralized logging configuration

##### `log_request(req: func.HttpRequest, correlation_id: str)`
**What it does**: Logs detailed information about incoming HTTP requests
- **Request Method**: HTTP verb (GET, POST, PUT, etc.)
- **URL Path**: Function route and query parameters
- **Headers**: Security-safe header logging (excludes sensitive data)
- **Content**: Request body size and content type
- **Client Info**: User agent and IP address (when available)

**Log Format Example**:
```
[HTTP-REQUEST] POST /api/process-document - Method: POST, Content-Type: application/json, Content-Length: 1024, Correlation-ID: req-12345, User-Agent: RestClient/1.0
```

##### `log_response(response: func.HttpResponse, duration_ms: float, correlation_id: str)`
**What it does**: Logs HTTP response information and performance metrics
- **Status Code**: HTTP response status (200, 400, 500, etc.)
- **Response Size**: Content length and type
- **Duration**: Request processing time in milliseconds
- **Headers**: Security-safe response header logging

**Log Format Example**:
```
[HTTP-RESPONSE] 200 OK - Status: 200, Content-Type: application/json, Duration: 1250ms, Correlation-ID: req-12345
```

##### `create_http_logging_wrapper(function_name: str)`
**What it does**: Decorator factory that creates HTTP logging wrappers for Azure Functions
- Automatically wraps function handlers with logging middleware
- Generates correlation IDs for each request
- Measures request processing time
- Handles exceptions and error responses

**Usage Example**:
```python
from shared.middleware.logging_middleware import create_http_logging_wrapper

@app.route(route="process-document", methods=["POST"])
@create_http_logging_wrapper("ProcessDocument")
def process_document(req: func.HttpRequest) -> func.HttpResponse:
    # Function logic here - logging is automatic
    return func.HttpResponse("Success")
```

#### **Security & Privacy Features**:

##### Sensitive Data Filtering
**What it does**: Automatically filters sensitive information from logs
- **Headers**: Excludes Authorization, API keys, authentication tokens
- **Parameters**: Filters password fields, personal data, financial information
- **Content**: Logs content size instead of actual content for binary data
- **URLs**: Sanitizes URLs to remove embedded credentials

##### Compliance Support
**What it does**: Supports regulatory compliance requirements
- **Data Retention**: Configurable log retention periods
- **PII Protection**: Automatic detection and masking of personal information
- **Audit Trails**: Complete request/response audit logging
- **Access Control**: Secure logging with appropriate access restrictions

## 🔧 Integration Patterns

### Function App Integration
All function apps follow this pattern for consistent logging:

```python
# 1. Import shared components
from shared.config.logging_config import get_logger, log_function_calls
from shared.middleware.logging_middleware import create_http_logging_wrapper

# 2. Create logger instance
logger = get_logger('warehouse_returns.function_name')

# 3. Apply middleware to HTTP functions
@app.route(route="endpoint", methods=["POST"])
@create_http_logging_wrapper("FunctionName")
@log_function_calls("module.function_name")
def azure_function(req: func.HttpRequest) -> func.HttpResponse:
    # Function implementation
    pass
```

### Cross-Service Communication
For external API calls and service-to-service communication:

```python
# Propagate correlation IDs
correlation_id = req.headers.get('X-Correlation-ID') or generate_correlation_id()

# Log outbound API calls
logger.log_api_request("GET", external_url, correlation_id=correlation_id)

# Make API call with correlation header
headers = {"X-Correlation-ID": correlation_id}
response = await http_client.get(external_url, headers=headers)

# Log API response
logger.log_api_response("GET", external_url, response.status_code, duration_ms, correlation_id)
```

## 📊 Monitoring & Observability

### Log Structure
All logs follow a consistent JSON structure:
```json
{
    "timestamp": "2024-01-15T10:30:00.000Z",
    "level": "INFO", 
    "logger": "warehouse_returns.document_intelligence",
    "message": "[HTTP-REQUEST] Document processing started",
    "correlation_id": "req-12345",
    "function_name": "ProcessDocument",
    "properties": {
        "method": "POST",
        "content_type": "application/json",
        "content_length": 1024
    }
}
```

### Business Event Analytics
Business events support operational analytics:
```json
{
    "event_name": "document_processing_completed",
    "entity_type": "document", 
    "entity_id": "doc-12345",
    "correlation_id": "req-12345",
    "properties": {
        "confidence_score": 0.85,
        "processing_time_ms": 1250,
        "model_used": "serialnumber",
        "blob_stored": false
    }
}
```

### Performance Monitoring
Function performance tracking:
- **Request Duration**: End-to-end request processing time
- **Function Duration**: Individual function execution time  
- **API Call Duration**: External dependency response times
- **Error Rates**: Success/failure ratios and error categorization

## 🔧 Configuration Management

### Environment Variables
```bash
# Logging Configuration
LOG_LEVEL=INFO                          # DEBUG, INFO, WARNING, ERROR
ENABLE_STRUCTURED_LOGGING=true         # JSON vs plain text logging
ENABLE_REQUEST_LOGGING=true            # HTTP request/response logging
ENABLE_BUSINESS_EVENTS=true            # Business event tracking

# Application Insights
APPINSIGHTS_INSTRUMENTATIONKEY=key     # Azure monitoring integration
APPLICATIONINSIGHTS_CONNECTION_STRING=conn_string

# Environment Context
WAREHOUSE_RETURNS_ENV=development      # Environment identifier
AZURE_FUNCTIONS_ENVIRONMENT=Development # Azure Functions context
```

### Customization Options
- **Log Formatters**: Custom formatting for different environments
- **Filter Rules**: Environment-specific filtering for sensitive data
- **Retention Policies**: Configurable log retention and archival
- **Integration Endpoints**: Custom telemetry and monitoring integrations

This shared components framework ensures consistent, observable, and maintainable logging across the entire Warehouse Returns system while providing the flexibility to customize behavior per environment and use case.
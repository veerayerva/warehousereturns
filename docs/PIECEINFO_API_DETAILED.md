# PieceInfo API Function App - Detailed Documentation

## 📋 Overview
The PieceInfo API Function App provides warehouse piece information aggregation services. It combines data from multiple external sources to provide comprehensive piece details including inventory location, product information, and vendor details.

## 🏗️ File Structure & Components

```
src/pieceinfo_api/
├── function_app.py                # Main Azure Functions entry point
├── services/
│   ├── aggregation_service.py     # Business logic for data aggregation
│   └── http_client.py            # HTTP client for external API calls
├── local.settings.json           # Local development configuration
└── requirements.txt              # Python dependencies
```

## 🔧 Core Functions Documentation

### 1. `function_app.py` - Main Entry Point

#### **Purpose**: Provides REST API endpoints with Swagger documentation for piece information retrieval

#### **Key Functions**:

##### `get_swagger_doc(req: func.HttpRequest) -> func.HttpResponse`
**What it does**: Returns OpenAPI/Swagger specification for the API
- **HTTP Method**: GET
- **Route**: `/api/swagger`
- **Authentication**: Anonymous
- **Response**: Complete OpenAPI 3.0 specification in JSON format

**Swagger Specification Includes**:
- API metadata (title, version, description)
- Server information and base URLs
- Security schemes and authentication
- Complete endpoint documentation
- Request/response schemas
- Error response formats

##### `swagger_ui(req: func.HttpRequest) -> func.HttpResponse`
**What it does**: Serves interactive Swagger UI documentation page
- **HTTP Method**: GET  
- **Route**: `/api/docs`
- **Authentication**: Anonymous
- **Response**: HTML page with embedded Swagger UI

**Features**:
- Interactive API testing interface
- Request/response examples
- Schema validation
- Try-it-out functionality for all endpoints

##### `get_piece_info(req: func.HttpRequest) -> func.HttpResponse`
**What it does**: Main API endpoint that aggregates piece information from multiple sources
- **HTTP Method**: GET
- **Route**: `/api/piece/{piece_number}`
- **Authentication**: Function level
- **Parameters**: 
  - `piece_number` (path): The warehouse piece identifier
  - `include_vendor` (query, optional): Include vendor details
  - `include_location` (query, optional): Include inventory location

**Processing Steps**:
1. **Validation**: Validate piece number format and required parameters
2. **Aggregation**: Call aggregation service to collect data from multiple sources
3. **Filtering**: Apply optional filters based on query parameters
4. **Response**: Return consolidated piece information

**Example Request**:
```
GET /api/piece/WH-12345?include_vendor=true&include_location=true
```

**Example Response**:
```json
{
    "piece_number": "WH-12345",
    "description": "Hydraulic Pump Assembly",
    "model": "HP-2000X",
    "brand": "Industrial Solutions Inc",
    "category": "Hydraulic Components",
    "location": {
        "warehouse": "Building-A",
        "rack": "A-15-C",
        "serial_numbers": ["SN123456", "SN123457"]
    },
    "vendor": {
        "name": "Parts Supplier Corp",
        "contact": "support@partssupplier.com",
        "phone": "+1-555-0199"
    },
    "metadata": {
        "last_updated": "2024-01-15T10:30:00.000Z",
        "data_sources": ["inventory_api", "product_master", "vendor_api"]
    }
}
```

##### `health_check(req: func.HttpRequest) -> func.HttpResponse`
**What it does**: Provides health monitoring for the API and external dependencies
- **HTTP Method**: GET
- **Route**: `/api/health`
- **Authentication**: Anonymous

**Health Checks Include**:
- Service availability and response time
- External API connectivity
- Configuration validation
- Memory and performance metrics

**Response Format**:
```json
{
    "status": "healthy",
    "timestamp": "2024-01-15T10:30:00.000Z",
    "version": "1.0.0",
    "dependencies": {
        "inventory_api": {"status": "healthy", "response_time_ms": 150},
        "product_master_api": {"status": "healthy", "response_time_ms": 200},
        "vendor_api": {"status": "healthy", "response_time_ms": 180}
    }
}
```

### 2. `services/aggregation_service.py` - Business Logic

#### **Purpose**: Orchestrates data collection from multiple external APIs and aggregates results

#### **Key Functions**:

##### `SimpleAggregationService.__init__(http_client)`
**What it does**: Initializes the aggregation service with HTTP client dependency
- Sets up external API endpoints from environment configuration
- Configures timeout and retry settings
- Initializes logging for service operations
- Validates required configuration parameters

##### `get_piece_info_async(piece_number, include_vendor, include_location, correlation_id)`
**What it does**: Asynchronously aggregates piece information from multiple data sources
- **Data Sources**:
  - **Inventory API**: Warehouse location and serial number data
  - **Product Master API**: Product descriptions, models, brands, categories
  - **Vendor API**: Supplier contact information and policies

**Aggregation Process**:
1. **Parallel Requests**: Makes concurrent API calls to all data sources
2. **Error Handling**: Handles individual API failures gracefully
3. **Data Merging**: Combines results from successful API calls
4. **Validation**: Validates data completeness and consistency
5. **Enrichment**: Adds metadata about data sources and freshness

**Retry Logic**:
- **Max Attempts**: 3 retries per API call
- **Backoff Strategy**: Exponential backoff with jitter
- **Timeout**: 30 seconds per API call
- **Circuit Breaker**: Temporary failure detection and recovery

##### `_fetch_inventory_data(piece_number, correlation_id)`
**What it does**: Retrieves inventory location and serial number information
- Calls external inventory management system API
- Extracts warehouse, rack location, and available serial numbers
- Handles inventory-specific error conditions
- Returns structured inventory data or null if unavailable

##### `_fetch_product_data(piece_number, correlation_id)`
**What it does**: Retrieves product master information
- Queries product catalog API for piece details
- Extracts descriptions, models, brands, and categories
- Handles product lookup failures and missing data
- Returns enriched product information

##### `_fetch_vendor_data(piece_number, correlation_id)`
**What it does**: Retrieves vendor and supplier information
- Looks up vendor details from supplier management system
- Extracts contact information, addresses, and policies
- Handles vendor API connectivity issues
- Returns vendor contact details and business information

### 3. `services/http_client.py` - External API Communication

#### **Purpose**: Provides reliable HTTP communication with external APIs using SSL/HTTPS

#### **Key Functions**:

##### `SecureHttpClient.__init__(timeout, max_retries, ssl_verify)`
**What it does**: Initializes secure HTTP client with SSL/TLS configuration
- **SSL Configuration**: Enforces HTTPS for all external API calls
- **Certificate Validation**: Validates server certificates by default
- **Timeout Management**: Configurable connection and read timeouts
- **Session Management**: Maintains connection pooling for performance

**Security Features**:
- **TLS 1.2+**: Enforces modern TLS versions
- **Certificate Pinning**: Optional certificate validation
- **Request Signing**: Support for API key authentication
- **Rate Limiting**: Respects external API rate limits

##### `get_async(url, headers, params, correlation_id)`
**What it does**: Makes secure HTTP GET requests to external APIs
- **URL Validation**: Validates HTTPS URLs and prevents HTTP downgrade
- **Header Management**: Adds authentication headers and user agent
- **Parameter Encoding**: Properly encodes query parameters
- **Response Handling**: Validates status codes and content types

**Request Process**:
1. **URL Security Check**: Ensures HTTPS protocol
2. **Authentication**: Adds API keys and tokens to headers
3. **Request Logging**: Logs outbound requests with correlation IDs
4. **Response Validation**: Checks status codes and content types
5. **Error Handling**: Converts HTTP errors to structured exceptions

##### `post_async(url, data, headers, correlation_id)`
**What it does**: Makes secure HTTP POST requests with JSON payloads
- Similar security and validation features as GET requests
- **Content Encoding**: Handles JSON serialization and content-type headers
- **Request Size Validation**: Prevents oversized request payloads
- **Response Processing**: Handles various response content types

**Error Handling**:
- **Network Errors**: Connection timeouts, DNS failures
- **HTTP Errors**: 4xx client errors, 5xx server errors  
- **SSL Errors**: Certificate validation failures
- **Timeout Errors**: Request timeout and read timeout handling

## 📊 Integration Architecture

### API Integration Flow
```
Client Request → PieceInfo API → Aggregation Service → HTTP Client → External APIs
                      ↓                ↓                    ↓
              Swagger Documentation   Data Merging    SSL/HTTPS Security
                      ↓                ↓                    ↓  
              JSON Response ← Consolidated Data ← API Responses ← Multiple Sources
```

### Data Sources Integration
- **Inventory API**: Real-time warehouse location and serial number tracking
- **Product Master API**: Centralized product catalog with specifications
- **Vendor API**: Supplier contact information and business details
- **Future Extensions**: Additional APIs can be integrated through the aggregation service

## 🔧 Configuration & Environment

### Required Environment Variables
```bash
# API Configuration
PIECEINFO_API_VERSION=1.0.0
LOG_LEVEL=INFO
CORS_ALLOWED_ORIGINS=*

# External API Endpoints
INVENTORY_API_ENDPOINT=https://inventory.warehouse.com/api
PRODUCT_MASTER_API_ENDPOINT=https://catalog.products.com/api  
VENDOR_API_ENDPOINT=https://vendors.suppliers.com/api

# Authentication
INVENTORY_API_KEY=your-inventory-api-key
PRODUCT_API_KEY=your-product-api-key
VENDOR_API_KEY=your-vendor-api-key

# HTTP Client Configuration
HTTP_CLIENT_TIMEOUT=30
HTTP_CLIENT_MAX_RETRIES=3
SSL_VERIFY=true

# Monitoring
APPINSIGHTS_INSTRUMENTATIONKEY=your-app-insights-key
ENABLE_REQUEST_LOGGING=true
```

### API Authentication
- **Function Level**: Azure Functions authentication for API endpoints
- **API Keys**: External API authentication using secure key management
- **Correlation IDs**: Request tracking across all service boundaries
- **Rate Limiting**: Respectful API usage with retry and backoff

## 🔍 Monitoring & Observability

### Logging Strategy
- **Request/Response Logging**: Complete API call tracing
- **Performance Metrics**: Response times and throughput
- **Error Tracking**: Detailed error context and correlation
- **Business Metrics**: Piece lookup success rates and data completeness

### Health Monitoring
- **Service Health**: API availability and response times
- **Dependency Health**: External API connectivity and performance
- **Resource Usage**: Memory, CPU, and network utilization
- **Error Rates**: Service error rates and failure patterns

This PieceInfo API provides a robust foundation for warehouse piece information services with comprehensive external system integration and security features.
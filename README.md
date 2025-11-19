# PieceInfo API - Production-Ready Azure Functions Application

A comprehensive, production-ready Azure Functions application that aggregates piece information from multiple external APIs with advanced error handling, monitoring, and security features.

## 🏗️ Project Architecture

```
warehouse-returns/
├── .github/                                    # GitHub workflows and CI/CD
│   └── workflows/
│       ├── ci.yml                             # Continuous integration
│       └── deploy.yml                         # Deployment pipeline
│
├── src/pieceinfo_api/                         # Main application source code
│   ├── function_app.py                        # Azure Functions entry point with 4 endpoints
│   ├── services/                              # Business logic layer
│   │   ├── aggregation_service.py             # Orchestrates API calls and data aggregation
│   │   └── http_client.py                     # HTTP client with retry, SSL, monitoring
│   ├── config/                                # Configuration management
│   │   ├── ssl_config.py                      # SSL/TLS security configuration
│   │   └── debug_config.py                    # Development debugging utilities
│   ├── host.json                              # Azure Functions runtime configuration
│   └── local.settings.json                    # Local development settings
│
├── tests/                                     # Comprehensive test suite
│   ├── conftest.py                           # Pytest fixtures and configuration
│   ├── test_http_client.py                   # HTTP client unit tests (25+ test cases)
│   ├── test_aggregation_service.py           # Service layer unit tests (20+ test cases)
│   ├── test_api_endpoints.py                 # API integration tests (15+ test cases)
│   └── pytest.ini                            # Test configuration and coverage settings
│
├── scripts/                                   # Build and deployment automation
│   ├── build.bat                             # Windows build script with validation
│   ├── build.sh                              # Linux/macOS build script with validation
│   ├── deploy.py                             # Azure deployment automation
│   └── verify_models.py                      # Data model validation utility
│
├── docs/                                      # Comprehensive documentation
│   ├── IMPLEMENTATION_SUMMARY.md             # Technical implementation details
│   └── architecture.md                       # System architecture overview
│
├── infrastructure/                            # Infrastructure as Code
│   ├── bicep/                                # Azure Bicep templates
│   └── terraform/                            # Terraform configurations
│
├── .env.example                              # Environment variables template
├── requirements.txt                          # Python dependencies with production packages
├── .gitignore                                # Git ignore patterns
└── README.md                                 # This comprehensive guide
```

## 🚀 Application Overview

### PieceInfo API - Production-Ready Features

**Core Purpose**: Aggregate comprehensive piece information from multiple external APIs with enterprise-grade reliability, security, and monitoring.

**Key Capabilities**:
- 🔄 **API Orchestration**: Sequential calls to 3 external APIs with intelligent error handling
- 🛡️ **Security**: SSL/TLS configuration, input validation, security headers, subscription key authentication
- ⚡ **Performance**: Connection pooling, retry logic with exponential backoff, correlation tracking
- 📊 **Monitoring**: Comprehensive logging, performance metrics, health checks with dependency validation
- 🧪 **Testing**: 60+ automated tests covering unit, integration, and API scenarios
- 🚀 **Production Ready**: Build scripts, deployment automation, configuration management

**External API Integration**:
1. **Piece Inventory Location API** - Warehouse location, rack position, serial numbers
2. **Product Master API** - Product descriptions, models, categories, brands  
3. **Vendor Details API** - Vendor information, contacts, policies, addresses

**Azure Functions Endpoints**:
- `GET /api/pieces/{piece_number}` - Aggregate piece information (main endpoint)
- `GET /api/pieces/health` - Health check with dependency validation
- `GET /api/swagger` - API documentation (OpenAPI/Swagger spec)  
- `GET /api/docs` - Interactive Swagger UI documentation

## 🛠️ Development Setup

### Quick Start Guide

#### Prerequisites
- **Python 3.11+** (Application built and tested with Python 3.11.3)
- **Azure Functions Core Tools v4+** (For local development and deployment)
- **Azure CLI** (For authentication and Azure resource management)
- **Git** (For version control)
- **VS Code** (Recommended IDE with Azure Functions extension)

#### 1. Repository Setup
```bash
# Clone the repository
git clone <repository-url>
cd WarehouseReturns

# Create and activate virtual environment
python -m venv .venv
.venv\Scripts\activate          # Windows
# or
source .venv/bin/activate       # Linux/macOS
```

#### 2. Dependencies Installation
```bash
# Install production and development dependencies
pip install -r requirements.txt

# Verify Azure Functions Core Tools
func --version
```

#### 3. Environment Configuration
```bash
# Copy example environment file
copy .env.example .env          # Windows  
# or
cp .env.example .env            # Linux/macOS

# Edit .env file with your configuration:
# - OCP_APIM_SUBSCRIPTION_KEY: Your API subscription key
# - EXTERNAL_API_BASE_URL: Base URL for external APIs (default: https://apim-dev.nfm.com)
# - SSL_VERIFY: Enable/disable SSL verification (true for production, false for development)
```

#### 4. Application Insights Configuration

**For Local Development:**

Edit `src/pieceinfo_api/local.settings.json` and update the Application Insights connection string:

```json
{
  "Values": {
    "APPLICATIONINSIGHTS_CONNECTION_STRING": "InstrumentationKey=your-instrumentation-key;IngestionEndpoint=https://your-region.in.applicationinsights.azure.com/",
    "OCP_APIM_SUBSCRIPTION_KEY": "your-api-subscription-key",
    "EXTERNAL_API_BASE_URL": "https://apim-dev.nfm.com"
  }
}
```

**Get Your Application Insights Connection String:**

```bash
# Option 1: From Azure Portal
# Go to Azure Portal → Application Insights → Overview → Connection String

# Option 2: Using Azure CLI
az monitor app-insights component show \
    --app "YourAppInsightsName" \
    --resource-group "YourResourceGroup" \
    --query connectionString -o tsv
```

**For Azure Deployment:**

Application Insights is automatically configured when you create the Function App with the `--app-insights` parameter:

```bash
# Create Function App with Application Insights
az functionapp create \
    --resource-group MyResourceGroup \
    --consumption-plan-location eastus \
    --runtime python \
    --runtime-version 3.11 \
    --functions-version 4 \
    --name MyPieceInfoAPI \
    --storage-account mystorageaccount \
    --app-insights MyAppInsights  # Automatically configures logging
```

**Manual Application Insights Configuration (if needed):**

```bash
# Set Application Insights connection string for existing Function App
az functionapp config appsettings set \
    --name MyPieceInfoAPI \
    --resource-group MyResourceGroup \
    --settings APPLICATIONINSIGHTS_CONNECTION_STRING="your-connection-string"
```

**Application Insights Configuration Files:**

The application is pre-configured for optimal Application Insights integration:

1. **`host.json`** - Logging configuration with sampling and log levels:
   ```json
   {
     "logging": {
       "applicationInsights": {
         "samplingSettings": {
           "isEnabled": true,
           "maxTelemetryItemsPerSecond": 20
         }
       }
     }
   }
   ```

2. **`function_app.py`** - Includes correlation tracking and structured logging:
   - Automatic correlation ID generation for request tracing
   - Performance monitoring with execution timing
   - Structured error logging with context
   - Security headers and request validation logging

#### 4. Build and Validation
```bash
# Run comprehensive build with validation
scripts\build.bat               # Windows
# or
./scripts/build.sh              # Linux/macOS

# Build options:
scripts\build.bat clean         # Clean build artifacts
scripts\build.bat test          # Build and run tests  
scripts\build.bat deploy        # Build and prepare for deployment
```

## 🔨 Building After Code Changes

### **Quick Build & Validation (Recommended)**
```bash
# Navigate to project root
cd c:\DEV\Samples\WarehouseReturns

# Run comprehensive build script
scripts\build.bat                # Windows
./scripts/build.sh              # Linux/macOS
```

### **Manual Build Steps**
```bash
# 1. Activate virtual environment
.venv\Scripts\activate          # Windows
source .venv/bin/activate       # Linux/macOS

# 2. Install/update dependencies
pip install -r requirements.txt

# 3. Validate Python syntax
python -m py_compile src/pieceinfo_api/function_app.py
python -m py_compile src/pieceinfo_api/services/aggregation_service.py
python -m py_compile src/pieceinfo_api/services/http_client.py

# 4. Run tests to validate changes
python -m pytest tests/ -v

# 5. Check code style (optional)
flake8 src/pieceinfo_api/ --max-line-length=120
```

### **Build with Different Modes**
```bash
# Clean build (removes cached files)
scripts\build.bat clean

# Build and run tests
scripts\build.bat test

# Build for production deployment
scripts\build.bat deploy
```

### **Azure Functions Specific Build**
```bash
# Navigate to the function app directory
cd src/pieceinfo_api

# Test locally
func start --python

# Validate function bindings
func extensions install

# Package for deployment
func azure functionapp publish <your-function-app-name>
```

### **Development Workflow After Changes**
```bash
# 1. Make your code changes
# 2. Run quick validation
python -m pytest tests/test_<changed_module>.py -v

# 3. Run full build with tests
scripts\build.bat test

# 4. If all tests pass, test locally
cd src/pieceinfo_api
func start --python

# 5. Deploy to Azure (if ready)
func azure functionapp publish <your-function-app-name>
```

### Manual Local Development Setup

### Prerequisites
- Python 3.11+
- Azure Functions Core Tools v4
- Azure CLI
- Azure subscription

### Installation

1. Clone the repository
2. Create and activate virtual environment:
   ```bash
   python -m venv .venv
   .venv\Scripts\activate  # Windows
   # or
   source .venv/bin/activate  # Linux/Mac
   ```

3. Install dependencies:
   ```bash
   pip install -r requirements.txt
   ```

4. Configure environment:
   ```bash
   # Copy example environment file
   copy .env.example .env  # Windows
   # or
   cp .env.example .env    # Linux/Mac
   
   # Edit .env with your Azure service configurations
   ```

### Running Function Apps

Each function app can be run independently with comprehensive logging:

```bash
# Document Intelligence (includes document processing with AI)
cd src/document_intelligence
func start --port 7071

# Return Processing (includes queue processing and status tracking)
cd src/return_processing  
func start --port 7072

# PieceInfo API (aggregates data from multiple external APIs)
cd src/pieceinfo_api
func start --port 7074
```

**Note**: The warehouse_management component was removed per project requirements.

### Logging Framework Features

The project includes a comprehensive logging framework with:

- **Structured JSON logging** for better searchability in Application Insights
- **Correlation tracking** across HTTP requests and function calls
- **Business event logging** for tracking key operations
- **Azure Application Insights integration** for centralized monitoring
- **HTTP request/response middleware** for automatic request logging
- **Function decorators** for automatic function entry/exit logging

Example log output:
```json
{
  "timestamp": "2024-01-15T10:30:00.123Z",
  "level": "INFO",
  "message": "Document processing completed successfully",
  "logger": "warehouse_returns.document_intelligence",
  "correlation_id": "abc123-def456",
  "event_type": "document_processing_completed",
  "entity_id": "sample-analysis-123",
  "entity_type": "document_analysis"
}
```

## 📊 Application Insights Integration

### Automatic Log Collection

Once configured, Application Insights automatically collects:

**🔍 Request Telemetry:**
- HTTP request/response details with status codes
- Request duration and performance metrics
- Correlation IDs for distributed tracing

**📋 Custom Telemetry:**
- Structured business event logging
- API call performance metrics (external API timing)
- Error details with stack traces and context
- Custom properties (piece numbers, correlation IDs, user context)

**🎯 Query Examples in Application Insights:**

```kql
// Find all requests for a specific piece number
requests
| where customDimensions.piece_number == "170080637"
| project timestamp, name, duration, resultCode, customDimensions

// Track API performance across all endpoints
requests
| where name startswith "GET /api/pieces"
| summarize avg(duration), count() by name, bin(timestamp, 1h)

// Find errors with correlation tracking
exceptions
| join requests on operation_Id
| project timestamp, type, outerMessage, operation_Id, customDimensions
```

**🚨 Alerts Configuration:**
- Set up alerts for API response times > 5 seconds
- Monitor error rates exceeding 5% of total requests
- Track dependency failures (external API unavailability)

## 🧪 Testing

### Install Test Dependencies
```bash
# Install pytest and testing dependencies
pip install pytest pytest-asyncio pytest-cov pytest-mock

# Or install all requirements (includes test dependencies)
pip install -r requirements.txt
```

### Run Test Suite
```bash
# Run all tests with verbose output
python -m pytest tests/ -v

# Run all tests with coverage report
python -m pytest tests/ --cov=src/pieceinfo_api --cov-report=html --cov-report=term-missing
```

### Run Specific Test Files
```bash
# Run HTTP client tests only
python -m pytest tests/test_http_client.py -v

# Run aggregation service tests only
python -m pytest tests/test_aggregation_service.py -v

# Run API endpoint tests only
python -m pytest tests/test_api_endpoints.py -v
```

### Run Tests by Category
```bash
# Run only unit tests
python -m pytest tests/ -m unit -v

# Run only integration tests  
python -m pytest tests/ -m integration -v

# Run only API tests
python -m pytest tests/ -m api -v
```

### Using Build Scripts (Recommended)
```bash
# Windows - Run build with tests
scripts\build.bat test

# Linux/macOS - Run build with tests  
./scripts/build.sh test
```

### Advanced Test Options
```bash
# Run tests in parallel (if pytest-xdist is installed)
python -m pytest tests/ -n auto

# Run with detailed output and stop on first failure
python -m pytest tests/ -v -x

# Run specific test function
python -m pytest tests/test_http_client.py::TestSimpleHTTPClient::test_successful_get_request -v
```

## Shared Components

All function apps utilize shared components in `src/shared/`:

- **Config**: Centralized configuration management and comprehensive logging framework
- **Middleware**: HTTP request/response logging with correlation tracking
- **Exceptions**: Custom exception classes
- **Utils**: Common utilities for dates, validation, decorators

### Logging Framework Features

The project includes a production-ready logging framework with:

- **Structured JSON logging** for searchability in Application Insights
- **Correlation tracking** across HTTP requests and function calls  
- **Business event logging** for tracking key operations
- **Azure Application Insights integration** for centralized monitoring
- **HTTP middleware** for automatic request/response logging
- **Function decorators** for entry/exit logging

### Using the Logging Framework

```python
from shared.config.logging_config import get_logger, log_function_calls

# Get a logger for your module
logger = get_logger('warehouse_returns.my_module')

# Structured logging with additional context
logger.info("Processing started", user_id="user123", batch_size=50)

# Business event tracking
logger.log_business_event(
    "order_processed",
    entity_id="ORDER-12345", 
    entity_type="order",
    properties={"amount": 99.99, "status": "completed"}
)

# Automatic function logging with decorator
@log_function_calls("my_module.important_function")
def process_order(order_id):
    pass  # Entry/exit automatically logged

# HTTP request/response logging
from shared.middleware.logging_middleware import create_http_logging_wrapper

@create_http_logging_wrapper("ProcessOrder")  
def my_http_function(req):
    pass  # Request/response automatically logged
```

## API Endpoints

### Document Intelligence API
- `POST /api/v1/documents/analyze` - Analyze uploaded documents
- `GET /api/v1/documents/health` - Health check

### Return Processing API  
- `POST /api/v1/returns` - Create new return
- `GET /api/v1/returns/{id}` - Get return details
- `PUT /api/v1/returns/{id}/status` - Update return status
- `GET /api/v1/returns/health` - Health check

### PieceInfo API
- `GET /api/v1/pieces/{piece_number}` - Get aggregated piece information
- `POST /api/v1/pieces/batch` - Get multiple pieces in batch
- `GET /api/v1/pieces/health` - Health check

**Example PieceInfo Response**:
```json
{
  "piece_inventory_key": "170080637",
  "sku": "67007500",
  "vendor_code": "VIZIA",
  "warehouse_location": "WHKCTY",
  "rack_location": "R03-019-03",
  "serial_number": "SZVOU5GB1600294",
  "description": "ALL-IN-ONE SOUNDBAR",
  "model_no": "SV210D-0806",
  "brand": "VIZBC",
  "family": "ELECTR",
  "category": "EHMAUD",
  "group": "HMSBAR",
  "purchase_reference_number": "6610299377*2",
  "vendor_name": "NIGHT & DAY",
  "vendor_address": {
    "address_line1": "3901 N KINGSHIGHWAY BLVD",
    "city": "SAINT LOUIS",
    "state": "MO",
    "zip_code": "63115"
  },
  "vendor_contact": {
    "rep_name": "John Nicholson",
    "primary_rep_email": "jpnick@kc.rr.com"
  },
  "vendor_policies": {
    "serial_number_required": false,
    "vendor_return": false
  }
}
```

## Environment Variables

See `.env.example` for required environment variables for each function app.

## Deployment

Each function app is deployed independently to Azure:

```bash
# Deploy all apps
./scripts/deploy.sh

# Deploy specific app
az functionapp deployment source config-zip -g myResourceGroup -n myFunctionApp --src document_intelligence.zip
```

## Contributing

1. Follow the established project structure
2. Write tests for new functionality  
3. Ensure code quality with linting and formatting
4. Update API documentation
5. Follow semantic versioning for releases
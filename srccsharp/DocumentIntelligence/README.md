# Document Intelligence API

Enterprise-grade Azure Functions API for automated document analysis using Azure Document Intelligence service. Specialized for serial number extraction from product labels, invoices, and warehouse documentation with confidence-based quality assurance workflows.

## 🎯 Overview

The Document Intelligence API provides automated document processing capabilities with intelligent field extraction, confidence scoring, and quality assurance routing. Documents that fail confidence thresholds are automatically stored for manual review, ensuring accuracy while maximizing automation efficiency.

### Key Value Proposition

- **Automated Serial Number Extraction**: AI-powered extraction with 85%+ accuracy on trained document types
- **Quality Assurance Workflow**: Automatic storage of low-confidence documents for manual review
- **Enterprise Security**: Comprehensive authentication, validation, and audit logging
- **Scalable Architecture**: Serverless Azure Functions with automatic scaling and high availability
- **Production Ready**: Complete monitoring, error handling, and deployment automation

## 🏗️ Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────────┐
│   Client Apps   │───▶│  Azure Functions │───▶│ Azure Document AI   │
│                 │    │                  │    │                     │
│ • Web Apps      │    │ • HTTP Triggers  │    │ • Custom Models     │
│ • Mobile Apps   │    │ • Validation     │    │ • Field Extraction  │
│ • Batch Jobs    │    │ • Error Handling │    │ • Confidence Scores │
└─────────────────┘    └──────────────────┘    └─────────────────────┘
                                │                         │
                                ▼                         ▼
                       ┌──────────────────┐    ┌─────────────────────┐
                       │  Blob Storage    │    │  Application        │
                       │                  │    │  Insights           │
                       │ • Low Confidence │    │                     │
                       │ • Document Archive│   │ • Performance       │
                       │ • Audit Trail    │    │ • Error Tracking    │
                       └──────────────────┘    │ • Usage Analytics   │
                                               └─────────────────────┘
```

### Core Components

| Component | Purpose | Technology |
|-----------|---------|------------|
| **Functions** | HTTP API endpoints for document processing | Azure Functions v4 |
| **Models** | Request/response models with validation | System.ComponentModel.DataAnnotations |
| **Services** | Business logic and Azure service integration | Dependency Injection |
| **Repositories** | Data access and blob storage management | Azure SDK |
| **Configuration** | Environment-specific settings management | IOptions pattern |

## 🚀 Quick Start (5 Minutes)

### Prerequisites

- .NET 8.0 SDK
- Azure Functions Core Tools v4
- Azure Document Intelligence resource
- Azure Storage account (optional, for low-confidence storage)

### 1. Clone and Configure

```bash
# Clone the repository
git clone <repository-url>
cd srccsharp/DocumentIntelligence

# Install dependencies
dotnet restore

# Copy local settings template
copy local.settings.template.json local.settings.json
```

### 2. Update Configuration

Edit `local.settings.json`:

```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "DOCUMENT_INTELLIGENCE_ENDPOINT": "https://your-resource.cognitiveservices.azure.com/",
    "DOCUMENT_INTELLIGENCE_KEY": "your-32-character-api-key",
    "AZURE_STORAGE_CONNECTION_STRING": "DefaultEndpointsProtocol=https;AccountName=...",
    "CONFIDENCE_THRESHOLD": "0.7"
  }
}
```

### 3. Run Locally

```bash
# Start the function app
func start --port 7072

# Test health endpoint
curl http://localhost:7072/api/health

# Test document processing
curl -X POST http://localhost:7072/api/process-document \
  -H "Content-Type: application/json" \
  -d '{"document_url": "https://example.com/document.pdf"}'
```

## 📋 API Reference

### Base URL
- **Local Development**: `http://localhost:7072`
- **Production**: `https://your-function-app.azurewebsites.net`

### Authentication
- **Development**: Anonymous access enabled
- **Production**: Azure Functions key required (`?code=your-function-key`)

### Endpoints

#### POST /api/process-document
Process documents from URLs for serial number extraction.

**Request Body:**
```json
{
  "document_url": "https://storage.example.com/document.pdf",
  "model_id": "serialnumber-v1.0",
  "confidence_threshold": 0.8,
  "document_type": "product_label"
}
```

**Response (Success):**
```json
{
  "analysis_id": "guid-12345",
  "status": "Succeeded",
  "serial_field": {
    "value": "SN123456789",
    "confidence": 0.92,
    "status": "ExtractionSuccess",
    "confidence_acceptable": true
  },
  "storage_info": {
    "stored": false,
    "storage_reason": null
  },
  "processing_time_ms": 2340,
  "timestamp": "2024-01-15T10:30:00Z"
}
```

**Response (Low Confidence):**
```json
{
  "analysis_id": "guid-67890",
  "status": "Succeeded", 
  "serial_field": {
    "value": "SN987654321",
    "confidence": 0.65,
    "status": "LowConfidence",
    "confidence_acceptable": false
  },
  "storage_info": {
    "stored": true,
    "container_name": "warehouse-returns-doc-intel-low-confidence",
    "blob_name": "2024/01/15/guid-67890-document.pdf",
    "storage_reason": "low_confidence"
  },
  "processing_time_ms": 1890,
  "timestamp": "2024-01-15T10:35:00Z"
}
```

#### GET /api/health
Service health check with dependency validation.

**Response:**
```json
{
  "status": "Healthy",
  "timestamp": "2024-01-15T10:30:00Z",
  "service_version": "1.0.0",
  "environment": "Production",
  "health_checks": [
    {
      "component": "DocumentIntelligence",
      "status": "Healthy",
      "response_time_ms": 145,
      "description": "Azure Document Intelligence API is operational"
    },
    {
      "component": "BlobStorage", 
      "status": "Healthy",
      "response_time_ms": 67,
      "description": "Azure Blob Storage is accessible"
    }
  ],
  "total_response_time_ms": 212
}
```

#### GET /api/docs
Interactive Swagger UI documentation interface.

#### GET /api/swagger
OpenAPI/Swagger JSON specification.

### Status Codes

| Code | Description | Response |
|------|-------------|----------|
| 200 | Success | Document processed successfully |
| 400 | Bad Request | Invalid request parameters or unsupported file type |
| 401 | Unauthorized | Missing or invalid function key |
| 413 | Payload Too Large | Document exceeds maximum size limit |
| 500 | Internal Server Error | Processing error or service unavailable |
| 503 | Service Unavailable | Azure Document Intelligence service issues |

### Error Response Format

```json
{
  "error_code": "ProcessingError",
  "message": "Document analysis failed",
  "details": "Azure Document Intelligence service returned an error",
  "correlation_id": "12345678-abcd-efgh-ijkl-123456789012",
  "timestamp": "2024-01-15T10:30:00Z",
  "validation_errors": []
}
```

## ⚙️ Configuration

### Environment Variables

| Variable | Required | Description | Example |
|----------|----------|-------------|---------|
| `DOCUMENT_INTELLIGENCE_ENDPOINT` | Yes | Azure Document Intelligence endpoint URL | `https://eastus-ai.cognitiveservices.azure.com/` |
| `DOCUMENT_INTELLIGENCE_KEY` | Yes | API key for authentication | `abc123def456ghi789jkl012mno345pq` |
| `DOCUMENT_INTELLIGENCE_API_VERSION` | No | API version to use | `2024-11-30` |
| `DEFAULT_MODEL_ID` | No | Default model for analysis | `serialnumber-v1.0` |
| `AZURE_STORAGE_CONNECTION_STRING` | No* | Blob storage connection string | `DefaultEndpointsProtocol=https;AccountName=...` |
| `BLOB_CONTAINER_PREFIX` | No | Container name prefix | `warehouse-returns-doc-intel` |
| `ENABLE_BLOB_STORAGE` | No | Enable document storage | `true` |
| `CONFIDENCE_THRESHOLD` | No | Default confidence threshold | `0.7` |
| `MAX_FILE_SIZE_MB` | No | Maximum document size | `50` |
| `AZURE_API_TIMEOUT` | No | API timeout in seconds | `300` |

*Required if `ENABLE_BLOB_STORAGE` is true

### Configuration Validation

```csharp
// Validate configuration at startup
var documentSettings = new DocumentIntelligenceSettings();
var errors = documentSettings.ValidateSettings();
if (errors.Any())
{
    throw new InvalidOperationException($"Configuration errors: {string.Join(", ", errors)}");
}
```

### Security Best Practices

- **API Keys**: Use Azure Key Vault or environment variables, never commit to source control
- **Endpoints**: Restrict network access using Azure Function access restrictions
- **Storage**: Use managed identity authentication where possible
- **Monitoring**: Enable Application Insights for security event tracking
- **Validation**: Implement input validation and sanitization for all requests

## 📊 Monitoring & Operations

### Application Insights Integration

The API includes comprehensive telemetry and monitoring:

**Performance Metrics:**
```kql
requests 
| where name == "ProcessDocument"
| summarize 
    avg_duration = avg(duration),
    success_rate = avg(toint(success)) * 100,
    request_count = count()
by bin(timestamp, 1h)
| order by timestamp desc
```

**Error Analysis:**
```kql
exceptions
| where outerMessage contains "Document"
| summarize error_count = count() by type, outerMessage
| order by error_count desc
```

**Confidence Score Analysis:**
```kql
traces
| where message contains "Serial-Confidence"
| extend confidence = extract(@"Serial-Confidence: ([\d.]+)", 1, message)
| where isnotnull(confidence)
| summarize 
    avg_confidence = avg(todouble(confidence)),
    low_confidence_rate = countif(todouble(confidence) < 0.7) * 100.0 / count()
by bin(timestamp, 1h)
```

### Health Monitoring

Set up health check monitoring with Application Insights:

```bash
# Create availability test
az monitor app-insights web-test create \
  --resource-group "your-rg" \
  --name "DocumentIntelligence-Health" \
  --location "East US" \
  --web-test-name "Health Check" \
  --web-test-kind "ping" \
  --locations "East US" "West Europe" \
  --url "https://your-app.azurewebsites.net/api/health"
```

### Performance Optimization

**Document Size Optimization:**
- Implement image compression for large documents
- Use PDF format for best OCR results
- Limit document resolution to 300 DPI maximum

**Caching Strategy:**
- Cache model results for identical documents
- Implement Redis cache for frequently accessed data
- Use CDN for document distribution

**Scaling Configuration:**
```json
{
  "functionAppScaleLimit": 200,
  "maximumElasticWorkerCount": 100,
  "http": {
    "maxConcurrentRequests": 500,
    "maxOutstandingRequests": 200
  }
}
```

## 🔧 Development Guide

### Local Development Setup

1. **Install Prerequisites:**
   ```bash
   # Install .NET 8.0 SDK
   winget install Microsoft.DotNet.SDK.8
   
   # Install Azure Functions Core Tools
   npm install -g azure-functions-core-tools@4
   
   # Install Azure CLI
   winget install Microsoft.AzureCLI
   ```

2. **Configure Development Environment:**
   ```bash
   # Login to Azure
   az login
   
   # Set default subscription
   az account set --subscription "your-subscription-id"
   
   # Create resource group (if needed)
   az group create --name "rg-document-intelligence-dev" --location "East US"
   ```

3. **Create Azure Resources:**
   ```bash
   # Create Document Intelligence resource
   az cognitiveservices account create \
     --name "doc-intel-dev" \
     --resource-group "rg-document-intelligence-dev" \
     --kind "FormRecognizer" \
     --sku "S0" \
     --location "East US"
   
   # Create Storage Account
   az storage account create \
     --name "docintelstorage" \
     --resource-group "rg-document-intelligence-dev" \
     --location "East US" \
     --sku "Standard_LRS"
   ```

### Testing Strategy

**Unit Tests:**
```bash
# Run unit tests
dotnet test ../../testcsharp/DocumentIntelligence/

# Run with coverage
dotnet test ../../testcsharp/DocumentIntelligence/ --collect:"XPlat Code Coverage"
```

**Integration Tests:**
```bash
# Start local functions
func start --port 7072

# Run integration tests
dotnet test ../../testcsharp/DocumentIntelligence/IntegrationTests/
```

**Load Testing:**
```bash
# Install Artillery
npm install -g artillery

# Run load test
artillery run load-test.yml
```

### Code Quality

**Static Analysis:**
```bash
# Install SonarQube scanner
dotnet tool install --global dotnet-sonarscanner

# Run analysis
dotnet sonarscanner begin /k:"DocumentIntelligence"
dotnet build
dotnet sonarscanner end
```

**Code Formatting:**
```bash
# Format code
dotnet format

# Verify formatting
dotnet format --verify-no-changes
```

## 🚀 Deployment

### Azure Deployment Options

#### Option 1: Azure CLI Deployment
```bash
# Create Function App
az functionapp create \
  --resource-group "rg-document-intelligence" \
  --consumption-plan-location "East US" \
  --runtime "dotnet-isolated" \
  --runtime-version "8" \
  --functions-version "4" \
  --name "func-document-intelligence" \
  --storage-account "docintelstorage"

# Deploy code
func azure functionapp publish func-document-intelligence
```

#### Option 2: Bicep Infrastructure as Code

See `infrastructure/bicep/main.bicep` for complete infrastructure deployment.

```bash
# Deploy infrastructure
az deployment group create \
  --resource-group "rg-document-intelligence" \
  --template-file "infrastructure/bicep/main.bicep" \
  --parameters environment=prod
```

#### Option 3: GitHub Actions CI/CD

See `.github/workflows/deploy.yml` for automated deployment pipeline.

### Environment-Specific Configuration

**Development:**
```json
{
  "CONFIDENCE_THRESHOLD": "0.6",
  "ENABLE_BLOB_STORAGE": "false",
  "MAX_FILE_SIZE_MB": "10",
  "AZURE_API_TIMEOUT": "60"
}
```

**Staging:**
```json
{
  "CONFIDENCE_THRESHOLD": "0.7", 
  "ENABLE_BLOB_STORAGE": "true",
  "MAX_FILE_SIZE_MB": "50",
  "AZURE_API_TIMEOUT": "180"
}
```

**Production:**
```json
{
  "CONFIDENCE_THRESHOLD": "0.8",
  "ENABLE_BLOB_STORAGE": "true", 
  "MAX_FILE_SIZE_MB": "100",
  "AZURE_API_TIMEOUT": "300"
}
```

### Production Readiness Checklist

- [ ] **Security**: API keys stored in Azure Key Vault
- [ ] **Monitoring**: Application Insights configured with alerts
- [ ] **Scaling**: Function app scaling limits configured
- [ ] **Backup**: Blob storage geo-replication enabled
- [ ] **Performance**: Load testing completed successfully
- [ ] **Documentation**: API documentation published
- [ ] **Compliance**: Security scanning and vulnerability assessment
- [ ] **Disaster Recovery**: Recovery procedures documented and tested

## 🆘 Troubleshooting

### Common Issues

**Issue**: Document Intelligence API returns 401 Unauthorized
```bash
# Solution: Verify API key and endpoint
az cognitiveservices account keys list \
  --name "your-doc-intel-resource" \
  --resource-group "your-rg"
```

**Issue**: Function timeout during large document processing
```json
// Solution: Increase timeout in host.json
{
  "functionTimeout": "00:10:00",
  "http": {
    "maxRequestLength": 104857600
  }
}
```

**Issue**: Blob storage connection failures
```bash
# Solution: Test storage connection
az storage container list \
  --connection-string "your-connection-string"
```

### Diagnostic Queries

**Failed Requests:**
```kql
requests
| where success == false
| where name startswith "api/"
| summarize count() by resultCode, name
| order by count_ desc
```

**Performance Issues:**
```kql
requests
| where name == "ProcessDocument" 
| where duration > 30000  // Over 30 seconds
| project timestamp, operation_Id, duration, url
| order by timestamp desc
```

**Storage Analysis:**
```kql
traces
| where message contains "STORAGE-ERROR"
| extend analysisId = extract(@"Analysis-ID: ([^,]+)", 1, message)
| project timestamp, analysisId, message
| order by timestamp desc
```

### Support Resources

- **Documentation**: [DEVELOPER_GUIDE.md](./DEVELOPER_GUIDE.md)
- **API Reference**: `/api/docs` (Swagger UI)
- **Monitoring**: Application Insights dashboard
- **Issues**: GitHub Issues or internal ticketing system
- **Performance**: Azure Monitor and Function App metrics

---

## 📚 Additional Resources

- [Azure Document Intelligence Documentation](https://docs.microsoft.com/azure/applied-ai-services/form-recognizer/)
- [Azure Functions Best Practices](https://docs.microsoft.com/azure/azure-functions/functions-best-practices)
- [Azure Blob Storage Security](https://docs.microsoft.com/azure/storage/common/security-recommendations)
- [Application Insights Monitoring](https://docs.microsoft.com/azure/azure-monitor/app/app-insights-overview)

**Version**: 1.0.0  
**Last Updated**: January 2024  
**Maintainer**: Warehouse Returns Team
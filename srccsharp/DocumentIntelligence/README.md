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

## 🔨 Build & Test Commands

### Building the Project

**Build Application:**
```bash
# Navigate to project directory
cd srccsharp/DocumentIntelligence

# Clean previous builds
dotnet clean

# Restore NuGet packages
dotnet restore

# Build in Debug mode (default)
dotnet build

# Build in Release mode for production
dotnet build --configuration Release

# Build with verbose output for troubleshooting
dotnet build --verbosity detailed
```

**Build Test Project:**
```bash
# Navigate to test project directory
cd testcsharp/DocumentIntelligence

# Clean and restore test dependencies
dotnet clean
dotnet restore

# Build test project
dotnet build

# Build test project in Release mode
dotnet build --configuration Release
```

### Running Tests

**Execute All Tests:**
```bash
# Run all tests with default settings
dotnet test

# Run tests with detailed output
dotnet test --verbosity normal

# Run tests and generate coverage report
dotnet test --collect:"XPlat Code Coverage"

# Run tests with filter for specific categories
dotnet test --filter Category=Unit
dotnet test --filter Category=Integration
```

**Test Categories Available:**
- **Unit Tests**: Fast, isolated tests (140 tests)
- **Integration Tests**: Tests with external dependencies
- **Performance Tests**: Load and response time validation
- **Security Tests**: Input validation and security scenarios

**Generate Test Reports:**
```bash
# Generate test report with coverage
dotnet test --logger trx --collect:"XPlat Code Coverage" --results-directory TestResults

# Generate HTML coverage report (requires reportgenerator tool)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"TestResults/Coverage" -reporttypes:Html

# View coverage report
start TestResults/Coverage/index.html  # Windows
open TestResults/Coverage/index.html   # macOS
```

### Development Workflow

**Complete Development Build:**
```bash
# Full development workflow script
#!/bin/bash
set -e  # Exit on any error

echo "🏗️ Building DocumentIntelligence API..."
cd srccsharp/DocumentIntelligence

# Clean and build application
dotnet clean --verbosity quiet
dotnet restore --verbosity quiet
dotnet build --configuration Debug

echo "🧪 Running tests..."
cd ../../testcsharp/DocumentIntelligence

# Clean and build tests
dotnet clean --verbosity quiet
dotnet restore --verbosity quiet
dotnet build --configuration Debug

# Execute test suite
dotnet test --configuration Debug --logger "console;verbosity=normal"

echo "✅ Build and tests completed successfully!"
echo "🚀 Ready to run: func start --port 7072"
```

**Production Build:**
```bash
# Production-ready build script
#!/bin/bash
set -e

echo "🏭 Production build started..."
cd srccsharp/DocumentIntelligence

# Clean previous builds
dotnet clean --configuration Release

# Restore with locked dependencies
dotnet restore --locked-mode

# Build with optimizations
dotnet build --configuration Release --no-restore

# Run security and quality checks
echo "🔒 Running security analysis..."
dotnet list package --vulnerable --include-transitive

# Package for deployment
echo "📦 Creating deployment package..."
dotnet publish --configuration Release --output ./publish --no-build

echo "✅ Production build completed: ./publish"
```

### Code Quality & Analysis

**Static Code Analysis:**
```bash
# Install analysis tools
dotnet tool install --global dotnet-sonarscanner
dotnet tool install --global security-scan

# Run SonarQube analysis
dotnet sonarscanner begin /k:"DocumentIntelligence" /d:sonar.host.url="http://localhost:9000"
dotnet build --configuration Release
dotnet sonarscanner end

# Security vulnerability scan
dotnet list package --vulnerable --include-transitive
security-scan .
```

**Code Formatting:**
```bash
# Format code according to .editorconfig
dotnet format

# Verify formatting without changes
dotnet format --verify-no-changes

# Format specific file types only
dotnet format --include "**/*.cs"
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

### CI/CD Pipeline Setup

**GitHub Actions Workflow (.github/workflows/build-test-deploy.yml):**
```yaml
name: Build, Test, and Deploy DocumentIntelligence API

on:
  push:
    branches: [ main, develop ]
    paths: [ 'srccsharp/DocumentIntelligence/**', 'testcsharp/DocumentIntelligence/**' ]
  pull_request:
    branches: [ main ]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET 8.0
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'
    
    - name: Restore dependencies
      run: |
        cd srccsharp/DocumentIntelligence
        dotnet restore
        cd ../../testcsharp/DocumentIntelligence
        dotnet restore
    
    - name: Build application
      run: |
        cd srccsharp/DocumentIntelligence
        dotnet build --configuration Release --no-restore
    
    - name: Run tests
      run: |
        cd testcsharp/DocumentIntelligence
        dotnet test --configuration Release --no-build --collect:"XPlat Code Coverage" --logger trx
    
    - name: Upload test results
      uses: actions/upload-artifact@v4
      if: always()
      with:
        name: test-results
        path: testcsharp/DocumentIntelligence/TestResults/
    
    - name: Security scan
      run: |
        cd srccsharp/DocumentIntelligence
        dotnet list package --vulnerable --include-transitive
  
  deploy-staging:
    needs: build-and-test
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/develop'
    environment: staging
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET 8.0
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'
    
    - name: Build and publish
      run: |
        cd srccsharp/DocumentIntelligence
        dotnet publish --configuration Release --output ./publish
    
    - name: Deploy to Azure Functions (Staging)
      uses: Azure/functions-action@v1
      with:
        app-name: ${{ secrets.AZURE_FUNCTIONAPP_NAME_STAGING }}
        package: srccsharp/DocumentIntelligence/publish
        publish-profile: ${{ secrets.AZURE_FUNCTIONAPP_PUBLISH_PROFILE_STAGING }}
  
  deploy-production:
    needs: build-and-test
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    environment: production
    steps:
    - uses: actions/checkout@v4
    
    - name: Setup .NET 8.0
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '8.0.x'
    
    - name: Build and publish
      run: |
        cd srccsharp/DocumentIntelligence
        dotnet publish --configuration Release --output ./publish
    
    - name: Deploy to Azure Functions (Production)
      uses: Azure/functions-action@v1
      with:
        app-name: ${{ secrets.AZURE_FUNCTIONAPP_NAME_PROD }}
        package: srccsharp/DocumentIntelligence/publish
        publish-profile: ${{ secrets.AZURE_FUNCTIONAPP_PUBLISH_PROFILE_PROD }}
```

**Azure DevOps Pipeline (azure-pipelines.yml):**
```yaml
trigger:
  branches:
    include:
    - main
    - develop
  paths:
    include:
    - srccsharp/DocumentIntelligence/*
    - testcsharp/DocumentIntelligence/*

variables:
  buildConfiguration: 'Release'
  azureSubscription: 'your-service-connection'

stages:
- stage: BuildAndTest
  displayName: 'Build and Test'
  jobs:
  - job: Build
    pool:
      vmImage: 'ubuntu-latest'
    steps:
    - task: UseDotNet@2
      displayName: 'Install .NET 8.0'
      inputs:
        packageType: 'sdk'
        version: '8.0.x'
    
    - task: DotNetCoreCLI@2
      displayName: 'Restore packages'
      inputs:
        command: 'restore'
        projects: 'srccsharp/DocumentIntelligence/*.csproj'
    
    - task: DotNetCoreCLI@2
      displayName: 'Build application'
      inputs:
        command: 'build'
        projects: 'srccsharp/DocumentIntelligence/*.csproj'
        arguments: '--configuration $(buildConfiguration) --no-restore'
    
    - task: DotNetCoreCLI@2
      displayName: 'Run tests'
      inputs:
        command: 'test'
        projects: 'testcsharp/DocumentIntelligence/*.csproj'
        arguments: '--configuration $(buildConfiguration) --collect:"XPlat Code Coverage" --logger trx'
    
    - task: PublishTestResults@2
      displayName: 'Publish test results'
      inputs:
        testResultsFormat: 'VSTest'
        testResultsFiles: '**/*.trx'
        failTaskOnFailedTests: true
    
    - task: DotNetCoreCLI@2
      displayName: 'Publish application'
      inputs:
        command: 'publish'
        projects: 'srccsharp/DocumentIntelligence/*.csproj'
        arguments: '--configuration $(buildConfiguration) --output $(Build.ArtifactStagingDirectory)'
    
    - task: PublishBuildArtifacts@1
      displayName: 'Publish artifacts'
      inputs:
        pathToPublish: '$(Build.ArtifactStagingDirectory)'
        artifactName: 'DocumentIntelligenceAPI'

- stage: DeployStaging
  displayName: 'Deploy to Staging'
  condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/develop'))
  jobs:
  - deployment: DeployStaging
    environment: 'staging'
    pool:
      vmImage: 'ubuntu-latest'
    strategy:
      runOnce:
        deploy:
          steps:
          - task: AzureFunctionApp@1
            displayName: 'Deploy to Azure Functions (Staging)'
            inputs:
              azureSubscription: '$(azureSubscription)'
              appType: 'functionApp'
              appName: '$(stagingFunctionAppName)'
              package: '$(Pipeline.Workspace)/DocumentIntelligenceAPI'

- stage: DeployProduction
  displayName: 'Deploy to Production'
  condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))
  jobs:
  - deployment: DeployProduction
    environment: 'production'
    pool:
      vmImage: 'ubuntu-latest'
    strategy:
      runOnce:
        deploy:
          steps:
          - task: AzureFunctionApp@1
            displayName: 'Deploy to Azure Functions (Production)'
            inputs:
              azureSubscription: '$(azureSubscription)'
              appType: 'functionApp'
              appName: '$(productionFunctionAppName)'
              package: '$(Pipeline.Workspace)/DocumentIntelligenceAPI'
```

### Production Readiness Checklist

#### Pre-Deployment Validation
- [ ] **Build**: All projects build successfully in Release configuration
- [ ] **Tests**: 100% test pass rate with >80% code coverage
- [ ] **Security**: Vulnerability scan completed with no high/critical issues
- [ ] **Performance**: Load tests validate SLA requirements (< 30s response time)
- [ ] **Dependencies**: All NuGet packages updated and security-scanned

#### Infrastructure & Security
- [ ] **API Keys**: All secrets stored in Azure Key Vault with rotation policies
- [ ] **Authentication**: Function keys configured with appropriate expiration
- [ ] **Network**: Private endpoints and firewall rules configured
- [ ] **Storage**: Blob storage configured with encryption at rest and in transit
- [ ] **Compliance**: Data residency and retention policies implemented

#### Monitoring & Observability
- [ ] **Application Insights**: Telemetry collection configured with custom metrics
- [ ] **Alerting**: Health check alerts, error rate thresholds, and SLA monitoring
- [ ] **Dashboards**: Operational dashboards for key metrics and performance
- [ ] **Log Analytics**: Structured logging with correlation ID tracking
- [ ] **Availability**: Uptime monitoring from multiple geographic locations

#### Operational Readiness
- [ ] **Scaling**: Function app scaling limits and consumption plan configured
- [ ] **Backup**: Geo-redundant storage and configuration backup procedures
- [ ] **Disaster Recovery**: Multi-region deployment and failover procedures tested
- [ ] **Documentation**: Runbooks, troubleshooting guides, and API documentation published
- [ ] **Support**: On-call procedures and escalation paths established

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

### Development Troubleshooting

**Common Build Issues:**

*Issue: Build fails with "The type or namespace name 'X' could not be found"*
```bash
# Solution: Clean and restore packages
dotnet clean
dotnet restore --force
dotnet build
```

*Issue: Tests fail with "Connection string not found"*
```bash
# Solution: Ensure local.settings.json is configured
cp local.settings.template.json local.settings.json
# Edit local.settings.json with proper connection strings
```

*Issue: Function app fails to start locally*
```bash
# Solution: Check Azure Functions Core Tools version
func --version  # Should be 4.x
npm install -g azure-functions-core-tools@4

# Verify .NET version
dotnet --version  # Should be 8.0.x
```

**Performance Optimization:**

*Slow test execution:*
```bash
# Run tests in parallel
dotnet test --parallel

# Run specific test categories
dotnet test --filter Category=Unit
```

*Large build times:*
```bash
# Use incremental builds
dotnet build --no-restore

# Build only changed projects
dotnet build --no-dependencies
```

### Support Resources

- **Documentation**: [DEVELOPER_GUIDE.md](./DEVELOPER_GUIDE.md)
- **API Reference**: `/api/docs` (Swagger UI)
- **Build Scripts**: [build-scripts/](./build-scripts/)
- **Monitoring**: Application Insights dashboard
- **Issues**: GitHub Issues or internal ticketing system
- **Performance**: Azure Monitor and Function App metrics
- **Security**: [SECURITY.md](./SECURITY.md) for security policies
- **Contributing**: [CONTRIBUTING.md](./CONTRIBUTING.md) for development guidelines

---

## 📚 Additional Resources

- [Azure Document Intelligence Documentation](https://docs.microsoft.com/azure/applied-ai-services/form-recognizer/)
- [Azure Functions Best Practices](https://docs.microsoft.com/azure/azure-functions/functions-best-practices)
- [Azure Blob Storage Security](https://docs.microsoft.com/azure/storage/common/security-recommendations)
- [Application Insights Monitoring](https://docs.microsoft.com/azure/azure-monitor/app/app-insights-overview)

---

## 📄 Project Information

**Version**: 1.0.0  
**Last Updated**: November 2024  
**Maintainer**: Warehouse Returns Development Team  
**License**: MIT License  
**Target Framework**: .NET 8.0  
**Azure Functions Runtime**: v4  

### Build Information
- **Solution File**: `WarehouseReturns.sln`
- **Project Path**: `srccsharp/DocumentIntelligence/DocumentIntelligence.csproj`
- **Test Path**: `testcsharp/DocumentIntelligence/DocumentIntelligence.Tests.csproj`
- **Build Configuration**: Debug (development) | Release (production)
- **Package Manager**: NuGet with PackageReference format
- **Code Analysis**: Enabled with .editorconfig and StyleCop rules

### Quick Reference Commands
```bash
# 🏗️ BUILD
dotnet build                                    # Debug build
dotnet build --configuration Release            # Release build

# 🧪 TEST
dotnet test                                     # Run all tests
dotnet test --collect:"XPlat Code Coverage"    # With coverage

# 🚀 RUN
func start --port 7072                          # Start locally
dotnet run --project DocumentIntelligence.csproj  # Alternative start

# 📦 DEPLOY
dotnet publish --configuration Release          # Create deployment package
func azure functionapp publish <app-name>      # Deploy to Azure
```
# PieceInfo API - Developer Guide

## 🚀 **Quick Start**

### **Prerequisites**
- **.NET 8.0 SDK** or later
- **Azure Functions Core Tools v4**
- **Visual Studio 2022** or **VS Code** with C# extension
- **Valid API Management Subscription Key**

### **Local Development Setup**

1. **Clone and Navigate**:
```bash
git clone https://github.com/veerayerva/warehousereturns.git
cd warehousereturns/srccsharp/PieceInfoApi
```

2. **Configure Local Settings**:
```bash
# Create local configuration
cp local.settings.template.json local.settings.json

# Edit configuration with your API keys
code local.settings.json
```

3. **Install Dependencies**:
```bash
dotnet restore
dotnet build
```

4. **Start Development Server**:
```bash
func start --port 7074
```

5. **Verify Installation**:
```bash
# Health check
curl http://localhost:7074/api/health

# Interactive documentation
open http://localhost:7074/api/swagger/ui
```

## 📚 **API Usage Examples**

### **Basic Usage**

#### **Get Piece Information**
```bash
curl -X GET "http://localhost:7074/api/pieces/170080637" \
     -H "Accept: application/json"
```

**Response**:
```json
{
  "piece_inventory_key": "170080637",
  "sku": "67007500", 
  "vendor_code": "VIZIA",
  "warehouse_location": "WHKCTY",
  "rack_location": "R03-019-03",
  "serial_number": "SZVOU5GB1600294",
  "family": "ELECTRONICS",
  "description": "ALL-IN-ONE SOUNDBAR",
  "model_no": "V-SB2921-C6",
  "brand": "VIZIO",
  "category": "AUDIO",
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
  },
  "metadata": {
    "correlation_id": "550e8400-e29b-41d4-a716-446655440000",
    "timestamp": "2025-11-19T15:30:00.000Z",
    "version": "1.0.0",
    "source": "pieceinfo-api"
  }
}
```

### **Health Monitoring**

#### **Health Check**
```bash
curl -X GET "http://localhost:7074/api/health" \
     -H "Accept: application/json"
```

**Response**:
```json
{
  "status": "healthy",
  "timestamp": "2025-11-19T15:30:00.000Z",
  "correlation_id": "health-123e4567-e89b-12d3-a456-426614174000",
  "version": "1.0.0",
  "service": "pieceinfo-api",
  "environment": "development",
  "components": {
    "external_apis": "healthy",
    "configuration": "healthy", 
    "database_connectivity": "healthy"
  },
  "configuration": {
    "base_url": "https://apim-dev.nfm.com",
    "timeout_seconds": 30,
    "max_retries": 3,
    "subscription_key_configured": true,
    "ssl_verification": "disabled",
    "log_level": "Information"
  }
}
```

## 🔧 **Advanced Configuration**

### **Environment Variables**

#### **Required Settings**
```bash
# Azure Functions Runtime
FUNCTIONS_WORKER_RUNTIME="dotnet-isolated"
AzureWebJobsStorage="UseDevelopmentStorage=true"

# External API Configuration
EXTERNAL_API_BASE_URL="https://apim-prod.nfm.com"
OCP_APIM_SUBSCRIPTION_KEY="your-subscription-key-here"

# Application Insights
APPLICATIONINSIGHTS_CONNECTION_STRING="InstrumentationKey=your-key;..."
```

#### **Optional Performance Settings**
```bash
# HTTP Client Configuration
API_TIMEOUT_SECONDS="30"
API_MAX_RETRIES="3"
MAX_BATCH_SIZE="10"

# Security Settings  
VERIFY_SSL="true"
SSL_CERT_PATH="/app/certs/client.crt"
SSL_KEY_PATH="/app/certs/client.key"

# Logging Configuration
LOG_LEVEL="Information"
WAREHOUSE_RETURNS_ENV="production"
```

### **Production Configuration Example**

#### **Azure App Service Settings**
```json
{
  "EXTERNAL_API_BASE_URL": "https://apim-prod.nfm.com",
  "OCP_APIM_SUBSCRIPTION_KEY": "@Microsoft.KeyVault(VaultName=prod-keyvault;SecretName=apim-key)",
  "API_TIMEOUT_SECONDS": "15",
  "API_MAX_RETRIES": "2", 
  "VERIFY_SSL": "true",
  "LOG_LEVEL": "Warning",
  "WAREHOUSE_RETURNS_ENV": "production",
  "APPLICATIONINSIGHTS_CONNECTION_STRING": "@Microsoft.KeyVault(VaultName=prod-keyvault;SecretName=appinsights-conn)"
}
```

## 🛠️ **Development Workflow**

### **Code Development**
```bash
# Make code changes
code ./Services/AggregationService.cs

# Build and validate
dotnet build
dotnet format

# Run tests
cd ../../testcsharp/PieceInfoApi  
dotnet test --verbosity normal

# Start with hot reload
func start --port 7074
```

### **Testing and Validation**
```bash
# Unit tests
dotnet test --filter "Category=Unit"

# Integration tests  
dotnet test --filter "Category=Integration"

# Performance testing
ab -n 1000 -c 10 http://localhost:7074/api/pieces/170080637

# Load testing with Artillery
artillery run load-test.yml
```

### **Code Quality**
```bash
# Format code
dotnet format --severity info

# Security analysis
dotnet list package --vulnerable --include-transitive

# Static analysis
dotnet build --verbosity diagnostic
```

## 📊 **Monitoring and Observability**

### **Application Insights Queries**

#### **Request Performance**
```kusto
requests
| where timestamp > ago(1h)
| where name contains "GetPieceInfo" 
| summarize 
    Count = count(),
    AvgDuration = avg(duration),
    P95Duration = percentile(duration, 95)
  by bin(timestamp, 5m)
| render timechart
```

#### **Error Analysis**
```kusto
exceptions
| where timestamp > ago(24h)
| where appName == "pieceinfo-api"
| summarize 
    ErrorCount = count(),
    UniqueErrors = dcount(type)
  by bin(timestamp, 1h), type
| render barchart
```

#### **Dependency Health**
```kusto
dependencies
| where timestamp > ago(1h)
| where target contains "apim"
| summarize 
    CallCount = count(),
    SuccessRate = avg(iff(success == true, 1.0, 0.0)) * 100,
    AvgDuration = avg(duration)
  by bin(timestamp, 5m)
| render timechart
```

### **Custom Metrics and Alerts**

#### **Performance Metrics**
- **Response Time**: P95 < 500ms
- **Success Rate**: > 99.5%
- **Error Rate**: < 0.5%
- **External API Health**: > 99%

#### **Alert Conditions**
```csharp
// Custom telemetry in code
_telemetryClient.TrackMetric("PieceInfo.ProcessingTime", stopwatch.ElapsedMilliseconds);
_telemetryClient.TrackMetric("PieceInfo.SuccessRate", successRate);
_telemetryClient.TrackMetric("ExternalApi.ResponseTime", externalApiDuration);
```

## 🚀 **Deployment Guide**

### **Azure Functions Deployment**

#### **Using Azure CLI**
```bash
# Create resource group
az group create --name rg-pieceinfo-prod --location "East US"

# Create Function App
az functionapp create \
  --resource-group rg-pieceinfo-prod \
  --consumption-plan-location eastus \
  --runtime dotnet-isolated \
  --functions-version 4 \
  --name func-pieceinfo-prod \
  --storage-account stpieceinfoprod

# Deploy application
func azure functionapp publish func-pieceinfo-prod
```

#### **Using GitHub Actions**
```yaml
name: Deploy PieceInfo API

on:
  push:
    branches: [main]
    paths: ['srccsharp/PieceInfoApi/**']

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
          
      - name: Build
        run: |
          cd srccsharp/PieceInfoApi
          dotnet build --configuration Release
          
      - name: Deploy to Azure
        uses: Azure/functions-action@v1
        with:
          app-name: func-pieceinfo-prod
          package: './srccsharp/PieceInfoApi'
          publish-profile: ${{ secrets.AZURE_FUNCTIONAPP_PUBLISH_PROFILE }}
```

### **Infrastructure as Code**

#### **Bicep Template**
```bicep
@description('PieceInfo API Function App')
param functionAppName string = 'func-pieceinfo-${uniqueString(resourceGroup().id)}'
param storageAccountName string = 'st${uniqueString(resourceGroup().id)}'
param appInsightsName string = 'ai-pieceinfo-${uniqueString(resourceGroup().id)}'

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-09-01' = {
  name: storageAccountName
  location: resourceGroup().location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: resourceGroup().location
  kind: 'web'
  properties: {
    Application_Type: 'web'
  }
}

resource functionApp 'Microsoft.Web/sites@2021-03-01' = {
  name: functionAppName
  location: resourceGroup().location
  kind: 'functionapp'
  properties: {
    serverFarmId: '/subscriptions/${subscription().subscriptionId}/resourceGroups/${resourceGroup().name}/providers/Microsoft.Web/serverfarms/ASP-${functionAppName}'
    siteConfig: {
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};EndpointSuffix=${environment().suffixes.storage};AccountKey=${storageAccount.listKeys().keys[0].value}'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
      ]
    }
  }
}
```

## 🔒 **Security Best Practices**

### **API Security**
- **Authentication**: API Management subscription keys
- **Authorization**: Azure AD integration (optional)
- **HTTPS/TLS**: Enforced in production
- **Rate Limiting**: APIM policies
- **Input Validation**: Comprehensive parameter validation

### **Configuration Security**
```bash
# Use Azure Key Vault for sensitive data
OCP_APIM_SUBSCRIPTION_KEY="@Microsoft.KeyVault(VaultName=vault;SecretName=apim-key)"

# Enable SSL verification in production
VERIFY_SSL="true"

# Secure certificate storage
SSL_CERT_PATH="/app/certs/client.crt"
SSL_KEY_PATH="/app/certs/client.key"
```

### **Operational Security**
- **Secrets Management**: Azure Key Vault integration
- **Network Security**: Virtual network integration
- **Access Control**: Role-based access control (RBAC)
- **Audit Logging**: Comprehensive request/response logging
- **Vulnerability Scanning**: Regular dependency updates

## 🐛 **Troubleshooting Guide**

### **Common Issues**

#### **Configuration Errors**
```bash
# Check configuration
curl http://localhost:7074/api/health

# Validate settings
dotnet run --configuration Debug --verbosity diagnostic
```

#### **External API Connectivity**
```bash
# Test external API directly
curl -H "Ocp-Apim-Subscription-Key: YOUR_KEY" \
     https://apim-dev.nfm.com/ihubservices/product/piece-inventory-location/170080637

# Check SSL configuration
openssl s_client -connect apim-dev.nfm.com:443
```

#### **Performance Issues**
```bash
# Enable detailed logging
LOG_LEVEL="Debug"

# Monitor with Application Insights
curl -X GET "http://localhost:7074/api/pieces/170080637" \
     -H "x-correlation-id: debug-12345"
```

### **Log Analysis**
```bash
# Application Insights queries
traces
| where timestamp > ago(1h)
| where customDimensions.CorrelationId == "your-correlation-id"
| project timestamp, message, customDimensions
| order by timestamp desc
```

---

## 📚 **Additional Resources**

- **[API Reference](./README.md)**: Complete API documentation
- **[Architecture Guide](./ARCHITECTURE.md)**: Technical architecture details  
- **[Test Suite](../../testcsharp/PieceInfoApi/README.md)**: Comprehensive test documentation
- **[Deployment Guide](./DEPLOYMENT.md)**: Production deployment instructions
- **[Monitoring Guide](./MONITORING.md)**: Observability and alerting setup

## 🤝 **Support and Contribution**

- **Issues**: Report bugs and feature requests via GitHub Issues
- **Documentation**: Contribute to documentation improvements
- **Code Reviews**: Follow the established code review process
- **Testing**: Maintain high test coverage and quality standards

---

**The PieceInfo API provides enterprise-grade piece information aggregation with comprehensive monitoring, security, and operational excellence features for production deployment.**
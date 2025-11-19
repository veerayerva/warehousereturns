# Deployment & Infrastructure - Detailed Documentation

## 📋 Overview
The deployment system provides automated Azure resource provisioning and application deployment for the Warehouse Returns system. It includes Infrastructure as Code (IaC) templates, deployment scripts, and configuration management.

## 🏗️ File Structure & Components

```
WarehouseReturns/
├── scripts/
│   ├── deploy.py                      # Main deployment orchestrator
│   ├── build.sh                       # Linux build script
│   ├── build.bat                      # Windows build script  
│   └── verify_models.py               # Model validation script
├── infrastructure/
│   ├── bicep/                         # Azure Bicep templates
│   └── terraform/                     # Terraform configurations
├── local.settings.json               # Local development configuration
└── requirements.txt                  # Python dependencies
```

## 🔧 Deployment Scripts Documentation

### 1. `scripts/deploy.py` - Main Deployment Orchestrator

#### **Purpose**: Automated Azure resource deployment and configuration management

#### **Key Classes & Functions**:

##### `AzureDeployer` Class
**What it does**: Orchestrates complete Azure infrastructure deployment
- Creates all required Azure resources
- Configures application settings and connections
- Manages environment-specific configurations
- Provides rollback and cleanup capabilities

##### `__init__(resource_group: str, location: str = "eastus")`
**What it does**: Initializes deployment configuration
- **Resource Group**: Logical container for all resources
- **Location**: Azure region for resource deployment
- **Resource Tracking**: Maintains created resource inventory
- **Validation**: Checks Azure CLI availability and authentication

##### `run_az_command(command: list) -> Dict[str, Any]`
**What it does**: Executes Azure CLI commands with error handling
- **Command Execution**: Runs Azure CLI commands securely
- **Error Handling**: Captures and reports command failures
- **Output Parsing**: Converts JSON responses to Python objects
- **Logging**: Detailed command execution logging

**Usage Example**:
```python
deployer = AzureDeployer("warehouse-returns-prod", "eastus")
result = deployer.run_az_command([
    'group', 'create',
    '--name', 'warehouse-returns-prod', 
    '--location', 'eastus'
])
```

##### `create_resource_group()`
**What it does**: Creates Azure resource group if it doesn't exist
- **Idempotency**: Safe to run multiple times
- **Validation**: Checks existing resource group properties
- **Tagging**: Applies standard resource tags
- **Permissions**: Validates deployment account permissions

**Resource Tags Applied**:
```json
{
    "Project": "WarehouseReturns",
    "Environment": "Production", 
    "CreatedBy": "DeploymentScript",
    "CreatedDate": "2024-01-15",
    "CostCenter": "Operations"
}
```

##### `create_storage_account() -> str`
**What it does**: Creates Azure Storage account for Functions and blob storage
- **Naming**: Generates unique storage account name
- **Configuration**: Sets up storage for Functions runtime and document storage
- **Security**: Configures access keys and network rules
- **Redundancy**: Sets appropriate replication options

**Storage Account Features**:
- **Performance**: Standard tier for cost optimization
- **Replication**: LRS (Locally Redundant Storage) for development, GRS for production
- **Blob Storage**: Container for low-confidence documents
- **Functions Storage**: Runtime storage for Azure Functions

##### `create_application_insights() -> str`  
**What it does**: Creates Application Insights for monitoring and logging
- **Integration**: Configures with Azure Functions automatically
- **Telemetry**: Sets up custom metrics and business events
- **Alerting**: Creates default alert rules for errors and performance
- **Retention**: Configures log retention policies

**Monitoring Features**:
- **Performance Monitoring**: Function execution times and throughput
- **Error Tracking**: Exception logging and error rate analysis
- **Custom Events**: Business event tracking and analytics
- **Live Metrics**: Real-time application performance monitoring

##### `create_document_intelligence_service() -> str`
**What it does**: Creates Azure Document Intelligence (Form Recognizer) service
- **Service Creation**: Provisions AI service with appropriate SKU
- **Model Access**: Configures access to custom "serialnumber" model
- **Keys Management**: Retrieves and stores API keys securely
- **Endpoint Configuration**: Sets up service endpoint URLs

**Service Configuration**:
```python
{
    'name': 'warehouse-returns-docintel-prod',
    'sku': 'F0',  # Free tier for development, S0 for production
    'location': 'eastus',
    'kind': 'FormRecognizer',
    'endpoint': 'https://warehouse-returns-docintel-prod.cognitiveservices.azure.com/',
    'api_version': '2024-11-30'
}
```

##### `create_function_apps(storage_account: str) -> Dict[str, str]`
**What it does**: Creates Azure Function Apps for document intelligence and return processing
- **App Creation**: Provisions serverless compute instances
- **Runtime Configuration**: Sets Python 3.11 runtime and Functions v4
- **Storage Integration**: Links to created storage account
- **Scaling**: Configures consumption plan for automatic scaling

**Function Apps Created**:
1. **Document Intelligence App**: `warehouse-returns-document-intelligence-prod`
   - Handles document processing and AI analysis
   - Integrates with Document Intelligence service
   - Manages blob storage for low-confidence documents

2. **Return Processing App**: `warehouse-returns-return-processing-prod`  
   - Manages return workflow and business logic
   - Handles status tracking and notifications
   - Integrates with external systems

##### `configure_app_settings(function_apps: Dict[str, str])`
**What it does**: Configures application settings for all Function Apps
- **Service Connections**: Sets up connection strings and endpoints
- **Environment Variables**: Configures runtime environment
- **Security Settings**: Manages API keys and authentication
- **Feature Flags**: Enables/disables specific functionality

**Configuration Categories**:

**Document Intelligence Settings**:
```bash
DOCUMENT_INTELLIGENCE_ENDPOINT=https://service.cognitiveservices.azure.com/
DOCUMENT_INTELLIGENCE_KEY=your-api-key
DOCUMENT_INTELLIGENCE_API_VERSION=2024-11-30
DEFAULT_MODEL_ID=serialnumber
```

**Storage Configuration**:
```bash
AZURE_STORAGE_CONNECTION_STRING=DefaultEndpointsProtocol=https;AccountName=...
BLOB_CONTAINER_PREFIX=warehouse-returns-doc-intel
ENABLE_BLOB_STORAGE=true
```

**Processing Configuration**:
```bash
CONFIDENCE_THRESHOLD=0.7
MAX_FILE_SIZE_MB=50
SUPPORTED_CONTENT_TYPES=application/pdf,image/jpeg,image/png,image/tiff
```

**Monitoring Configuration**:
```bash
APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=...
LOG_LEVEL=INFO
ENABLE_STRUCTURED_LOGGING=true
ENABLE_REQUEST_LOGGING=true
```

##### `generate_env_file()`
**What it does**: Generates local development configuration files
- **Local Settings**: Creates `local.settings.json` for Azure Functions
- **Environment File**: Creates `.env` file for development
- **Connection Strings**: Includes all service connection information
- **Development Overrides**: Sets development-specific configurations

**Generated Files**:

**local.settings.json**:
```json
{
    "IsEncrypted": false,
    "Values": {
        "AzureWebJobsStorage": "storage-connection-string",
        "FUNCTIONS_WORKER_RUNTIME": "python",
        "DOCUMENT_INTELLIGENCE_ENDPOINT": "https://service-endpoint/",
        "DOCUMENT_INTELLIGENCE_KEY": "api-key",
        "AZURE_STORAGE_CONNECTION_STRING": "storage-connection",
        "BLOB_CONTAINER_PREFIX": "warehouse-returns-doc-intel"
    }
}
```

**Development .env**:
```bash
# Azure Services
AZURE_SUBSCRIPTION_ID=subscription-id
AZURE_RESOURCE_GROUP=warehouse-returns-dev
AZURE_LOCATION=eastus

# Document Intelligence  
DOCUMENT_INTELLIGENCE_ENDPOINT=https://service-endpoint/
DOCUMENT_INTELLIGENCE_KEY=api-key

# Storage
AZURE_STORAGE_CONNECTION_STRING=storage-connection
BLOB_CONTAINER_PREFIX=warehouse-returns-doc-intel

# Environment
WAREHOUSE_RETURNS_ENV=development
LOG_LEVEL=DEBUG
```

### 2. `scripts/build.sh` & `scripts/build.bat` - Build Scripts

#### **Purpose**: Cross-platform build scripts for local development and CI/CD

##### **Linux/macOS Build Script (build.sh)**
```bash
#!/bin/bash
# What it does: Builds and packages Python applications for deployment

# Environment Setup
echo "Setting up Python virtual environment..."
python3 -m venv venv
source venv/bin/activate

# Dependency Installation  
echo "Installing Python dependencies..."
pip install -r requirements.txt

# Application Building
echo "Building Function Apps..."
cd src/document_intelligence
func pack --build-native-deps

cd ../pieceinfo_api  
func pack --build-native-deps

# Testing
echo "Running tests..."
cd ../../
python -m pytest tests/ -v

echo "Build completed successfully!"
```

##### **Windows Build Script (build.bat)**
```batch
@echo off
REM What it does: Windows-compatible build process

echo Setting up Python virtual environment...
python -m venv venv
call venv\Scripts\activate.bat

echo Installing Python dependencies...
pip install -r requirements.txt

echo Building Function Apps...
cd src\document_intelligence
func pack --build-native-deps

cd ..\pieceinfo_api
func pack --build-native-deps

echo Running tests...
cd ..\..\
python -m pytest tests\ -v

echo Build completed successfully!
```

### 3. `scripts/verify_models.py` - Model Validation

#### **Purpose**: Validates Azure Document Intelligence model availability and configuration

##### **Key Functions**:

##### `verify_model_access(endpoint: str, api_key: str, model_id: str) -> bool`
**What it does**: Verifies access to custom Document Intelligence models
- **Authentication**: Tests API key and endpoint connectivity
- **Model Validation**: Confirms custom model availability
- **Version Compatibility**: Checks API version compatibility
- **Performance Testing**: Validates model response times

##### `validate_model_training(model_id: str) -> Dict[str, Any]`
**What it does**: Checks model training status and performance metrics
- **Training Status**: Confirms model is ready for production use
- **Accuracy Metrics**: Retrieves model performance statistics
- **Field Configuration**: Validates expected field extraction capabilities
- **Version Management**: Tracks model version and updates

**Model Validation Report**:
```json
{
    "model_id": "serialnumber",
    "status": "ready", 
    "accuracy": 0.94,
    "last_trained": "2024-01-10T15:30:00.000Z",
    "supported_fields": ["Serial"],
    "document_types": ["warehouse_return", "invoice", "receipt"],
    "api_version_compatibility": ["2024-11-30", "2023-07-31"]
}
```

## 🚀 Deployment Workflows

### Development Environment Setup
```bash
# 1. Clone repository
git clone https://github.com/company/warehouse-returns.git
cd warehouse-returns

# 2. Install dependencies
pip install -r requirements.txt

# 3. Configure local settings
cp local.settings.example.json src/document_intelligence/local.settings.json
# Edit local.settings.json with your Azure service keys

# 4. Start local Functions
cd src/document_intelligence
func start --port 7071

# 5. Test endpoints
curl -X GET http://localhost:7071/api/health
```

### Production Deployment
```bash
# 1. Authenticate with Azure
az login
az account set --subscription "your-subscription-id"

# 2. Run deployment script
python scripts/deploy.py \
    --resource-group "warehouse-returns-prod" \
    --location "eastus" \
    --environment "production"

# 3. Deploy application code
cd src/document_intelligence
func azure functionapp publish warehouse-returns-document-intelligence-prod

cd ../pieceinfo_api
func azure functionapp publish warehouse-returns-pieceinfo-api-prod

# 4. Verify deployment
python scripts/verify_models.py --environment "production"
```

### CI/CD Pipeline Integration
```yaml
# Azure DevOps Pipeline (azure-pipelines.yml)
trigger:
  branches:
    include:
    - main
    - develop

variables:
  pythonVersion: '3.11'

stages:
- stage: Build
  jobs:
  - job: BuildAndTest
    pool:
      vmImage: 'ubuntu-latest'
    steps:
    - task: UsePythonVersion@0
      inputs:
        versionSpec: '$(pythonVersion)'
    - script: |
        pip install -r requirements.txt
        python -m pytest tests/ --junitxml=test-results.xml
    - task: PublishTestResults@2
      inputs:
        testResultsFiles: 'test-results.xml'

- stage: Deploy
  condition: and(succeeded(), eq(variables['Build.SourceBranch'], 'refs/heads/main'))
  jobs:
  - deployment: DeployProduction
    environment: 'warehouse-returns-production'
    strategy:
      runOnce:
        deploy:
          steps:
          - script: |
              python scripts/deploy.py \
                --resource-group "$(RESOURCE_GROUP)" \
                --location "$(AZURE_LOCATION)"
```

## 🔧 Infrastructure Configuration

### Resource Naming Conventions
- **Resource Group**: `warehouse-returns-{environment}`
- **Storage Account**: `warehousereturns{env}{random}`  
- **Function Apps**: `warehouse-returns-{service}-{environment}`
- **Document Intelligence**: `warehouse-returns-docintel-{environment}`
- **Application Insights**: `warehouse-returns-appinsights-{environment}`

### Environment Management
- **Development**: Local Functions runtime with Azure service connections
- **Staging**: Separate resource group with scaled-down SKUs
- **Production**: Full-scale resources with monitoring and alerting

### Security Configuration
- **Managed Identity**: Used where possible for service-to-service authentication
- **Key Vault Integration**: Secure storage for connection strings and API keys
- **Network Security**: VNet integration for production environments
- **Access Control**: RBAC configuration for deployment and management

This deployment system provides a robust, automated foundation for managing Azure infrastructure and application deployments across multiple environments while maintaining security, monitoring, and operational best practices.
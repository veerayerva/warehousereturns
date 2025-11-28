# Azure Infrastructure Documentation
## Vendor Returns Processing System - Complete Infrastructure Specification

**Document Version:** 1.0  
**Last Updated:** 2024  
**Purpose:** Complete Azure infrastructure specification for Terraform deployment

---

## Table of Contents
1. [System Architecture Overview](#system-architecture-overview)
2. [Resource Groups](#resource-groups)
3. [Azure Resources by Service](#azure-resources-by-service)
4. [Azure AD & Identity](#azure-ad--identity)
5. [Networking](#networking)
6. [Security & Secrets Management](#security--secrets-management)
7. [Monitoring & Logging](#monitoring--logging)
8. [Resource Dependencies](#resource-dependencies)
9. [Deployment Order](#deployment-order)
10. [Configuration Management](#configuration-management)
11. [Environment Strategy](#environment-strategy)

---

## System Architecture Overview

### Three-Service Architecture
```
VendorReturnsService (.NET 8)
    ├─> Document Intelligence API (Python) ─> Azure Document Intelligence
    ├─> PieceInfo API (Python) ─> External APIs (via API Management)
    └─> SharePoint Online (via Microsoft Graph)
```

### Service Descriptions

**1. VendorReturnsService** (Orchestrator - .NET 8 Isolated)
- **Purpose:** Orchestrates vendor return processing workflow
- **Functions:** 
  - `ProcessReturnItem` (HTTP Trigger)
  - `ProcessPendingReturns` (Timer Trigger)
- **Location:** `srccsharp/VendorReturnsService/`

**2. Document Intelligence API** (Python 3.11)
- **Purpose:** Wrapper for Azure Document Intelligence, extracts serial/piece numbers from images
- **Port:** 7075 (local), HTTP endpoint in Azure
- **Location:** `src/document_intelligence/`

**3. PieceInfo API** (Python 3.11)
- **Purpose:** Aggregates data from 3 external APIs (inventory location, product master, vendor details)
- **Port:** 7074 (local), HTTP endpoint in Azure
- **External Dependency:** https://apim-dev.nfm.com/ihubservices
- **Location:** `src/pieceinfo_api/`

---

## Resource Groups

### Recommended Resource Group Structure

#### Option 1: By Environment
```
rg-vendorreturns-dev-eastus
rg-vendorreturns-staging-eastus
rg-vendorreturns-prod-eastus
```

#### Option 2: By Service (Recommended for Production)
```
# Core Infrastructure (shared across environments)
rg-vendorreturns-core-prod-eastus
  - Azure Document Intelligence
  - Storage Account (Document Intelligence blob storage)
  - Application Insights (workspace-level)
  - Log Analytics Workspace

# Application Services
rg-vendorreturns-apps-prod-eastus
  - Azure Functions (all 3 apps)
  - App Service Plans
  - Key Vault

# Identity & Security
rg-vendorreturns-identity-prod-eastus
  - Managed Identities
  - Azure AD App Registration (reference only)
```

### Naming Convention
```
{resource-type}-{workload}-{service}-{environment}-{region}

Examples:
- func-vendorreturns-orchestrator-prod-eastus
- func-vendorreturns-docintell-prod-eastus
- func-vendorreturns-pieceinfo-prod-eastus
- ai-vendorreturns-prod-eastus
- st-vendorreturns-prod-eastus (storage accounts: max 24 chars, no hyphens)
- kv-vendorreturns-prod-eus (key vaults: max 24 chars)
```

---

## Azure Resources by Service

### 1. VendorReturnsService (.NET 8 Orchestrator)

#### Azure Function App
```yaml
Resource Type: Microsoft.Web/sites
SKU: EP1 (Elastic Premium) or Y1 (Consumption)
Runtime: .NET 8 Isolated
OS: Windows or Linux

Configuration:
  Name: func-vendorreturns-orchestrator-{env}-eastus
  Runtime Stack: .NET|8-isolated
  Always On: true (if using Premium plan)
  
App Settings:
  # SharePoint Configuration
  SharePoint__TenantId: "842eb59b-6f77-4fb7-bf9a-d56cf7a6e7c4"
  SharePoint__ClientId: "<from Azure AD App Registration>"
  SharePoint__UseCertificate: "true"
  SharePoint__CertificateThumbprint: "48CC926D825D5AD9849FC4E91A22AE77FC78F5A4"
  SharePoint__ClientSecret: "@Microsoft.KeyVault(SecretUri=<keyvault-uri>)"
  SharePoint__SiteUrl: "https://nfm365.sharepoint.com/sites/vrp"
  SharePoint__SiteName: "vrp"
  SharePoint__ListId: "7c0ae31e-adcf-456f-ad03-8d0a17aba9d1"
  SharePoint__DriveId: "b!mRHHFnDbS0qhzW5PdWHBQw85ZdKj8O5DpC6WYwi_V4a6Qq9F9sVeS6RaKHWPHzxA"
  SharePoint__ImageSourceType: "SharePoint"
  
  # Document Intelligence API Configuration
  DocumentIntelligence__ApiEndpoint: "https://func-vendorreturns-docintell-{env}-eastus.azurewebsites.net/api"
  DocumentIntelligence__ApiKey: "@Microsoft.KeyVault(SecretUri=<keyvault-uri>)"
  DocumentIntelligence__TimeoutSeconds: "300"
  DocumentIntelligence__MaxRetries: "3"
  
  # PieceInfo API Configuration
  PieceInfo__ApiBaseUrl: "https://func-vendorreturns-pieceinfo-{env}-eastus.azurewebsites.net/api"
  PieceInfo__ApiKey: "@Microsoft.KeyVault(SecretUri=<keyvault-uri>)"
  
  # Processing Configuration
  Processing__Schedule: "0 */5 * * * *"  # Every 5 minutes
  Processing__MaxParallelItems: "5"
  
  # Timer Trigger Control
  AzureWebJobs.ProcessPendingReturns.Disabled: "false"
  
  # Application Insights
  APPLICATIONINSIGHTS_CONNECTION_STRING: "<connection-string>"
  
  # Functions Runtime
  FUNCTIONS_WORKER_RUNTIME: "dotnet-isolated"
  FUNCTIONS_EXTENSION_VERSION: "~4"
```

#### Managed Identity
```yaml
Resource Type: Microsoft.ManagedIdentity/userAssignedIdentities
Name: id-vendorreturns-orchestrator-{env}-eastus

Assignments:
  - Key Vault: Get Secrets
  - SharePoint: Via Azure AD App Registration (not Managed Identity)
```

---

### 2. Document Intelligence API (Python)

#### Azure Function App
```yaml
Resource Type: Microsoft.Web/sites
SKU: EP1 (Elastic Premium) or Y1 (Consumption)
Runtime: Python 3.11
OS: Linux (required for Python)

Configuration:
  Name: func-vendorreturns-docintell-{env}-eastus
  Runtime Stack: Python|3.11
  Always On: true (if using Premium plan)
  Enable CORS: Configure allowed origins
  
App Settings:
  # Azure Document Intelligence
  DOCUMENT_INTELLIGENCE_ENDPOINT: "https://cog-vendorreturns-docintell-{env}-eastus.cognitiveservices.azure.com/"
  DOCUMENT_INTELLIGENCE_KEY: "@Microsoft.KeyVault(SecretUri=<keyvault-uri>)"
  DOCUMENT_INTELLIGENCE_API_VERSION: "2024-11-30"
  DEFAULT_MODEL_ID: "serialnumber"
  
  # Azure Blob Storage
  AZURE_STORAGE_CONNECTION_STRING: "@Microsoft.KeyVault(SecretUri=<keyvault-uri>)"
  BLOB_CONTAINER_PREFIX: "warehouse-returns-doc-intel"
  ENABLE_BLOB_STORAGE: "true"
  
  # Document Processing Configuration
  CONFIDENCE_THRESHOLD: "0.3"
  MAX_FILE_SIZE_MB: "50"
  SUPPORTED_CONTENT_TYPES: "application/pdf,image/jpeg,image/jpg,image/png,image/bmp,image/tiff"
  
  # Retry Configuration
  AZURE_API_RETRY_ATTEMPTS: "3"
  AZURE_API_RETRY_DELAY: "2"
  AZURE_API_TIMEOUT: "300"
  
  # Application Insights
  APPINSIGHTS_INSTRUMENTATIONKEY: "<instrumentation-key>"
  APPLICATIONINSIGHTS_CONNECTION_STRING: "<connection-string>"
  LOG_LEVEL: "INFO"
  ENABLE_STRUCTURED_LOGGING: "true"
  
  # Functions Runtime
  AzureWebJobsStorage: "<storage-connection-string>"
  FUNCTIONS_WORKER_RUNTIME: "python"
  FUNCTIONS_WORKER_RUNTIME_VERSION: "3.11"
  FUNCTIONS_EXTENSION_VERSION: "~4"
  
  # Environment
  AZURE_FUNCTIONS_ENVIRONMENT: "Production"
  ENABLE_DEBUG: "false"
```

#### Managed Identity
```yaml
Resource Type: Microsoft.ManagedIdentity/userAssignedIdentities
Name: id-vendorreturns-docintell-{env}-eastus

Assignments:
  - Azure Document Intelligence: Cognitive Services User
  - Storage Account: Storage Blob Data Contributor
  - Key Vault: Get Secrets
```

---

### 3. PieceInfo API (Python)

#### Azure Function App
```yaml
Resource Type: Microsoft.Web/sites
SKU: EP1 (Elastic Premium) or Y1 (Consumption)
Runtime: Python 3.11
OS: Linux (required for Python)

Configuration:
  Name: func-vendorreturns-pieceinfo-{env}-eastus
  Runtime Stack: Python|3.11
  Always On: true (if using Premium plan)
  
App Settings:
  # External API Configuration
  EXTERNAL_API_BASE_URL: "https://apim-dev.nfm.com/ihubservices"
  EXTERNAL_API_KEY: "@Microsoft.KeyVault(SecretUri=<keyvault-uri>)"
  
  # API Endpoints (relative to base URL)
  INVENTORY_LOCATION_API_PATH: "/api/v1/inventory/location"
  PRODUCT_MASTER_API_PATH: "/api/v1/product/master"
  VENDOR_DETAILS_API_PATH: "/api/v1/vendor/details"
  
  # HTTP Configuration
  REQUEST_TIMEOUT_SECONDS: "30"
  MAX_RETRIES: "3"
  ENABLE_SSL_VERIFICATION: "true"
  
  # Application Insights
  APPINSIGHTS_INSTRUMENTATIONKEY: "<instrumentation-key>"
  APPLICATIONINSIGHTS_CONNECTION_STRING: "<connection-string>"
  LOG_LEVEL: "INFO"
  
  # Functions Runtime
  AzureWebJobsStorage: "<storage-connection-string>"
  FUNCTIONS_WORKER_RUNTIME: "python"
  FUNCTIONS_WORKER_RUNTIME_VERSION: "3.11"
  FUNCTIONS_EXTENSION_VERSION: "~4"
  
  # Environment
  AZURE_FUNCTIONS_ENVIRONMENT: "Production"
```

#### Managed Identity
```yaml
Resource Type: Microsoft.ManagedIdentity/userAssignedIdentities
Name: id-vendorreturns-pieceinfo-{env}-eastus

Assignments:
  - Key Vault: Get Secrets
  - External API: Requires API key (managed in Key Vault)
```

---

### 4. Azure Document Intelligence (Cognitive Services)

#### Document Intelligence Resource
```yaml
Resource Type: Microsoft.CognitiveServices/accounts
Kind: FormRecognizer (Document Intelligence)
SKU: S0 (Standard)

Configuration:
  Name: cog-vendorreturns-docintell-{env}-eastus
  Location: East US
  API Version: 2024-11-30
  
Custom Models:
  - Model Name: "serialnumber"
    Purpose: Extract serial numbers and piece numbers from images
    Training Data: Required - customer-specific labeled dataset
    
Custom Model Deployment:
  Method: Azure Document Intelligence Studio or API
  Training Dataset Location: Azure Blob Storage container
  Model Deployment: After training, assign to this resource
  
Endpoints:
  - https://cog-vendorreturns-docintell-{env}-eastus.cognitiveservices.azure.com/
```

**CRITICAL REQUIREMENT:** Custom model "serialnumber" must be trained and deployed before Document Intelligence API can function. This requires:
1. Labeled training dataset stored in blob storage
2. Training via Document Intelligence Studio or API
3. Model deployment to the Document Intelligence resource

---

### 5. Storage Account (Document Intelligence)

#### Storage Account
```yaml
Resource Type: Microsoft.Storage/storageAccounts
SKU: Standard_LRS or Standard_GRS
Kind: StorageV2

Configuration:
  Name: stvendorreturnsprod (max 24 chars, no hyphens)
  Location: East US
  Replication: LRS (dev/staging) or GRS (production)
  Access Tier: Hot
  HTTPS Only: true
  Minimum TLS Version: TLS1_2
  
Containers:
  - warehouse-returns-doc-intel
    Purpose: Store processed documents and training data
    Access Level: Private
    
  - warehouse-returns-model-training (optional)
    Purpose: Store labeled training data for custom models
    Access Level: Private
```

---

### 6. App Service Plans

#### Option 1: Consumption Plan (Y1)
```yaml
Resource Type: Microsoft.Web/serverfarms
SKU: Y1 (Dynamic)

Pros:
  - Pay-per-execution
  - Auto-scaling
  - Low cost for low-volume workloads
  
Cons:
  - Cold start delays
  - 5-minute execution timeout
  - Limited instance memory
```

#### Option 2: Elastic Premium Plan (EP1) - RECOMMENDED
```yaml
Resource Type: Microsoft.Web/serverfarms
SKU: EP1, EP2, or EP3

Configuration:
  Name: plan-vendorreturns-{env}-eastus
  OS: Linux (for Python apps) or Windows (for .NET app)
  
Note: May need separate plans for Linux and Windows apps

Pros:
  - No cold starts (Always On)
  - VNet integration support
  - Longer execution timeout (unlimited with Always On)
  - Better performance and memory
  
Recommended Configuration:
  - plan-vendorreturns-linux-{env}-eastus (Python apps)
  - plan-vendorreturns-windows-{env}-eastus (.NET app)
```

---

### 7. Application Insights

#### Application Insights Resources

**Option 1: Single Instance (Recommended for cost optimization)**
```yaml
Resource Type: Microsoft.Insights/components
Kind: web

Configuration:
  Name: ai-vendorreturns-{env}-eastus
  Application Type: web
  Workspace-based: true
  Log Analytics Workspace: Required
  
Used By:
  - VendorReturnsService
  - Document Intelligence API
  - PieceInfo API
```

**Option 2: Per-Service Instances (Better isolation)**
```yaml
ai-vendorreturns-orchestrator-{env}-eastus
ai-vendorreturns-docintell-{env}-eastus
ai-vendorreturns-pieceinfo-{env}-eastus
```

#### Log Analytics Workspace
```yaml
Resource Type: Microsoft.OperationalInsights/workspaces

Configuration:
  Name: log-vendorreturns-{env}-eastus
  SKU: PerGB2018
  Retention Days: 30 (dev) / 90 (prod)
  
Data Sources:
  - Application Insights (all 3 apps)
  - Function Apps diagnostic logs
  - Storage Account diagnostic logs
  - Document Intelligence diagnostic logs
```

---

### 8. Azure Key Vault

#### Key Vault
```yaml
Resource Type: Microsoft.KeyVault/vaults
SKU: Standard

Configuration:
  Name: kv-vendorreturns-prod (max 24 chars)
  Location: East US
  Enable RBAC: true (recommended) or Access Policies
  Soft Delete: Enabled
  Purge Protection: Enabled (production)
  
Secrets:
  # SharePoint
  - SharePointClientSecret
  - SharePointCertificate (PFX file, base64 encoded)
  
  # Document Intelligence
  - DocumentIntelligenceApiKey (for Document Intelligence API authentication)
  - DocumentIntelligenceKey (for Azure DI service)
  
  # PieceInfo
  - PieceInfoApiKey
  - ExternalApiKey (for apim-dev.nfm.com)
  
  # Storage
  - StorageConnectionString
  
Access Policies / RBAC Assignments:
  - VendorReturnsService Managed Identity: Get Secrets
  - Document Intelligence API Managed Identity: Get Secrets
  - PieceInfo API Managed Identity: Get Secrets
  - DevOps Service Principal: All permissions (for deployment)
```

---

## Azure AD & Identity

### Azure AD App Registration

#### App Registration for SharePoint Access
```yaml
Name: VendorReturnsService-SharePoint-{env}
Application (client) ID: <to be created>
Directory (tenant) ID: 842eb59b-6f77-4fb7-bf9a-d56cf7a6e7c4

API Permissions:
  Microsoft Graph:
    - Sites.ReadWrite.All (Application permission)
    - Sites.FullControl.All (Application permission)
  
  SharePoint:
    - Sites.ReadWrite.All (Application permission)
    
Authentication:
  Preferred: Certificate-based authentication
    - Certificate Thumbprint: 48CC926D825D5AD9849FC4E91A22AE77FC78F5A4
    - Certificate: Upload .cer file to Azure AD
    - Private Key (.pfx): Store in Key Vault
  
  Fallback: Client Secret
    - Secret: Store in Key Vault
    - Expiry: 24 months maximum, set calendar reminder

Admin Consent: REQUIRED - must be granted by tenant administrator

Service Principal Assignment:
  - SharePoint Site Collection Administrator access to:
    https://nfm365.sharepoint.com/sites/vrp
```

#### Certificate Requirements
```yaml
Certificate Type: X.509
Key Size: 2048-bit or higher
Hash Algorithm: SHA-256
Format: 
  - .cer (public key) - upload to Azure AD
  - .pfx (private key) - store in Key Vault with password

Certificate Storage:
  - Azure Key Vault secret: SharePointCertificate
  - Value: Base64-encoded .pfx file
  - Content Type: application/x-pkcs12
  
Certificate Deployment:
  - Function App: Reference Key Vault secret
  - Load certificate in application startup
  - Use AzureCertificateCredential from Azure.Identity
```

### Managed Identities Summary

```yaml
VendorReturnsService:
  Identity: id-vendorreturns-orchestrator-{env}-eastus
  RBAC Assignments:
    - Key Vault: Key Vault Secrets User
    - (SharePoint via App Registration, not Managed Identity)

Document Intelligence API:
  Identity: id-vendorreturns-docintell-{env}-eastus
  RBAC Assignments:
    - Document Intelligence: Cognitive Services User
    - Storage Account: Storage Blob Data Contributor
    - Key Vault: Key Vault Secrets User

PieceInfo API:
  Identity: id-vendorreturns-pieceinfo-{env}-eastus
  RBAC Assignments:
    - Key Vault: Key Vault Secrets User
```

---

## Networking

### VNet Integration (Optional - Recommended for Production)

#### Virtual Network
```yaml
Resource Type: Microsoft.Network/virtualNetworks

Configuration:
  Name: vnet-vendorreturns-{env}-eastus
  Address Space: 10.0.0.0/16
  
Subnets:
  - snet-functions-{env}
    Address Range: 10.0.1.0/24
    Purpose: Function Apps VNet integration
    Delegation: Microsoft.Web/serverFarms
    
  - snet-privateendpoints-{env}
    Address Range: 10.0.2.0/24
    Purpose: Private endpoints for PaaS services
```

#### Private Endpoints (Production)
```yaml
Resources with Private Endpoints:
  - Storage Account
    Purpose: Secure blob storage access
    
  - Key Vault
    Purpose: Secure secret access
    
  - Document Intelligence (if supported in region)
    Purpose: Secure API access
```

#### Network Security Groups
```yaml
NSG for Function Apps Subnet:
  Inbound Rules:
    - Allow HTTPS (443) from VNet
    - Deny all other inbound
    
  Outbound Rules:
    - Allow HTTPS (443) to Internet (for SharePoint, external APIs)
    - Allow Azure services
```

### Outbound Dependencies
```yaml
VendorReturnsService requires outbound access to:
  - https://nfm365.sharepoint.com (SharePoint)
  - https://func-vendorreturns-docintell-*.azurewebsites.net (Document Intelligence API)
  - https://func-vendorreturns-pieceinfo-*.azurewebsites.net (PieceInfo API)
  - https://login.microsoftonline.com (Azure AD authentication)
  - https://*.vault.azure.net (Key Vault)
  
Document Intelligence API requires outbound access to:
  - https://cog-vendorreturns-docintell-*.cognitiveservices.azure.com (Azure DI)
  - https://*.blob.core.windows.net (Storage)
  - https://*.vault.azure.net (Key Vault)
  
PieceInfo API requires outbound access to:
  - https://apim-dev.nfm.com (External API Management)
  - https://*.vault.azure.net (Key Vault)
```

---

## Security & Secrets Management

### Secrets Inventory

| Secret Name | Used By | Storage Location | Rotation Frequency |
|-------------|---------|------------------|-------------------|
| SharePointClientSecret | VendorReturnsService | Key Vault | 12 months |
| SharePointCertificate | VendorReturnsService | Key Vault | 24 months |
| DocumentIntelligenceApiKey | VendorReturnsService | Key Vault | Manual rotation |
| DocumentIntelligenceKey | Document Intelligence API | Key Vault | Azure-managed |
| PieceInfoApiKey | VendorReturnsService | Key Vault | Manual rotation |
| ExternalApiKey | PieceInfo API | Key Vault | Per external vendor policy |
| StorageConnectionString | Document Intelligence API | Key Vault | Azure-managed |

### RBAC Role Assignments

#### VendorReturnsService Function App
```yaml
Scope: Key Vault
Role: Key Vault Secrets User
Principal: id-vendorreturns-orchestrator-{env}-eastus
```

#### Document Intelligence API Function App
```yaml
Scope: Document Intelligence Resource
Role: Cognitive Services User
Principal: id-vendorreturns-docintell-{env}-eastus

Scope: Storage Account
Role: Storage Blob Data Contributor
Principal: id-vendorreturns-docintell-{env}-eastus

Scope: Key Vault
Role: Key Vault Secrets User
Principal: id-vendorreturns-docintell-{env}-eastus
```

#### PieceInfo API Function App
```yaml
Scope: Key Vault
Role: Key Vault Secrets User
Principal: id-vendorreturns-pieceinfo-{env}-eastus
```

### SharePoint Permissions

#### Service Principal Permissions
```yaml
Site Collection: https://nfm365.sharepoint.com/sites/vrp
Service Principal: VendorReturnsService-SharePoint-{env}

Required Permissions:
  - Site Collection Administrator (full access)
  OR
  - Contribute permission on the specific list (minimum)
  
List Access:
  - List ID: 7c0ae31e-adcf-456f-ad03-8d0a17aba9d1
  - Required: Read, Write items
  
Drive Access:
  - Drive ID: b!mRHHFnDbS0qhzW5PdWHBQw85ZdKj8O5DpC6WYwi_V4a6Qq9F9sVeS6RaKHWPHzxA
  - Required: Read files
```

#### Grant Permissions via PowerShell
```powershell
# Connect to SharePoint Online
Connect-PnPOnline -Url "https://nfm365.sharepoint.com/sites/vrp" -Interactive

# Grant Service Principal permissions
Grant-PnPAzureADAppSitePermission `
  -AppId "<client-id-from-app-registration>" `
  -DisplayName "VendorReturnsService-SharePoint-{env}" `
  -Permissions FullControl
```

---

## Monitoring & Logging

### Application Insights Monitoring

#### Custom Metrics to Track
```yaml
VendorReturnsService:
  - ProcessReturnItem execution duration
  - SharePoint API call success/failure rate
  - Document Intelligence API response time
  - PieceInfo API response time
  - Items processed per hour
  - Error rate by service dependency
  
Document Intelligence API:
  - Document analysis duration
  - Azure DI API response time
  - Blob storage upload/download metrics
  - Confidence score distribution
  
PieceInfo API:
  - External API aggregation time
  - Individual API response times
  - Cache hit/miss ratio (if caching implemented)
```

#### Alerts to Configure
```yaml
Critical Alerts:
  - VendorReturnsService function failures > 5 in 5 minutes
  - Document Intelligence API availability < 95%
  - PieceInfo API availability < 95%
  - SharePoint authentication failures
  - Key Vault access denied errors
  
Warning Alerts:
  - Response time > 30 seconds (any API)
  - Document confidence score < 0.3 (frequent occurrences)
  - Storage account throttling
```

### Diagnostic Settings

#### Enable for All Resources
```yaml
Log Categories to Capture:
  - FunctionAppLogs
  - AllMetrics
  - Audit logs (Key Vault, Storage)
  
Destination:
  - Log Analytics Workspace: log-vendorreturns-{env}-eastus
  - Storage Account (optional, for long-term retention)
  
Retention:
  - Development: 30 days
  - Production: 90 days minimum
```

---

## Resource Dependencies

### Dependency Graph
```
┌─────────────────────────────────────────────────────────────┐
│                   External Dependencies                     │
├─────────────────────────────────────────────────────────────┤
│  • SharePoint Online (nfm365.sharepoint.com)               │
│  • External API Management (apim-dev.nfm.com)              │
│  • Azure AD (login.microsoftonline.com)                    │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                VendorReturnsService (.NET 8)                │
│  Functions: ProcessReturnItem, ProcessPendingReturns       │
└─────────────────┬────────────────────┬──────────────────────┘
                  │                    │
         ┌────────▼─────────┐   ┌──────▼───────────────┐
         │ Document         │   │ PieceInfo API        │
         │ Intelligence API │   │ (Python 3.11)        │
         │ (Python 3.11)    │   └──────┬───────────────┘
         └────────┬─────────┘          │
                  │                    │
         ┌────────▼─────────┐   ┌──────▼───────────────┐
         │ Azure Document   │   │ External APIs        │
         │ Intelligence     │   │ (via API Mgmt)       │
         │ + Custom Model   │   └──────────────────────┘
         └────────┬─────────┘
                  │
         ┌────────▼─────────┐
         │ Blob Storage     │
         │ (Document Store) │
         └──────────────────┘

┌─────────────────────────────────────────────────────────────┐
│                     Shared Services                         │
├─────────────────────────────────────────────────────────────┤
│  • Azure Key Vault (secrets for all apps)                  │
│  • Application Insights (telemetry for all apps)           │
│  • Log Analytics Workspace (centralized logging)           │
└─────────────────────────────────────────────────────────────┘
```

### Resource Dependency Table

| Resource | Depends On | Dependency Type |
|----------|-----------|-----------------|
| VendorReturnsService | Document Intelligence API | HTTP call |
| VendorReturnsService | PieceInfo API | HTTP call |
| VendorReturnsService | SharePoint Online | Microsoft Graph API |
| VendorReturnsService | Azure AD App Registration | Authentication |
| VendorReturnsService | Key Vault | Secrets retrieval |
| Document Intelligence API | Azure Document Intelligence | Azure SDK |
| Document Intelligence API | Storage Account | Blob storage |
| Document Intelligence API | Key Vault | Secrets retrieval |
| PieceInfo API | External API Management | HTTP calls |
| PieceInfo API | Key Vault | Secrets retrieval |
| All Function Apps | Application Insights | Telemetry |
| All Function Apps | App Service Plan | Hosting |
| Application Insights | Log Analytics Workspace | Log storage |

---

## Deployment Order

### Phase 1: Foundation Infrastructure
```yaml
1. Resource Groups
   - Create all resource groups per environment

2. Networking (if using VNet)
   - Virtual Network
   - Subnets
   - Network Security Groups

3. Log Analytics Workspace
   - Required before Application Insights

4. Application Insights
   - Single instance or per-service instances
```

### Phase 2: Identity & Security
```yaml
5. Managed Identities
   - Create user-assigned identities for all 3 function apps

6. Azure Key Vault
   - Create Key Vault
   - Configure RBAC or access policies
   - Upload initial secrets (manual step)

7. Azure AD App Registration (Manual)
   - Create app registration for SharePoint access
   - Upload certificate or create client secret
   - Request admin consent for API permissions
   - Grant SharePoint site permissions
```

### Phase 3: Core Services
```yaml
8. Storage Account
   - Create storage account for Document Intelligence
   - Create containers
   - Configure RBAC for Document Intelligence API managed identity

9. Azure Document Intelligence
   - Create Cognitive Services resource
   - Note: Custom model training is a separate post-deployment step
```

### Phase 4: Compute Resources
```yaml
10. App Service Plans
    - plan-vendorreturns-linux-{env}-eastus (for Python apps)
    - plan-vendorreturns-windows-{env}-eastus (for .NET app) OR single plan

11. Document Intelligence API Function App
    - Create function app
    - Assign managed identity
    - Configure app settings (reference Key Vault)
    - Enable Application Insights

12. PieceInfo API Function App
    - Create function app
    - Assign managed identity
    - Configure app settings (reference Key Vault)
    - Enable Application Insights

13. VendorReturnsService Function App
    - Create function app
    - Assign managed identity
    - Configure app settings (reference Key Vault)
    - Enable Application Insights
```

### Phase 5: Application Deployment
```yaml
14. Deploy Function App Code
    - Document Intelligence API (Python)
    - PieceInfo API (Python)
    - VendorReturnsService (.NET)

15. Configure Diagnostic Settings
    - All function apps → Log Analytics
    - Storage account → Log Analytics
    - Document Intelligence → Log Analytics

16. Set up Monitoring Alerts
    - Create action groups
    - Configure metric alerts
    - Set up availability tests (optional)
```

### Phase 6: Post-Deployment
```yaml
17. Custom Model Training (CRITICAL)
    - Upload labeled training data to blob storage
    - Train "serialnumber" custom model via Document Intelligence Studio
    - Deploy model to Document Intelligence resource
    - Validate model endpoint and ID

18. Validation Testing
    - Test Document Intelligence API independently
    - Test PieceInfo API independently
    - Test VendorReturnsService end-to-end
    - Verify SharePoint integration
    - Validate monitoring and logging
```

---

## Configuration Management

### Environment-Specific Configuration

#### Development
```yaml
Resource SKUs:
  - Function Apps: Consumption (Y1) or EP1
  - Document Intelligence: S0
  - Storage: Standard_LRS
  
Configuration:
  - AZURE_FUNCTIONS_ENVIRONMENT: "Development"
  - LOG_LEVEL: "DEBUG"
  - ENABLE_DEBUG: "true"
  - Processing__Schedule: "0 */15 * * * *" (every 15 minutes)
  - AzureWebJobs.ProcessPendingReturns.Disabled: "true" (disable timer for dev)
```

#### Staging
```yaml
Resource SKUs:
  - Function Apps: EP1
  - Document Intelligence: S0
  - Storage: Standard_LRS
  
Configuration:
  - AZURE_FUNCTIONS_ENVIRONMENT: "Staging"
  - LOG_LEVEL: "INFO"
  - ENABLE_DEBUG: "false"
  - Processing__Schedule: "0 */10 * * * *" (every 10 minutes)
  - AzureWebJobs.ProcessPendingReturns.Disabled: "false"
```

#### Production
```yaml
Resource SKUs:
  - Function Apps: EP2 or EP3 (higher tier for performance)
  - Document Intelligence: S0
  - Storage: Standard_GRS (geo-redundant)
  
Configuration:
  - AZURE_FUNCTIONS_ENVIRONMENT: "Production"
  - LOG_LEVEL: "INFO"
  - ENABLE_DEBUG: "false"
  - Processing__Schedule: "0 */5 * * * *" (every 5 minutes)
  - AzureWebJobs.ProcessPendingReturns.Disabled: "false"
  - Purge Protection: Enabled (Key Vault)
  - Always On: true
```

### Configuration Validation Checklist

- [ ] All Key Vault references use proper syntax: `@Microsoft.KeyVault(SecretUri=...)`
- [ ] Managed identities assigned to all function apps
- [ ] RBAC permissions granted for all identities
- [ ] SharePoint app registration has admin consent
- [ ] Application Insights connection strings configured
- [ ] CORS configured for APIs (if accessed from browser)
- [ ] Timer trigger disabled in non-production environments (if desired)
- [ ] Custom Document Intelligence model deployed and tested
- [ ] External API connectivity validated
- [ ] SharePoint site/list/drive IDs verified

---

## Environment Strategy

### Recommended Environment Topology

#### Development
```yaml
Purpose: Developer testing, feature development
Deployment: Manual or CI trigger on feature branch
Data: Synthetic/test data only
SharePoint: Separate test site collection

Resources:
  - All resources in rg-vendorreturns-dev-eastus
  - Lower SKUs (Consumption or EP1)
  - Shared Application Insights
```

#### Staging
```yaml
Purpose: Pre-production validation, integration testing
Deployment: Automated CI/CD on main branch
Data: Sanitized production data or comprehensive test dataset
SharePoint: Staging site collection with production-like schema

Resources:
  - All resources in rg-vendorreturns-staging-eastus
  - Production-like SKUs (EP1/EP2)
  - Separate Application Insights
```

#### Production
```yaml
Purpose: Live workload
Deployment: Automated CD with approval gates
Data: Production data
SharePoint: Production site (https://nfm365.sharepoint.com/sites/vrp)

Resources:
  - Resources in rg-vendorreturns-*-prod-eastus (by service grouping)
  - Production SKUs (EP2/EP3, GRS storage)
  - Dedicated Application Insights
  - VNet integration and private endpoints
  - Geo-redundancy considerations
```

---

## SharePoint Schema Requirements

### List Schema
```yaml
List Name: <to be determined>
List ID: 7c0ae31e-adcf-456f-ad03-8d0a17aba9d1

Required Fields:
  - VendorReturnId (Single line of text)
  - ItemImageId (Single line of text) - reference to file in Drive
  - SerialNumber (Single line of text) - populated by system
  - PieceNumber (Single line of text) - populated by system
  - ProcessingStatus (Choice: Pending, Processing, Completed, Failed)
  - ProcessedDate (Date and Time)
  - ErrorMessage (Multiple lines of text, optional)
  - InventoryLocation (Single line of text) - from PieceInfo API
  - ProductDescription (Single line of text) - from PieceInfo API
  - VendorName (Single line of text) - from PieceInfo API
  - CreatedDate (Date and Time)
  - ModifiedDate (Date and Time)
```

### Drive/Document Library
```yaml
Drive ID: b!mRHHFnDbS0qhzW5PdWHBQw85ZdKj8O5DpC6WYwi_V4a6Qq9F9sVeS6RaKHWPHzxA

Purpose: Store vendor return item images
File Types: JPEG, PNG, BMP, TIFF, PDF
Max File Size: 50 MB

Folder Structure (suggested):
  /VendorReturns/
    /Pending/      - New images to process
    /Processed/    - Successfully processed images
    /Failed/       - Failed processing images
```

---

## Cost Estimation (Monthly - Production)

### Compute
```yaml
Function Apps (3x EP1 instances):
  - $164/month per EP1 instance = $492/month
  OR Consumption (low volume):
  - First 1M executions free
  - $0.20 per million executions + $0.000016/GB-s

Recommendation: EP1 for production (no cold starts, better performance)
```

### Storage
```yaml
Storage Account (Standard_GRS, 100 GB):
  - Storage: ~$5/month
  - Transactions: ~$2/month
  Total: ~$7/month
```

### Azure Document Intelligence
```yaml
S0 Standard Tier:
  - First 500 pages: Included
  - Additional: $1.50 per 1,000 pages
  
Estimated (1,000 documents/month):
  - ~$2-5/month (depends on pages processed)
```

### Application Insights & Log Analytics
```yaml
Log Analytics (5 GB ingestion/month):
  - First 5 GB/month: Included
  - Additional: $2.30/GB
  
Estimated: $10-20/month
```

### Key Vault
```yaml
Secrets Operations:
  - 10,000 operations: $0.03
  
Estimated: $5/month
```

### Networking (if using VNet/Private Endpoints)
```yaml
Private Endpoints (3 endpoints):
  - $0.01/hour per endpoint = $22/month total
  
Data Processing:
  - $0.01/GB ingress (varies)
```

### Total Estimated Cost
```yaml
Development: $50-100/month (Consumption plan, lower usage)
Staging: $250-350/month (EP1 plans)
Production: $550-650/month (EP2/EP3 plans, VNet, geo-redundancy)

Note: Costs scale with:
  - Number of function executions
  - Document processing volume
  - Storage growth
  - Data transfer
  - External API call costs (if applicable)
```

---

## Terraform Module Structure (Recommended)

```hcl
/infrastructure/terraform/
  /modules/
    /resource-group/
    /app-service-plan/
    /function-app/
    /document-intelligence/
    /storage-account/
    /key-vault/
    /managed-identity/
    /application-insights/
    /log-analytics/
    /vnet/ (optional)
  
  /environments/
    /dev/
      main.tf
      variables.tf
      terraform.tfvars
    /staging/
      main.tf
      variables.tf
      terraform.tfvars
    /prod/
      main.tf
      variables.tf
      terraform.tfvars
  
  main.tf (root module)
  variables.tf
  outputs.tf
  providers.tf
```

---

## Critical Success Factors

### Pre-Deployment Requirements
1. **Azure AD App Registration** - Must be created and admin consent granted
2. **SharePoint Permissions** - Service principal must have site access
3. **Certificate Upload** - Certificate uploaded to Azure AD and stored in Key Vault
4. **Custom Model Training** - "serialnumber" model trained and deployed
5. **External API Access** - Confirm connectivity to apim-dev.nfm.com

### Post-Deployment Validation
1. **Document Intelligence API** - Test with sample image, verify extraction
2. **PieceInfo API** - Test aggregation of external APIs
3. **VendorReturnsService** - End-to-end test with SharePoint
4. **Monitoring** - Verify telemetry flowing to Application Insights
5. **SharePoint Integration** - Confirm read/write access to list and drive

### Monitoring Health Checks
1. **Function App Availability** - All 3 apps responding to health endpoints
2. **SharePoint Authentication** - No authentication errors in logs
3. **External API Connectivity** - Successful responses from apim-dev.nfm.com
4. **Custom Model Availability** - Document Intelligence model endpoint accessible
5. **Blob Storage Access** - No throttling or access denied errors

---

## Additional Considerations

### Disaster Recovery
```yaml
Backup Strategy:
  - Key Vault: Enable soft delete and purge protection
  - Storage Account: GRS replication in production
  - Document Intelligence: Custom model backed up (training data in blob storage)
  - SharePoint: Microsoft-managed backup
  - Function App Code: Source control (Git repository)
  
Recovery Time Objective (RTO): < 4 hours
Recovery Point Objective (RPO): < 1 hour

DR Plan:
  1. Redeploy infrastructure via Terraform in secondary region
  2. Restore secrets from Key Vault backup
  3. Retrain/redeploy Document Intelligence custom model
  4. Deploy function app code from repository
  5. Update SharePoint app registration with new endpoints (if needed)
```

### Scaling Considerations
```yaml
Auto-Scaling:
  - EP1/EP2 plans: Scale out based on metrics (CPU, memory, queue depth)
  - Consumption plans: Auto-scale by default
  
Scale Triggers:
  - Processing__MaxParallelItems: Adjust based on throughput requirements
  - Timer trigger frequency: Increase/decrease based on volume
  
Bottlenecks to Monitor:
  - SharePoint API throttling (429 responses)
  - Document Intelligence rate limits
  - External API rate limits (apim-dev.nfm.com)
  - Storage account IOPS limits
```

### Security Hardening
```yaml
Production Security:
  - Disable public access to Storage Account (use private endpoints)
  - Enable Azure DDoS Protection (if high-value)
  - Configure Azure Front Door or Application Gateway (if public-facing)
  - Enable Azure Defender for Cloud (security recommendations)
  - Implement network security groups (NSGs)
  - Configure Azure Firewall for egress filtering
  - Use Azure Policy for compliance enforcement
  - Enable Microsoft Defender for Key Vault
  - Implement just-in-time (JIT) VM access for management (if applicable)
```

---

## Contact & Support

### Azure Resource Naming Reference
- [Azure Naming Conventions](https://docs.microsoft.com/en-us/azure/cloud-adoption-framework/ready/azure-best-practices/naming-and-tagging)

### Terraform Azure Provider Documentation
- [Azure Provider](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs)

### Additional Documentation
- VendorReturnsService README: `srccsharp/VendorReturnsService/README.md`
- Document Intelligence API README: `src/document_intelligence/README.md` (to be created)
- PieceInfo API README: `src/pieceinfo_api/README.md`

---

**End of Document**

This specification provides comprehensive infrastructure requirements for deploying the Vendor Returns Processing system to Azure using Terraform. All resource names, configurations, and dependencies are documented for infrastructure-as-code implementation.

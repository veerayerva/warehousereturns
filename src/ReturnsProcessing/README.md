# SharePoint Returns Processing Azure Function

This C# Azure Functions project processes SharePoint vendor return entries by extracting serial images from attachments and sending them through Document Intelligence OCR and PieceInfo API lookup for complete returns processing.

## Current Implementation Status ✅

**COMPLETED** - Ready for testing and deployment

### What's Implemented:
- ✅ **Managed Identity Authentication** - Secure SharePoint access using Azure DefaultAzureCredential
- ✅ **SharePoint Service** - Complete Microsoft Graph integration for list item operations
- ✅ **Serial Image Extraction** - Downloads SerialImage attachments from SharePoint items
- ✅ **Document Intelligence Integration** - Sends images for OCR processing to extract serial numbers
- ✅ **PieceInfo API Integration** - Looks up SKU and Family information using extracted serials
- ✅ **SharePoint Updates** - Writes processing results back to original SharePoint items
- ✅ **Swagger Documentation** - Built-in API documentation at `/swagger/ui`
- ✅ **Error Handling & Logging** - Comprehensive error handling with correlation IDs
- ✅ **Configuration Management** - Strongly-typed configuration for all services

## Project Structure

```
ReturnsProcessing/
├── Models/
│   ├── SharePointConfiguration.cs    # SharePoint connection settings
│   └── SharePointModels.cs          # SharePoint list item and attachment models
├── Services/
│   └── SharePointService.cs         # Microsoft Graph service with managed identity
├── SharePointFunction.cs            # Main Azure Function with complete processing workflow
├── Program.cs                       # Dependency injection and service registration
├── host.json                       # Azure Functions runtime configuration
├── local.settings.json             # Development configuration (configured for NFM365)
├── DEPLOYMENT.md                   # Azure deployment guide for managed identity
└── ReturnsProcessing.csproj        # Project dependencies (Graph SDK, Azure Identity)
```

## SharePoint Integration Details

### Target SharePoint Site:
- **Site URL**: `https://nfm365.sharepoint.com/teams/VendorReturnProcess-024670`
- **List Name**: `VendorReturnEntries`
- **Authentication**: Managed Identity (no client secrets required)

### SharePoint List Fields Processed:
- `ID` - SharePoint list item identifier
- `SerialImage` - Image attachment containing serial number for OCR
- `PieceImage` - Product image attachment
- `SerialNumber` - Text field (updated by function)
- `SkuNumber` - Text field (updated by function)  
- `Family` - Text field (updated by function)
- `Status` - Processing status (updated by function)
- `RackLocation`, `PieceNumber`, `Comments`, `Vendor`, etc.

## API Endpoints

### 1. Process SharePoint Item
- **Endpoint**: `POST /api/process-sharepoint-item`
- **Purpose**: Complete end-to-end processing of SharePoint vendor return entry
- **Authentication**: Function-level (requires function key)
- **Request Body**: 
  ```json
  {
    "listItemId": "21",
    "correlationId": "optional-guid-for-tracking"
  }
  ```
- **Response Success (200)**:
  ```json
  {
    "listItemId": "21",
    "status": "Processing Completed",
    "serial": "ABC123456",
    "confidenceScore": 0.95,
    "sku": "SKU-001",
    "family": "ProductFamily-A", 
    "processedDateTime": "2025-11-19T15:30:00Z",
    "correlationId": "guid"
  }
  ```
- **Response Error (400/404/500)**:
  ```json
  {
    "error": "No serial image attachment found for processing",
    "details": "Additional error information",
    "correlationId": "guid"
  }
  ```

### 2. Health Check
- **Endpoint**: `GET /api/health`
- **Purpose**: Service health and dependency status check
- **Authentication**: Anonymous (no key required)
- **Response**:
  ```json
  {
    "status": "Healthy",
    "service": "SharePoint Returns Processing",
    "timestamp": "2025-11-19T15:30:00Z",
    "version": "1.0.0"
  }
  ```

### 3. Swagger Documentation
- **Endpoint**: `GET /api/swagger/ui`
- **Purpose**: Interactive API documentation and testing interface
- **Authentication**: Anonymous
- **Features**: Complete OpenAPI spec with request/response examples

### 4. OpenAPI Specification
- **Endpoint**: `GET /api/swagger.json`
- **Purpose**: Machine-readable API specification
- **Format**: OpenAPI 3.0.1 JSON

## Configuration (Ready for Production)

### Current Settings (local.settings.json) - Configured for NFM365
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "FUNCTIONS_EXTENSION_VERSION": "~4",
    
    // SharePoint Configuration - Managed Identity Authentication
    "SharePoint__SiteUrl": "https://nfm365.sharepoint.com/teams/VendorReturnProcess-024670",
    "SharePoint__ListName": "VendorReturnEntries", 
    "SharePoint__TenantDomain": "nfm365.sharepoint.com",
    "SharePoint__SitePath": "/teams/VendorReturnProcess-024670",
    
    // API Endpoints (Update ports as needed)
    "DocumentIntelligenceApi__BaseUrl": "http://localhost:7075",
    "DocumentIntelligenceApi__Timeout": "00:05:00",
    "DocumentIntelligenceApi__RetryCount": "3",
    "DocumentIntelligenceApi__RetryDelay": "00:00:02",
    
    "PieceInfoApi__BaseUrl": "http://localhost:7074", 
    "PieceInfoApi__Timeout": "00:02:00",
    "PieceInfoApi__RetryCount": "3",
    "PieceInfoApi__RetryDelay": "00:00:01",
    
    // Processing Configuration
    "Processing__ConfidenceThreshold": "0.3",
    "Processing__MaxProcessingTime": "00:10:00",
    "Processing__EnableRetries": "true",
    "Processing__BatchSize": "10"
  }
}
```

### Managed Identity Benefits:
- ❌ **No Client Secrets** - No credentials to manage or rotate
- ✅ **Automatic Authentication** - Azure handles token management
- ✅ **Secure by Default** - No secrets in configuration files
- ✅ **Easy Deployment** - Just assign permissions to managed identity

### Production Deployment Requirements:
1. **Enable Managed Identity** on Function App
2. **Grant Microsoft Graph Permissions**: `Sites.Read.All`, `Sites.ReadWrite.All`  
3. **Update API Base URLs** to production endpoints
4. **Configure Application Insights** for monitoring

See `DEPLOYMENT.md` for complete Azure setup instructions.

## Development Setup & Testing

### Prerequisites ✅
- .NET 8.0 SDK
- Azure Functions Core Tools v4
- Visual Studio Code or Visual Studio 2022  
- Azure Storage Emulator (for local development)
- Azure CLI (for managed identity authentication during development)

### Quick Start (Ready to Run)

1. **Build the Project**
   ```bash
   cd src/ReturnsProcessing
   dotnet restore
   dotnet build  # ✅ Builds successfully
   ```

2. **Start Dependencies**
   ```bash
   # Start Document Intelligence API (port 7075)
   cd ../DocumentIntelligence
   func start --port 7075
   
   # Start PieceInfo API (port 7074) 
   cd ../PieceInfoApi
   func start --port 7074
   ```

3. **Run the SharePoint Processing Function**
   ```bash
   cd ../ReturnsProcessing
   func start --port 7076  # Default port for this service
   ```

4. **Test the Implementation**
   ```bash
   # Health check (should work immediately)
   curl http://localhost:7076/api/health
   
   # View Swagger documentation
   # Open browser: http://localhost:7076/api/swagger/ui
   
   # Process a SharePoint item (requires actual SharePoint item ID)
   curl -X POST http://localhost:7076/api/process-sharepoint-item \
     -H "Content-Type: application/json" \
     -d '{"listItemId": "21", "correlationId": "test-123"}'
   ```

### Authentication for Local Development

The service uses `DefaultAzureCredential` which will try these methods in order:
1. **Azure CLI** - Run `az login` to authenticate
2. **Visual Studio** - Sign in to your Azure account
3. **Visual Studio Code** - Use Azure Account extension
4. **Managed Identity** - Automatic when deployed to Azure

### Configuration Status:
- ✅ **SharePoint Site Configured** - Points to NFM365 VendorReturnProcess site
- ✅ **API Endpoints Configured** - Document Intelligence & PieceInfo APIs
- ✅ **Managed Identity Ready** - No client secrets required
- ✅ **Swagger Documentation** - Built-in API docs available

## Processing Workflow (Fully Implemented)

The complete end-to-end processing workflow:

1. **📥 Receive Request** 
   - Azure Function receives SharePoint list item ID
   - Validates request and generates correlation ID for tracking

2. **📋 Fetch SharePoint Item**
   - Uses Microsoft Graph API with managed identity authentication
   - Retrieves vendor return entry from `VendorReturnEntries` list
   - Extracts metadata including SerialImage attachment information

3. **🖼️ Extract Serial Image**
   - Downloads SerialImage attachment from SharePoint
   - Supports multiple image formats (PNG, JPG, etc.)
   - Handles attachment parsing from SharePoint's complex JSON structure

4. **🔍 Document Intelligence OCR**
   - Sends image to Document Intelligence API (`localhost:7075/api/analyze-document`)
   - Extracts serial number with confidence score
   - Returns structured OCR results

5. **🔎 PieceInfo Lookup**
   - Queries PieceInfo API (`localhost:7074/api/piece-info/{serial}`)
   - Retrieves SKU, Family, and product information
   - Enriches processing results with product data

6. **💾 Update SharePoint**
   - Writes results back to original SharePoint list item
   - Updates: `SerialNumber`, `SkuNumber`, `Family`, `Status` fields
   - Maintains audit trail with processing timestamps

7. **📤 Return Results**
   - Provides comprehensive processing results to caller
   - Includes correlation ID for end-to-end tracking
   - Returns confidence scores and extracted data

### Error Handling & Resilience:
- **Comprehensive Logging** - All operations logged with correlation IDs
- **Graceful Degradation** - Continues processing even if some steps fail  
- **Detailed Error Messages** - Clear error responses with troubleshooting information
- **Status Updates** - SharePoint items updated with processing status regardless of outcome

## Technical Dependencies ✅

### NuGet Packages (Installed & Configured)
- **Microsoft.Azure.Functions.Worker** (1.19.0) - Azure Functions isolated worker runtime
- **Microsoft.Graph** (5.42.0) - Microsoft Graph SDK for SharePoint operations  
- **Azure.Identity** (1.12.0) - Managed identity and DefaultAzureCredential
- **System.Text.Json** (8.0.4) - High-performance JSON serialization
- **Microsoft.Extensions.Http** (8.0.0) - HTTP client factory and configuration

### External API Dependencies
- **Document Intelligence API**: `http://localhost:7075/api/analyze-document` 
  - Purpose: OCR processing of serial images
  - Input: Multipart form data with image file
  - Output: Extracted serial number with confidence score

- **PieceInfo API**: `http://localhost:7074/api/piece-info/{serial}`
  - Purpose: Product information lookup by serial number
  - Input: Serial number as URL parameter  
  - Output: SKU, Family, and product details

### SharePoint Requirements
- **Target Site**: `https://nfm365.sharepoint.com/teams/VendorReturnProcess-024670`
- **List**: `VendorReturnEntries` 
- **Required Permissions**: `Sites.Read.All`, `Sites.ReadWrite.All`
- **Authentication**: Managed Identity (no secrets required)

## Security & Authentication ✅

- ✅ **Managed Identity Authentication** - No client secrets or certificates
- ✅ **Function-level Authorization** - Processing endpoints require function key
- ✅ **Anonymous Health Checks** - Public health and documentation endpoints
- ✅ **Input Validation** - Request validation with detailed error messages
- ✅ **Correlation IDs** - End-to-end request tracing for security auditing

## Monitoring & Observability ✅

- ✅ **Structured Logging** - ILogger with correlation ID tracking
- ✅ **Comprehensive Error Handling** - Detailed error messages with stack traces
- ✅ **Health Check Endpoint** - Service and dependency status monitoring
- ✅ **Performance Tracking** - Processing timestamps and duration logging
- 🔄 **Application Insights Ready** - Configuration placeholders for production monitoring

## Next Steps for Tomorrow 🚀

### Ready for Immediate Work:
1. **Test with Real SharePoint Data** - Use actual SharePoint item IDs from NFM365 site
2. **Verify Image Attachment Extraction** - Test SerialImage download and processing
3. **End-to-End Integration Testing** - Validate complete workflow with Document Intelligence
4. **Production Deployment** - Deploy to Azure with managed identity configuration

### Implementation Status:
- ✅ **SharePoint Integration** - Complete with managed identity
- ✅ **Image Extraction** - SerialImage attachment processing
- ✅ **API Integration** - Document Intelligence and PieceInfo calls
- ✅ **Error Handling** - Comprehensive error management
- ✅ **Documentation** - Swagger UI and deployment guides
- ✅ **Configuration** - Production-ready settings for NFM365

### Files to Review Tomorrow:
- `SharePointFunction.cs` - Main processing logic
- `Services/SharePointService.cs` - SharePoint operations  
- `DEPLOYMENT.md` - Azure deployment instructions
- `local.settings.json` - Configuration settings

## Support

For issues and questions:
- Check the health endpoint for service status
- Review logs for error details
- Verify API endpoint connectivity
- Validate SharePoint permissions and configuration
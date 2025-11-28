# VendorReturnsService

Production-ready Azure Functions service for processing vendor return items from SharePoint.

## Overview

This service processes vendor return items through the following workflow:

1. **Image Retrieval**: Retrieves piece and serial images from SharePoint (attachments or drive)
2. **Document Intelligence**: Extracts PieceNumber and SerialNumber using Azure Document Intelligence
3. **PieceInfo API**: Enriches data with vendor, location, and SKU information
4. **SharePoint Update**: Updates list item with extracted data and changes status from R1 to R2

## Architecture

### Functions

- **ProcessReturnItem** (HTTP POST): Process a single item on-demand
- **ProcessPendingReturns** (Timer): Batch process pending R1 items in parallel

### Services

- **SharePointService**: SharePoint operations via Microsoft Graph and REST API
- **DocumentIntelligenceService**: Document analysis using Azure AI Form Recognizer
- **PieceInfoService**: External API integration for piece information
- **VendorReturnProcessor**: Orchestrates the complete processing workflow

### Models

- **VendorReturnItem**: SharePoint list item representation
- **SharePointUpdateModel**: Update payload for SharePoint
- **DocumentIntelligenceResult**: Document analysis results
- **PieceInfoResponse**: PieceInfo API response
- **ProcessingResult**: Processing outcome with details

## Configuration

All configuration is managed through `local.settings.json` (local) or Application Settings (Azure).

### Required Settings

```json
{
  "TenantId": "your-azure-ad-tenant-id",
  "ClientId": "your-app-registration-client-id",
  "ClientSecret": "your-client-secret",
  "SiteUrl": "https://yourtenant.sharepoint.com/sites/yoursite",
  "ListId": "your-sharepoint-list-guid",
  
  "PieceImageEndpoint": "https://your-di-instance.cognitiveservices.azure.com/",
  "PieceImageKey": "your-key",
  "PieceImageModelId": "your-model-id",
  
  "SerialImageEndpoint": "https://your-di-instance.cognitiveservices.azure.com/",
  "SerialImageKey": "your-key",
  "SerialImageModelId": "your-model-id",
  
  "PieceInfoApiBaseUrl": "https://your-api.azurewebsites.net",
  "PieceInfoApiKey": "your-api-key"
}
```

### Authentication Options

**Client Secret (Default)**:
```json
{
  "AuthenticationMode": "ClientSecret",
  "ClientSecret": "your-secret"
}
```

**Certificate**:
```json
{
  "AuthenticationMode": "Certificate",
  "CertificateThumbprint": "your-thumbprint",
  // OR
  "CertificatePath": "path/to/cert.pfx",
  "CertificatePassword": "cert-password"
}
```

### Image Source Options

**Attachment (Default)**:
```json
{
  "ImageSource": "Attachment"
}
```
Retrieves images from `PieceImage` and `SerialImage` attachment fields.

**SharePoint Drive**:
```json
{
  "ImageSource": "SharePointDrive"
}
```
Downloads images from `PieceImageUrl` and `SerialImageUrl` fields.

### Timer Configuration

```json
{
  "ProcessingSchedule": "0 */15 * * * *",  // Every 15 minutes
  "MaxParallelProcessing": "3"              // Process 3 items concurrently
}
```

## Workflow Details

### Processing Logic

1. **Retrieve Item**: Fetch SharePoint list item by ID
2. **Get Piece Image** (CRITICAL): 
   - Failure stops processing
   - Sets ProcessStatus = "Failed"
3. **Analyze Piece Image** (CRITICAL):
   - Extract PieceNumber using Document Intelligence
   - Failure stops processing
4. **Get Serial Image** (Optional):
   - Failure logged but processing continues
5. **Analyze Serial Image** (Optional):
   - Extract SerialNumber
   - Failure logged but processing continues
6. **Call PieceInfo API**:
   - Retrieve vendor/location/SKU data
   - Failure logged but processing continues
7. **Update SharePoint**:
   - Update all extracted fields
   - Status R1 → R2 only if PieceInfo succeeded
   - ProcessStatus = "Completed"

### Status Management

- **Status Field**: 
  - `R1`: Pending processing
  - `R2`: Successfully processed

- **ProcessStatus Field**:
  - `null` or empty: Not yet attempted
  - `Completed`: Successfully processed
  - `Failed`: Processing failed (will not retry)

### Error Handling

- PieceImage retrieval/analysis failures are **CRITICAL** - processing stops, ProcessStatus = "Failed"
- SerialImage failures are **logged** but processing continues
- PieceInfo API failures are **logged** but processing continues (Status remains R1)
- All failures include detailed error messages and correlation IDs for troubleshooting

## API Endpoints

### POST /api/process-return-item

Process a single vendor return item.

**Request**:
```json
{
  "listItemId": "123",
  "correlationId": "optional-correlation-id"
}
```

**Response** (Success):
```json
{
  "success": true,
  "listItemId": "123",
  "pieceNumber": "PC12345",
  "serialNumber": "SN67890",
  "processStatus": "Completed",
  "processingDetails": {
    "PieceNumber": "PC12345",
    "SerialNumber": "SN67890",
    "SkuNumber": "SKU-001",
    "Vendor": "ACME Corp",
    "StatusUpdated": "R2"
  },
  "correlationId": "abc-123-def"
}
```

**Response** (Failure):
```json
{
  "success": false,
  "listItemId": "123",
  "processStatus": "Failed",
  "errorMessage": "Failed to retrieve piece image",
  "processingDetails": {
    "Error": "Piece image not found or failed to download"
  },
  "correlationId": "abc-123-def"
}
```

## Deployment

### Prerequisites

1. Azure Functions App (Windows, .NET 8 Isolated)
2. Azure AD App Registration with SharePoint permissions
3. Two Document Intelligence instances (or one with two models)
4. PieceInfo API endpoint

### Azure AD Permissions

Your App Registration needs:
- **Microsoft Graph**: `Sites.ReadWrite.All`
- **SharePoint**: `Sites.FullControl.All` (for REST API)

### Deploy Steps

1. **Build**:
   ```powershell
   dotnet build --configuration Release
   ```

2. **Publish**:
   ```powershell
   func azure functionapp publish <function-app-name>
   ```

3. **Configure Application Settings** in Azure Portal

## Local Development

1. **Install Azure Functions Core Tools**:
   ```powershell
   npm install -g azure-functions-core-tools@4
   ```

2. **Update local.settings.json** with your configuration

3. **Run locally**:
   ```powershell
   func start
   ```

4. **Test HTTP Function**:
   ```powershell
   Invoke-RestMethod -Uri "http://localhost:7071/api/process-return-item" `
     -Method POST `
     -Body '{"listItemId":"123"}' `
     -ContentType "application/json"
   ```

## Monitoring

### Application Insights

All operations include correlation IDs for end-to-end tracking:
- HTTP requests
- SharePoint operations
- Document Intelligence calls
- PieceInfo API calls
- Processing results

### Log Levels

- **Information**: Successful operations, processing milestones
- **Warning**: Non-critical failures (SerialImage, PieceInfo API)
- **Error**: Critical failures, exceptions

### Correlation ID Format

- HTTP trigger: Single GUID
- Timer trigger: `{BatchGuid}-{ItemId}` for per-item tracking

## Troubleshooting

### Common Issues

1. **Authentication Failures**:
   - Verify TenantId, ClientId, ClientSecret
   - Check App Registration permissions
   - Ensure certificate is valid (if using certificate auth)

2. **Document Intelligence Errors**:
   - Verify endpoint and key
   - Check model ID
   - Ensure image is valid format

3. **SharePoint Update Failures**:
   - Check field names in configuration
   - Verify permissions
   - Ensure list item exists

4. **PieceInfo API Errors**:
   - Verify base URL and API key
   - Check network connectivity
   - Review API response format

### Debug Logs

Filter Application Insights by `correlationId` to trace a specific item through the entire workflow.

## Project Structure

```
VendorReturnsService/
├── Configuration/
│   └── AppSettings.cs
├── Models/
│   ├── SharePointModels.cs
│   ├── DocumentIntelligenceModels.cs
│   ├── PieceInfoModels.cs
│   └── ProcessingResult.cs
├── Services/
│   ├── ISharePointService.cs
│   ├── SharePointService.cs
│   ├── IDocumentIntelligenceService.cs
│   ├── DocumentIntelligenceService.cs
│   ├── IPieceInfoService.cs
│   ├── PieceInfoService.cs
│   ├── IVendorReturnProcessor.cs
│   └── VendorReturnProcessor.cs
├── Functions/
│   ├── ProcessReturnItemFunction.cs
│   └── ProcessPendingReturnsFunction.cs
├── Utilities/
│   └── ImageSourceHandler.cs
├── Program.cs
├── host.json
└── local.settings.json
```

## License

Internal use only.

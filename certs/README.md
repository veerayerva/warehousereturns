# Certificate-Based SharePoint Authentication

This directory contains certificates for SharePoint API authentication using certificate-based authentication with MSAL (Microsoft Authentication Library).

## Generated Certificate

- **Certificate Name**: WarehouseReturnsSharePointApp
- **Thumbprint**: `661E740832ADA15A4641430AF41778D8ADF97109`
- **Valid Until**: November 24, 2027
- **PFX Path**: `C:\DEV\Samples\WarehouseReturns\certs\WarehouseReturnsSharePointApp.pfx`
- **CER Path**: `C:\DEV\Samples\WarehouseReturns\certs\WarehouseReturnsSharePointApp.cer`
- **Password**: `DevCert2024!`

## Setup Instructions

### 1. Upload Certificate to Azure AD

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to **Azure Active Directory** > **App registrations**
3. Select your application (Client ID: `ddd6b480-7bc0-49d5-9192-54ccd56b6134`)
4. Go to **Certificates & secrets**
5. Click **Upload certificate**
6. Upload the file: `C:\DEV\Samples\WarehouseReturns\certs\WarehouseReturnsSharePointApp.cer`

### 2. Grant SharePoint Permissions

Ensure your app has the following API permissions:

- **SharePoint**:
  - `Sites.Read.All` or `Sites.ReadWrite.All` (Application permission)
  
To grant permissions:
1. In your App Registration, go to **API permissions**
2. Click **Add a permission**
3. Select **SharePoint**
4. Choose **Application permissions**
5. Select required permissions
6. Click **Grant admin consent**

### 3. Configuration

The certificate configuration is already added to `local.settings.json`:

```json
"SHAREPOINT_CERTIFICATE_PATH": "C:\\DEV\\Samples\\WarehouseReturns\\certs\\WarehouseReturnsSharePointApp.pfx",
"SHAREPOINT_CERTIFICATE_PASSWORD": "DevCert2024!",
"SHAREPOINT_CERTIFICATE_THUMBPRINT": "661E740832ADA15A4641430AF41778D8ADF97109"
```

### 4. Testing the Certificate Authentication

#### Start the Function App

```powershell
cd C:\DEV\Samples\WarehouseReturns\srccsharp\ReturnsProcessing
func start
```

#### Test the Endpoint

Once the function app is running, test the certificate-based authentication:

```powershell
# Replace with your actual list item ID and filename
$listItemId = "2"
$fileName = "your-attachment-filename.png"
$url = "http://localhost:7071/api/test/cert-auth?listItemId=$listItemId&fileName=$fileName"

Invoke-RestMethod -Uri $url -Method Get
```

**Expected Response (Success)**:
```json
{
  "success": true,
  "message": "Certificate-based authentication successful",
  "listItemId": "2",
  "fileName": "your-attachment-filename.png",
  "fileSizeBytes": 274464,
  "correlationId": "...",
  "timestamp": "2025-11-24T..."
}
```

## Code Implementation

### New Method: `GetAttachmentWithCertificateAsync`

This method uses certificate-based authentication instead of client secret:

**Location**: `Services/SharePointService.cs`

**Key Features**:
- Uses `ConfidentialClientApplicationBuilder` from MSAL
- Loads certificate from PFX file or certificate store by thumbprint
- Acquires token with scope: `https://{tenantHost}/.default`
- Sends `x5c` header in client assertion for Azure AD validation
- Calls SharePoint REST API: `/_api/web/lists(guid'{listId}')/items({itemId})/AttachmentFiles('{fileName}')/$value`

**Usage Example**:
```csharp
var imageData = await _sharePointService.GetAttachmentWithCertificateAsync(
    listItemId: "2",
    fileName: "attachment.png",
    correlationId: Guid.NewGuid().ToString()
);
```

## Comparison: Certificate vs Client Secret

### Current Method (`GetAttachmentAsync`)
- Uses `ClientSecretCredential` 
- Requires client secret in configuration
- Secret needs rotation every 1-2 years

### New Method (`GetAttachmentWithCertificateAsync`)
- Uses certificate-based authentication
- More secure (private key never transmitted)
- Certificate valid for 2 years (can be longer)
- Recommended for production environments

## Security Notes

⚠️ **IMPORTANT**:
- The .pfx file contains the private key - keep it secure
- Do NOT commit certificates to source control
- The password should be stored in Azure Key Vault in production
- This is a development certificate for testing purposes

## Troubleshooting

### Certificate Not Found
- Verify the certificate path is correct
- Check that the thumbprint matches
- Ensure the certificate is installed in the user store

### Authentication Failed
- Verify the certificate is uploaded to Azure AD
- Check API permissions are granted
- Ensure admin consent is provided
- Verify tenant ID and client ID are correct

### Permission Denied
- Ensure the app has SharePoint API permissions
- Grant admin consent for the permissions
- Verify the app is authorized for your SharePoint site

## Log Messages

Look for these log prefixes in the function app output:
- `[CERT-ATTACHMENT]` - Certificate authentication operations
- `[TEST-CERT]` - Test endpoint operations

Example successful log:
```
[CERT-ATTACHMENT] Certificate loaded successfully
[CERT-ATTACHMENT] Successfully obtained SharePoint access token
[CERT-ATTACHMENT] Successfully downloaded 274464 bytes using certificate auth
```

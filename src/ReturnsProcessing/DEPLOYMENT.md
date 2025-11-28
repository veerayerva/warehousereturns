# Azure Deployment Notes for Managed Identity

## Prerequisites for SharePoint Access

When deploying this Azure Function to Azure, you need to configure managed identity authentication to access SharePoint.

### 1. Enable Managed Identity on the Function App

**Option A: System-Assigned Managed Identity (Recommended)**
```bash
# Enable system-assigned managed identity
az functionapp identity assign --name <your-function-app> --resource-group <your-rg>
```

**Option B: User-Assigned Managed Identity**
```bash
# Create user-assigned managed identity
az identity create --name <identity-name> --resource-group <your-rg>

# Assign to function app
az functionapp identity assign --name <your-function-app> --resource-group <your-rg> --identities <identity-resource-id>
```

### 2. Grant SharePoint Permissions

The managed identity needs Microsoft Graph API permissions to access SharePoint:

**Required Permissions:**
- `Sites.Read.All` - Read SharePoint sites and lists
- `Sites.ReadWrite.All` - Update SharePoint list items

**Grant Permissions via Azure CLI:**
```bash
# Get the managed identity object ID
IDENTITY_ID=$(az functionapp identity show --name <your-function-app> --resource-group <your-rg> --query principalId -o tsv)

# Grant Sites.Read.All permission
az ad app permission grant --id $IDENTITY_ID --api 00000003-0000-0000-c000-000000000000 --scope Sites.Read.All

# Grant Sites.ReadWrite.All permission  
az ad app permission grant --id $IDENTITY_ID --api 00000003-0000-0000-c000-000000000000 --scope Sites.ReadWrite.All
```

**Grant Permissions via PowerShell (Alternative):**
```powershell
# Connect to Microsoft Graph
Connect-MgGraph -Scopes "Application.ReadWrite.All", "Directory.ReadWrite.All"

# Get the managed identity
$managedIdentity = Get-MgServicePrincipal -Filter "displayName eq '<your-function-app>'"

# Get Microsoft Graph service principal
$graphServicePrincipal = Get-MgServicePrincipal -Filter "appId eq '00000003-0000-0000-c000-000000000000'"

# Grant Sites.Read.All
$readPermission = $graphServicePrincipal.AppRoles | Where-Object {$_.Value -eq "Sites.Read.All"}
New-MgServicePrincipalAppRoleAssignment -ServicePrincipalId $managedIdentity.Id -PrincipalId $managedIdentity.Id -ResourceId $graphServicePrincipal.Id -AppRoleId $readPermission.Id

# Grant Sites.ReadWrite.All  
$writePermission = $graphServicePrincipal.AppRoles | Where-Object {$_.Value -eq "Sites.ReadWrite.All"}
New-MgServicePrincipalAppRoleAssignment -ServicePrincipalId $managedIdentity.Id -PrincipalId $managedIdentity.Id -ResourceId $graphServicePrincipal.Id -AppRoleId $writePermission.Id
```

### 3. Update App Settings

Ensure your Function App has the correct configuration:

```bash
# Set SharePoint configuration
az functionapp config appsettings set --name <your-function-app> --resource-group <your-rg> --settings \
  "SharePoint__SiteUrl=https://nfm365.sharepoint.com/teams/VendorReturnProcess-024670" \
  "SharePoint__ListName=VendorReturnEntries" \
  "SharePoint__TenantDomain=nfm365.sharepoint.com" \
  "SharePoint__SitePath=/teams/VendorReturnProcess-024670"
```

### 4. Verify Permissions

After deployment, test the SharePoint connection by calling the health endpoint:

```bash
curl https://<your-function-app>.azurewebsites.net/api/health
```

### 5. Local Development

For local development, you can authenticate using:
- Azure CLI: `az login`
- Visual Studio: Sign in to your Azure account
- Visual Studio Code: Azure Account extension

The `DefaultAzureCredential` will automatically use the appropriate authentication method.

## Troubleshooting

### Common Issues

1. **"Insufficient privileges to complete the operation"**
   - Verify the managed identity has the required Graph API permissions
   - Check if admin consent has been granted

2. **"Site not found"**
   - Verify the SharePoint site URL is correct
   - Ensure the managed identity has access to the specific SharePoint site

3. **"Authentication failed"**
   - Verify managed identity is properly enabled on the Function App
   - Check that the correct permissions are assigned

### Logs and Monitoring

Enable Application Insights for detailed logging:

```bash
az functionapp config appsettings set --name <your-function-app> --resource-group <your-rg> --settings \
  "APPINSIGHTS_INSTRUMENTATIONKEY=<your-app-insights-key>"
```

The SharePointService includes comprehensive logging for troubleshooting authentication and API calls.
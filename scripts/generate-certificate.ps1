# Generate Self-Signed Certificate for SharePoint API Testing
# This script creates a certificate for app-only authentication with SharePoint

$certName = "WarehouseReturnsSharePointApp"
$certPath = "C:\DEV\Samples\WarehouseReturns\certs"
$certPassword = "DevCert2024!"  # Change this to a secure password

# Create certs directory if it doesn't exist
if (-not (Test-Path $certPath)) {
    New-Item -ItemType Directory -Path $certPath -Force
    Write-Host "Created certificate directory: $certPath" -ForegroundColor Green
}

# Generate self-signed certificate
Write-Host "Generating self-signed certificate..." -ForegroundColor Cyan
$cert = New-SelfSignedCertificate `
    -Subject "CN=$certName" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -KeyExportPolicy Exportable `
    -KeySpec Signature `
    -KeyLength 2048 `
    -KeyAlgorithm RSA `
    -HashAlgorithm SHA256 `
    -NotAfter (Get-Date).AddYears(2) `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.2")

Write-Host "Certificate created in user store with thumbprint: $($cert.Thumbprint)" -ForegroundColor Green

# Export certificate with private key to PFX file
$pfxPath = Join-Path $certPath "$certName.pfx"
$securePassword = ConvertTo-SecureString -String $certPassword -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePassword
Write-Host "Certificate exported to: $pfxPath" -ForegroundColor Green

# Export public key to CER file (for uploading to Azure AD)
$cerPath = Join-Path $certPath "$certName.cer"
Export-Certificate -Cert $cert -FilePath $cerPath
Write-Host "Public certificate exported to: $cerPath" -ForegroundColor Green

Write-Host "`n====== Certificate Information ======" -ForegroundColor Yellow
Write-Host "Thumbprint: $($cert.Thumbprint)" -ForegroundColor White
Write-Host "Subject: $($cert.Subject)" -ForegroundColor White
Write-Host "Valid From: $($cert.NotBefore)" -ForegroundColor White
Write-Host "Valid Until: $($cert.NotAfter)" -ForegroundColor White
Write-Host "PFX Path: $pfxPath" -ForegroundColor White
Write-Host "CER Path: $cerPath" -ForegroundColor White
Write-Host "Password: $certPassword" -ForegroundColor White

Write-Host "`n====== Next Steps ======" -ForegroundColor Yellow
Write-Host "1. Upload the .cer file to your Azure AD App Registration:" -ForegroundColor Cyan
Write-Host "   - Go to Azure Portal > App Registrations > Your App" -ForegroundColor White
Write-Host "   - Navigate to 'Certificates & secrets'" -ForegroundColor White
Write-Host "   - Click 'Upload certificate' and select: $cerPath" -ForegroundColor White
Write-Host ""
Write-Host "2. Update your local.settings.json with these values:" -ForegroundColor Cyan
Write-Host "   CERTIFICATE_PATH: `"$pfxPath`"" -ForegroundColor White
Write-Host "   CERTIFICATE_PASSWORD: `"$certPassword`"" -ForegroundColor White
Write-Host "   CERTIFICATE_THUMBPRINT: `"$($cert.Thumbprint)`"" -ForegroundColor White
Write-Host ""
Write-Host "3. Ensure SharePoint API permissions are granted in Azure AD" -ForegroundColor Cyan

# Create a local.settings.json template snippet
$settingsSnippet = @"

Add these to your local.settings.json SharePoint section:

"SharePoint": {
  "TENANT_ID": "YOUR_TENANT_ID",
  "CLIENT_ID": "YOUR_APP_CLIENT_ID",
  "CERTIFICATE_PATH": "$pfxPath",
  "CERTIFICATE_PASSWORD": "$certPassword",
  "CERTIFICATE_THUMBPRINT": "$($cert.Thumbprint)",
  "SHAREPOINT_SITE_URL": "https://YOUR_TENANT.sharepoint.com/sites/YOUR_SITE",
  "SHAREPOINT_LIST_ID": "YOUR_LIST_GUID"
}
"@

$settingsPath = Join-Path $certPath "local-settings-snippet.txt"
$settingsSnippet | Out-File -FilePath $settingsPath -Encoding UTF8
Write-Host "`nConfiguration snippet saved to: $settingsPath" -ForegroundColor Green

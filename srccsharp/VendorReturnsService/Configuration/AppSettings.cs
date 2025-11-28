namespace VendorReturnsService.Configuration;

/// <summary>
/// SharePoint connection and authentication settings.
/// </summary>
public class SharePointSettings
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SiteUrl { get; set; } = string.Empty;
    public string ListId { get; set; } = string.Empty;
    
    // Certificate Authentication (Optional - alternative to ClientSecret)
    public string? CertificateThumbprint { get; set; }
    public string? CertificatePath { get; set; }
    public string? CertificatePassword { get; set; }
    
    // Authentication Mode: "Certificate" or "ClientSecret"
    public string AuthenticationMode { get; set; } = "ClientSecret";
    
    // Image Source: "Attachment" or "SharePointDrive"
    public string ImageSource { get; set; } = "Attachment";
}

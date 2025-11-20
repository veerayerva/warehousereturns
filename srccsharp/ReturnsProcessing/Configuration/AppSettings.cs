using System.ComponentModel.DataAnnotations;

namespace WarehouseReturns.ReturnsProcessing.Configuration;

/// <summary>
/// SharePoint and Microsoft Graph API configuration settings
/// </summary>
public class SharePointSettings
{
    /// <summary>
    /// Authentication method: "ManagedIdentity" or "ClientCredentials"
    /// </summary>
    public string AUTHENTICATION_METHOD { get; set; } = "ManagedIdentity";

    /// <summary>
    /// Azure AD Tenant ID (required for ClientCredentials authentication)
    /// </summary>
    public string TENANT_ID { get; set; } = string.Empty;

    /// <summary>
    /// Azure AD Application (Client) ID (required for ClientCredentials authentication)
    /// </summary>
    public string CLIENT_ID { get; set; } = string.Empty;

    /// <summary>
    /// Azure AD Application Client Secret (required for ClientCredentials authentication)
    /// </summary>
    public string CLIENT_SECRET { get; set; } = string.Empty;

    /// <summary>
    /// SharePoint Site URL
    /// </summary>
    [Required(ErrorMessage = "SharePoint site URL is required")]
    [Url(ErrorMessage = "SharePoint site URL must be a valid URL")]
    public string SHAREPOINT_SITE_URL { get; set; } = string.Empty;

    /// <summary>
    /// SharePoint List ID for returns processing
    /// </summary>
    [Required(ErrorMessage = "SharePoint list ID is required")]
    public string SHAREPOINT_LIST_ID { get; set; } = string.Empty;

    /// <summary>
    /// Validate authentication configuration
    /// </summary>
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(AUTHENTICATION_METHOD))
            return false;

        if (AUTHENTICATION_METHOD.Equals("ClientCredentials", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(TENANT_ID) &&
                   !string.IsNullOrWhiteSpace(CLIENT_ID) &&
                   !string.IsNullOrWhiteSpace(CLIENT_SECRET);
        }

        return true; // ManagedIdentity doesn't require additional config
    }

    /// <summary>
    /// Check if using managed identity
    /// </summary>
    public bool IsManagedIdentity => 
        AUTHENTICATION_METHOD.Equals("ManagedIdentity", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Check if using client credentials
    /// </summary>
    public bool IsClientCredentials => 
        AUTHENTICATION_METHOD.Equals("ClientCredentials", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Get the site ID from the SharePoint URL
    /// </summary>
    public string GetSiteId()
    {
        var uri = new Uri(SHAREPOINT_SITE_URL);
        return $"{uri.Host},{uri.LocalPath.Split('/').Skip(1).FirstOrDefault()},{uri.LocalPath.Split('/').Skip(2).FirstOrDefault()}";
    }
}

/// <summary>
/// Document Intelligence API configuration settings
/// </summary>
public class DocumentIntelligenceApiSettings
{
    /// <summary>
    /// Document Intelligence API endpoint
    /// </summary>
    [Required(ErrorMessage = "Document Intelligence endpoint is required")]
    [Url(ErrorMessage = "Document Intelligence endpoint must be a valid URL")]
    public string DOCUMENT_INTELLIGENCE_ENDPOINT { get; set; } = string.Empty;

    /// <summary>
    /// Document Intelligence API key (optional for anonymous endpoints)
    /// </summary>
    public string DOCUMENT_INTELLIGENCE_API_KEY { get; set; } = string.Empty;

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    [Range(30, 600, ErrorMessage = "Timeout must be between 30 and 600 seconds")]
    public int DOCUMENT_INTELLIGENCE_TIMEOUT_SECONDS { get; set; } = 120;

    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    [Range(0, 5, ErrorMessage = "Max retries must be between 0 and 5")]
    public int DOCUMENT_INTELLIGENCE_MAX_RETRIES { get; set; } = 3;
}

/// <summary>
/// Piece Info API configuration settings
/// </summary>
public class PieceInfoApiSettings
{
    /// <summary>
    /// Piece Info API endpoint
    /// </summary>
    [Required(ErrorMessage = "Piece Info API endpoint is required")]
    [Url(ErrorMessage = "Piece Info API endpoint must be a valid URL")]
    public string PIECE_INFO_API_ENDPOINT { get; set; } = string.Empty;

    /// <summary>
    /// Piece Info API key (optional for anonymous endpoints)
    /// </summary>
    public string PIECE_INFO_API_KEY { get; set; } = string.Empty;

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    [Range(15, 300, ErrorMessage = "Timeout must be between 15 and 300 seconds")]
    public int PIECE_INFO_API_TIMEOUT_SECONDS { get; set; } = 60;

    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    [Range(0, 5, ErrorMessage = "Max retries must be between 0 and 5")]
    public int PIECE_INFO_API_MAX_RETRIES { get; set; } = 3;
}

/// <summary>
/// Processing configuration settings
/// </summary>
public class ProcessingSettings
{
    /// <summary>
    /// Confidence threshold for document intelligence results
    /// </summary>
    [Range(0.0, 1.0, ErrorMessage = "Confidence threshold must be between 0.0 and 1.0")]
    public decimal CONFIDENCE_THRESHOLD { get; set; } = 0.3m;

    /// <summary>
    /// Maximum file size in MB
    /// </summary>
    [Range(1, 100, ErrorMessage = "Max file size must be between 1 and 100 MB")]
    public int MAX_FILE_SIZE_MB { get; set; } = 50;

    /// <summary>
    /// Supported image file extensions (comma-separated)
    /// </summary>
    public string SUPPORTED_IMAGE_TYPES { get; set; } = "jpg,jpeg,png,pdf,tiff,bmp";

    /// <summary>
    /// Batch processing size
    /// </summary>
    [Range(1, 50, ErrorMessage = "Batch size must be between 1 and 50")]
    public int BATCH_SIZE { get; set; } = 10;

    /// <summary>
    /// Processing timeout in minutes
    /// </summary>
    [Range(1, 30, ErrorMessage = "Processing timeout must be between 1 and 30 minutes")]
    public int PROCESSING_TIMEOUT_MINUTES { get; set; } = 5;

    /// <summary>
    /// Get supported image types as array
    /// </summary>
    public string[] GetSupportedImageTypes()
    {
        return SUPPORTED_IMAGE_TYPES.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim().ToLowerInvariant())
            .ToArray();
    }

    /// <summary>
    /// Check if file type is supported
    /// </summary>
    public bool IsFileTypeSupported(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return false;
        
        var extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        return GetSupportedImageTypes().Contains(extension);
    }
}

/// <summary>
/// Application settings container
/// </summary>
public class AppSettings
{
    public SharePointSettings SharePoint { get; set; } = new();
    public DocumentIntelligenceApiSettings DocumentIntelligenceApi { get; set; } = new();
    public PieceInfoApiSettings PieceInfoApi { get; set; } = new();
    public ProcessingSettings Processing { get; set; } = new();
}
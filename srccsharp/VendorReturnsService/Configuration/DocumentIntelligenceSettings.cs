namespace VendorReturnsService.Configuration;

/// <summary>
/// Document Intelligence API settings for calling the wrapper API.
/// </summary>
public class DocumentIntelligenceSettings
{
    public string ApiEndpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 120;
    public int MaxRetries { get; set; } = 3;
}

using System.ComponentModel.DataAnnotations;

namespace WarehouseReturns.ReturnsProcessing.Configuration
{
    /// <summary>
    /// SharePoint service configuration settings
    /// </summary>
    public class SharePointSettings
    {
        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public string ClientId { get; set; } = string.Empty;

        [Required]
        public string ClientSecret { get; set; } = string.Empty;

        [Required]
        [Url]
        public string SiteUrl { get; set; } = string.Empty;

        [Required]
        public string ListId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Document Intelligence API configuration settings
    /// </summary>
    public class DocumentIntelligenceApiSettings
    {
        [Required]
        [Url]
        public string BaseUrl { get; set; } = string.Empty;

        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

        [Range(1, 10)]
        public int RetryCount { get; set; } = 3;

        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);
    }

    /// <summary>
    /// Piece Info API configuration settings
    /// </summary>
    public class PieceInfoApiSettings
    {
        [Required]
        [Url]
        public string BaseUrl { get; set; } = string.Empty;

        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);

        [Range(1, 10)]
        public int RetryCount { get; set; } = 3;

        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
    }

    /// <summary>
    /// Processing configuration settings
    /// </summary>
    public class ProcessingSettings
    {
        [Range(0.0, 1.0)]
        public double ConfidenceThreshold { get; set; } = 0.3;

        public TimeSpan MaxProcessingTime { get; set; } = TimeSpan.FromMinutes(10);

        public bool EnableRetries { get; set; } = true;

        [Range(1, 100)]
        public int BatchSize { get; set; } = 10;
    }
}
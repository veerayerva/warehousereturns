using System.Text.Json.Serialization;

namespace WarehouseReturns.ReturnsProcessing.Models
{
    /// <summary>
    /// SharePoint list item model
    /// </summary>
    public class SharePointListItem
    {
        public string Id { get; set; } = string.Empty;
        public DateTime CreatedDateTime { get; set; }
        public DateTime LastModifiedDateTime { get; set; }
        public SharePointFields Fields { get; set; } = new();
    }

    /// <summary>
    /// SharePoint list item fields
    /// </summary>
    public class SharePointFields
    {
        public string Title { get; set; } = string.Empty;
        public string ReturnId { get; set; } = string.Empty;
        public string CustomerInfo { get; set; } = string.Empty;
        public string? Serial { get; set; }
        public decimal? ConfidenceScore { get; set; }
        public string? SKU { get; set; }
        public string? Family { get; set; }
        public string ProcessingStatus { get; set; } = "Pending";
        public string? ErrorMessage { get; set; }
        public DateTime? ProcessedDateTime { get; set; }
        public string? CorrelationId { get; set; }
        public SharePointAttachment? ProductImage { get; set; }
    }

    /// <summary>
    /// SharePoint attachment model
    /// </summary>
    public class SharePointAttachment
    {
        public string FileName { get; set; } = string.Empty;
        public string ServerRelativeUrl { get; set; } = string.Empty;
        public string ServerUrl { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string ContentType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Processing result model
    /// </summary>
    public class ProcessingResult
    {
        public string ListItemId { get; set; } = string.Empty;
        public string? Serial { get; set; }
        public decimal ConfidenceScore { get; set; }
        public string? SKU { get; set; }
        public string? Family { get; set; }
        public string Status { get; set; } = "Pending";
        public DateTime ProcessedDateTime { get; set; } = DateTime.UtcNow;
        public string CorrelationId { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public TimeSpan ProcessingDuration { get; set; }
    }
}
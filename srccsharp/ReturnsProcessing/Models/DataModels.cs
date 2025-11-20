using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WarehouseReturns.ReturnsProcessing.Models;

/// <summary>
/// SharePoint list information for discovery
/// </summary>
public class SharePointListInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("webUrl")]
    public string? WebUrl { get; set; }

    [JsonPropertyName("itemCount")]
    public int ItemCount { get; set; }
}

/// <summary>
/// SharePoint field (column) information with complete metadata
/// </summary>
public class SharePointFieldInfo
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("fieldType")]
    public string FieldType { get; set; } = "Unknown";

    [JsonPropertyName("readOnly")]
    public bool ReadOnly { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("hidden")]
    public bool Hidden { get; set; }

    [JsonPropertyName("indexed")]
    public bool Indexed { get; set; }

    [JsonPropertyName("canBeDeleted")]
    public bool CanBeDeleted { get; set; }

    [JsonPropertyName("sealed")]
    public bool Sealed { get; set; }
}

/// <summary>
/// Vendor return entry model for SharePoint integration demo
/// </summary>
public class VendorReturnEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("returnId")]
    public string ReturnId { get; set; } = string.Empty;

    [JsonPropertyName("customerInfo")]
    public string CustomerInfo { get; set; } = string.Empty;

    [JsonPropertyName("serial")]
    public string? Serial { get; set; }

    [JsonPropertyName("sku")]
    public string? SKU { get; set; }

    [JsonPropertyName("family")]
    public string? Family { get; set; }

    [JsonPropertyName("processingStatus")]
    public string ProcessingStatus { get; set; } = "Pending";

    [JsonPropertyName("confidenceScore")]
    public decimal? ConfidenceScore { get; set; }

    [JsonPropertyName("createdDateTime")]
    public DateTime CreatedDateTime { get; set; }

    [JsonPropertyName("lastModifiedDateTime")]
    public DateTime LastModifiedDateTime { get; set; }

    [JsonPropertyName("attachments")]
    public List<AttachmentInfo> Attachments { get; set; } = new();

    // Additional properties used by SharePoint service
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("rackLocation")]
    public string RackLocation { get; set; } = string.Empty;

    [JsonPropertyName("pieceNumber")]
    public string PieceNumber { get; set; } = string.Empty;

    [JsonPropertyName("serialNumber")]
    public string SerialNumber { get; set; } = string.Empty;

    [JsonPropertyName("comments")]
    public string Comments { get; set; } = string.Empty;

    [JsonPropertyName("vendor")]
    public string Vendor { get; set; } = string.Empty;

    [JsonPropertyName("skuNumber")]
    public string SkuNumber { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public string Created { get; set; } = string.Empty;
}

/// <summary>
/// QC Item model matching SharePoint VendorReturnEntries list structure
/// </summary>
public class QcItem
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("pieceImage")]
    public string PieceImage { get; set; } = string.Empty;

    [JsonPropertyName("serialImage")]
    public string? SerialImage { get; set; }

    [JsonPropertyName("pieceNumber")]
    public string? PieceNumber { get; set; }

    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; set; }

    [JsonPropertyName("comments")]
    public string? Comments { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("damageImage1Link")]
    public string? DamageImage1Link { get; set; }

    [JsonPropertyName("damageImage2Link")]
    public string? DamageImage2Link { get; set; }

    [JsonPropertyName("damageImage3Link")]
    public string? DamageImage3Link { get; set; }

    [JsonPropertyName("damageImage4Link")]
    public string? DamageImage4Link { get; set; }

    [JsonPropertyName("damageImage5Link")]
    public string? DamageImage5Link { get; set; }

    [JsonPropertyName("reasonCategory")]
    public string? ReasonCategory { get; set; }

    [JsonPropertyName("reasonCode")]
    public string? ReasonCode { get; set; }

    [JsonPropertyName("locationCode")]
    public string? LocationCode { get; set; }

    [JsonPropertyName("qcNumber")]
    public int? QCNumber { get; set; }

    [JsonPropertyName("qcFileName")]
    public string? QCFileName { get; set; }

    [JsonPropertyName("skuNumber")]
    public string? SkuNumber { get; set; }

    [JsonPropertyName("whseloc")]
    public string? WHSELOC { get; set; }

    [JsonPropertyName("vendor")]
    public string? Vendor { get; set; }

    [JsonPropertyName("family")]
    public string? Family { get; set; }

    [JsonPropertyName("orgPO")]
    public string? OrgPO { get; set; }

    [JsonPropertyName("modelNumber")]
    public string? ModelNumber { get; set; }

    [JsonPropertyName("rackLocation")]
    public string? RackLocation { get; set; }
}

/// <summary>
/// Attachment information model for SharePoint integration
/// </summary>
public class AttachmentInfo
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("serverRelativeUrl")]
    public string ServerRelativeUrl { get; set; } = string.Empty;

    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("downloadUrl")]
    public string? DownloadUrl { get; set; }
}

/// <summary>
/// SharePoint list item model for returns processing
/// </summary>
public class SharePointListItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public SharePointFields Fields { get; set; } = new();

    [JsonPropertyName("createdDateTime")]
    public DateTime CreatedDateTime { get; set; }

    [JsonPropertyName("lastModifiedDateTime")]
    public DateTime LastModifiedDateTime { get; set; }
}

/// <summary>
/// SharePoint list item fields (legacy - kept for compatibility)
/// </summary>
public class SharePointFields
{
    [JsonPropertyName("Title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("ReturnId")]
    public string ReturnId { get; set; } = string.Empty;

    [JsonPropertyName("CustomerInfo")]
    public string CustomerInfo { get; set; } = string.Empty;

    [JsonPropertyName("ProcessingStatus")]
    public string ProcessingStatus { get; set; } = "Pending";

    [JsonPropertyName("CorrelationId")]
    public string? CorrelationId { get; set; }
}

/// <summary>
/// SharePoint attachment information
/// </summary>
public class SharePointAttachment
{
    [JsonPropertyName("serverRelativeUrl")]
    public string ServerRelativeUrl { get; set; } = string.Empty;

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

/// <summary>
/// Document Intelligence API request model
/// </summary>
public class DocumentAnalysisRequest
{
    [JsonPropertyName("documentUrl")]
    public string? DocumentUrl { get; set; }

    [JsonPropertyName("modelId")]
    public string ModelId { get; set; } = "serialnumber";

    [JsonPropertyName("confidenceThreshold")]
    public decimal ConfidenceThreshold { get; set; } = 0.3m;

    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Document Intelligence API response model
/// </summary>
public class DocumentAnalysisResponse
{
    [JsonPropertyName("analysis_id")]
    public string AnalysisId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("serial_field")]
    public SerialField? SerialField { get; set; }

    [JsonPropertyName("document_metadata")]
    public DocumentMetadata? DocumentMetadata { get; set; }

    [JsonPropertyName("processing_metadata")]
    public ProcessingMetadata? ProcessingMetadata { get; set; }

    [JsonPropertyName("correlation_id")]
    public string? CorrelationId { get; set; }

    [JsonPropertyName("error_details")]
    public ErrorDetails? ErrorDetails { get; set; }
}

/// <summary>
/// Serial field extraction result
/// </summary>
public class SerialField
{
    [JsonPropertyName("field_name")]
    public string FieldName { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("confidence")]
    public decimal Confidence { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Document metadata
/// </summary>
public class DocumentMetadata
{
    [JsonPropertyName("source_type")]
    public string SourceType { get; set; } = string.Empty;

    [JsonPropertyName("document_url")]
    public string? DocumentUrl { get; set; }

    [JsonPropertyName("document_type")]
    public string DocumentType { get; set; } = string.Empty;

    [JsonPropertyName("model_id")]
    public string ModelId { get; set; } = string.Empty;
}

/// <summary>
/// Processing metadata
/// </summary>
public class ProcessingMetadata
{
    [JsonPropertyName("processing_time_ms")]
    public int ProcessingTimeMs { get; set; }

    [JsonPropertyName("azure_api_version")]
    public string AzureApiVersion { get; set; } = string.Empty;

    [JsonPropertyName("confidence_threshold")]
    public decimal ConfidenceThreshold { get; set; }

    [JsonPropertyName("model_used")]
    public string ModelUsed { get; set; } = string.Empty;
}

/// <summary>
/// Error details
/// </summary>
public class ErrorDetails
{
    [JsonPropertyName("error_code")]
    public string ErrorCode { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public string? Details { get; set; }
}

/// <summary>
/// Piece Info API response model
/// </summary>
public class PieceInfoResponse
{
    [JsonPropertyName("piece_number")]
    public string PieceNumber { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("sku")]
    public string? SKU { get; set; }

    [JsonPropertyName("family")]
    public string? Family { get; set; }

    [JsonPropertyName("brand")]
    public string? Brand { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("vendor_info")]
    public VendorInfo? VendorInfo { get; set; }

    [JsonPropertyName("inventory_info")]
    public InventoryInfo? InventoryInfo { get; set; }

    [JsonPropertyName("metadata")]
    public PieceMetadata? Metadata { get; set; }
}

/// <summary>
/// Vendor information
/// </summary>
public class VendorInfo
{
    [JsonPropertyName("vendor_name")]
    public string? VendorName { get; set; }

    [JsonPropertyName("contact_info")]
    public string? ContactInfo { get; set; }
}

/// <summary>
/// Inventory information
/// </summary>
public class InventoryInfo
{
    [JsonPropertyName("warehouse_location")]
    public string? WarehouseLocation { get; set; }

    [JsonPropertyName("rack_position")]
    public string? RackPosition { get; set; }

    [JsonPropertyName("availability")]
    public string? Availability { get; set; }
}

/// <summary>
/// Piece metadata
/// </summary>
public class PieceMetadata
{
    [JsonPropertyName("correlation_id")]
    public string? CorrelationId { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }
}

/// <summary>
/// Processing result for SharePoint update
/// </summary>
public class ProcessingResult
{
    public string ListItemId { get; set; } = string.Empty;
    public string? Serial { get; set; }
    public decimal ConfidenceScore { get; set; }
    public string? SKU { get; set; }
    public string? Family { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime ProcessedDateTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Calculate overall confidence score combining Document Intelligence and Piece Info results
    /// </summary>
    public static decimal CalculateOverallConfidence(decimal documentConfidence, bool pieceInfoFound, bool hasEnrichment)
    {
        var baseScore = documentConfidence;
        
        // Boost confidence if piece info was found
        if (pieceInfoFound)
        {
            baseScore += 0.1m;
        }
        
        // Additional boost if we have enrichment data (SKU, Family)
        if (hasEnrichment)
        {
            baseScore += 0.05m;
        }
        
        // Ensure we don't exceed 1.0
        return Math.Min(baseScore, 1.0m);
    }
}

/// <summary>
/// Processing context for a SharePoint list item
/// </summary>
public class ProcessingContext
{
    public string ListItemId { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public SharePointListItem? ListItem { get; set; }
    public byte[]? ImageData { get; set; }
    public string? ImageFileName { get; set; }
    public string? ImageContentType { get; set; }
    public DocumentAnalysisResponse? DocumentResult { get; set; }
    public PieceInfoResponse? PieceInfoResult { get; set; }
    public List<string> ProcessingSteps { get; set; } = new();
    public List<string> Errors { get; set; } = new();

    public void AddStep(string step)
    {
        ProcessingSteps.Add($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} - {step}");
    }

    public void AddError(string error)
    {
        Errors.Add($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} - {error}");
    }

    public TimeSpan GetElapsedTime()
    {
        return DateTime.UtcNow - StartTime;
    }
}
using System.Text.Json.Serialization;

namespace WarehouseReturns.DocumentIntelligence.Models;

/// <summary>
/// Complete response model for document analysis operations.
/// 
/// This model represents the comprehensive result of a document analysis request,
/// including extracted fields, analysis metadata, and storage information.
/// </summary>
public class DocumentAnalysisResponse
{
    /// <summary>
    /// Unique identifier for this analysis operation
    /// </summary>
    [JsonPropertyName("analysis_id")]
    public string AnalysisId { get; set; } = string.Empty;

    /// <summary>
    /// Overall status of the document analysis
    /// </summary>
    [JsonPropertyName("status")]
    public AnalysisStatus Status { get; set; }

    /// <summary>
    /// Correlation ID for request tracking
    /// </summary>
    [JsonPropertyName("correlation_id")]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Extracted serial number field results
    /// </summary>
    [JsonPropertyName("serial_field")]
    public SerialFieldResult? SerialField { get; set; }

    /// <summary>
    /// Analysis processing metadata
    /// </summary>
    [JsonPropertyName("analysis_metadata")]
    public AnalysisMetadata? AnalysisMetadata { get; set; }

    /// <summary>
    /// Blob storage information (when document is stored for review)
    /// </summary>
    [JsonPropertyName("storage_info")]
    public StorageInformation? StorageInfo { get; set; }

    /// <summary>
    /// Timestamp when analysis was completed
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Processing duration in milliseconds
    /// </summary>
    [JsonPropertyName("processing_time_ms")]
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// Error details if analysis failed
    /// </summary>
    [JsonPropertyName("error")]
    public ErrorResponse? Error { get; set; }
}

/// <summary>
/// Extracted serial number field with confidence scoring and validation status.
/// </summary>
public class SerialFieldResult
{
    /// <summary>
    /// Extracted serial number value
    /// </summary>
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    /// <summary>
    /// Confidence score for the extraction (0.0 to 1.0)
    /// </summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    /// <summary>
    /// Status of the field extraction
    /// </summary>
    [JsonPropertyName("status")]
    public FieldExtractionStatus Status { get; set; }

    /// <summary>
    /// Bounding box coordinates where the field was found
    /// </summary>
    [JsonPropertyName("bounding_region")]
    public BoundingRegion? BoundingRegion { get; set; }

    /// <summary>
    /// Text spans indicating character positions
    /// </summary>
    [JsonPropertyName("spans")]
    public List<TextSpan>? Spans { get; set; }

    /// <summary>
    /// Whether the confidence meets the acceptance threshold
    /// </summary>
    [JsonPropertyName("confidence_acceptable")]
    public bool ConfidenceAcceptable { get; set; }
}

/// <summary>
/// Analysis metadata including processing details and model information.
/// </summary>
public class AnalysisMetadata
{
    /// <summary>
    /// Azure Document Intelligence model used for analysis
    /// </summary>
    [JsonPropertyName("model_id")]
    public string? ModelId { get; set; }

    /// <summary>
    /// Document type analyzed
    /// </summary>
    [JsonPropertyName("document_type")]
    public DocumentType DocumentType { get; set; }

    /// <summary>
    /// Confidence threshold applied
    /// </summary>
    [JsonPropertyName("confidence_threshold")]
    public double ConfidenceThreshold { get; set; }

    /// <summary>
    /// API version used for analysis
    /// </summary>
    [JsonPropertyName("api_version")]
    public string? ApiVersion { get; set; }

    /// <summary>
    /// Number of pages processed
    /// </summary>
    [JsonPropertyName("page_count")]
    public int PageCount { get; set; }

    /// <summary>
    /// Source document information
    /// </summary>
    [JsonPropertyName("source_info")]
    public SourceInformation? SourceInfo { get; set; }
}

/// <summary>
/// Source document information for tracking and auditing.
/// </summary>
public class SourceInformation
{
    /// <summary>
    /// Original filename (for uploads) or URL (for URL-based processing)
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    /// <summary>
    /// Content type of the source document
    /// </summary>
    [JsonPropertyName("content_type")]
    public string? ContentType { get; set; }

    /// <summary>
    /// File size in bytes
    /// </summary>
    [JsonPropertyName("file_size")]
    public long FileSize { get; set; }

    /// <summary>
    /// Processing method used (url or upload)
    /// </summary>
    [JsonPropertyName("processing_method")]
    public string? ProcessingMethod { get; set; }
}

/// <summary>
/// Storage information for documents saved to blob storage.
/// </summary>
public class StorageInformation
{
    /// <summary>
    /// Whether the document was stored
    /// </summary>
    [JsonPropertyName("stored")]
    public bool Stored { get; set; }

    /// <summary>
    /// Container name in blob storage
    /// </summary>
    [JsonPropertyName("container_name")]
    public string? ContainerName { get; set; }

    /// <summary>
    /// Blob name/path in storage
    /// </summary>
    [JsonPropertyName("blob_name")]
    public string? BlobName { get; set; }

    /// <summary>
    /// Reason for storage (e.g., "low_confidence")
    /// </summary>
    [JsonPropertyName("storage_reason")]
    public string? StorageReason { get; set; }

    /// <summary>
    /// Timestamp when stored
    /// </summary>
    [JsonPropertyName("storage_timestamp")]
    public DateTime? StorageTimestamp { get; set; }

    /// <summary>
    /// Metadata stored with the blob
    /// </summary>
    [JsonPropertyName("blob_metadata")]
    public Dictionary<string, string>? BlobMetadata { get; set; }
}
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WarehouseReturns.DocumentIntelligence.Models;

/// <summary>
/// Request model for URL-based document analysis.
/// 
/// This model represents a request to analyze a document located at a URL.
/// It includes the document URL, model configuration, and analysis parameters
/// required for processing through Azure Document Intelligence service.
/// </summary>
public class DocumentAnalysisUrlRequest
{
    /// <summary>
    /// The URL where the document is hosted (must be publicly accessible)
    /// </summary>
    [Required]
    [Url]
    [JsonPropertyName("documentUrl")]
    public string DocumentUrl { get; set; } = string.Empty;

    /// <summary>
    /// Type of document to analyze for targeted field extraction
    /// </summary>
    [JsonPropertyName("document_type")]
    public DocumentType DocumentType { get; set; } = DocumentType.General;

    /// <summary>
    /// Azure Document Intelligence custom model ID (automatically set from configuration if not provided)
    /// </summary>
    [JsonPropertyName("modelId")]
    public string? ModelId { get; set; }

    /// <summary>
    /// Minimum confidence level for field extraction (0.0 to 1.0, automatically set from configuration if not provided)
    /// </summary>
    [Range(0.0, 1.0)]
    [JsonPropertyName("confidenceThreshold")]
    public double? ConfidenceThreshold { get; set; }

    /// <summary>
    /// Optional correlation ID for request tracking
    /// </summary>
    [JsonPropertyName("correlation_id")]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Optional metadata for the request
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Request model for file-based document analysis.
/// 
/// This model represents a request to analyze an uploaded document file.
/// It includes file metadata, model configuration, and analysis parameters
/// required for processing through Azure Document Intelligence service.
/// </summary>
public class DocumentAnalysisFileRequest
{
    /// <summary>
    /// The binary content of the uploaded file
    /// </summary>
    [Required]
    public byte[] FileContent { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Original filename of the uploaded document
    /// </summary>
    [Required]
    [StringLength(255, MinimumLength = 1)]
    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    /// <summary>
    /// MIME type of the uploaded file (e.g., 'application/pdf', 'image/jpeg')
    /// </summary>
    [Required]
    [JsonPropertyName("content_type")]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Type of document to analyze for targeted field extraction
    /// </summary>
    [JsonPropertyName("document_type")]
    public DocumentType DocumentType { get; set; } = DocumentType.General;

    /// <summary>
    /// Azure Document Intelligence custom model ID (automatically set from configuration if not provided)
    /// </summary>
    [JsonPropertyName("modelId")]
    public string? ModelId { get; set; }

    /// <summary>
    /// Minimum confidence level for field extraction (0.0 to 1.0, automatically set from configuration if not provided)
    /// </summary>
    [Range(0.0, 1.0)]
    [JsonPropertyName("confidenceThreshold")]
    public double? ConfidenceThreshold { get; set; }

    /// <summary>
    /// Optional correlation ID for request tracking
    /// </summary>
    [JsonPropertyName("correlation_id")]
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Maximum file size in MB (for validation)
    /// </summary>
    [Range(1, 100)]
    [JsonPropertyName("max_file_size_mb")]
    public int MaxFileSizeMb { get; set; } = 10;

    /// <summary>
    /// Allowed content types for validation
    /// </summary>
    [JsonPropertyName("allowed_content_types")]
    public List<string> AllowedContentTypes { get; set; } = new()
    {
        "image/jpeg",
        "image/png",
        "application/pdf"
    };

    /// <summary>
    /// Optional metadata for the request
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }
}
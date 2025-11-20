using System.ComponentModel;
using System.Text.Json.Serialization;

namespace WarehouseReturns.DocumentIntelligence.Models;

/// <summary>
/// Supported document types for analysis.
/// 
/// Enum values align with Azure Document Intelligence model types and 
/// business requirements for serial number extraction.
/// </summary>
public enum DocumentType
{
    /// <summary>Serial number extraction from product labels</summary>
    [Description("serialnumber")]
    SerialNumber,
    
    /// <summary>Product label analysis</summary>
    [Description("product_label")]
    ProductLabel,
    
    /// <summary>General document processing</summary>
    [Description("general")]
    General
}

/// <summary>
/// Overall analysis status for document processing workflow
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AnalysisStatus
{
    /// <summary>Analysis request was submitted</summary>
    [JsonPropertyName("submitted")]
    Submitted,
    
    /// <summary>Analysis is currently processing</summary>
    [JsonPropertyName("processing")]
    Processing,
    
    /// <summary>Analysis completed successfully with extracted fields</summary>
    [JsonPropertyName("succeeded")]
    Succeeded,
    
    /// <summary>Analysis failed due to processing errors</summary>
    [JsonPropertyName("failed")]
    Failed,
    
    /// <summary>Analysis requires manual review</summary>
    [JsonPropertyName("requires_review")]
    RequiresReview
}

/// <summary>
/// Field extraction status for individual document fields
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FieldExtractionStatus
{
    /// <summary>Field extracted successfully with acceptable confidence</summary>
    [JsonPropertyName("extracted")]
    Extracted,
    
    /// <summary>Field extracted but confidence below threshold</summary>
    [JsonPropertyName("low_confidence")]
    LowConfidence,
    
    /// <summary>Field not found in document</summary>
    [JsonPropertyName("not_found")]
    NotFound,
    
    /// <summary>Field extraction failed with error</summary>
    [JsonPropertyName("extraction_error")]
    ExtractionError
}

/// <summary>
/// Error codes for standardized error handling
/// </summary>
public enum ErrorCode
{
    /// <summary>Invalid request format or parameters</summary>
    InvalidRequest,
    
    /// <summary>File validation failed (size, type, content)</summary>
    FileValidationError,
    
    /// <summary>Azure Document Intelligence service error</summary>
    AzureServiceError,
    
    /// <summary>Blob storage operation failed</summary>
    BlobStorageError,
    
    /// <summary>Document processing workflow error</summary>
    ProcessingError,
    
    /// <summary>Authentication or authorization failure</summary>
    AuthenticationError,
    
    /// <summary>Service temporarily unavailable</summary>
    ServiceUnavailable,
    
    /// <summary>Internal server error</summary>
    InternalError
}
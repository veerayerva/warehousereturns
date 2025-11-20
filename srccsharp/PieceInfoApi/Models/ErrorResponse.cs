using System.Text.Json.Serialization;

namespace WarehouseReturns.PieceInfoApi.Models;

/// <summary>
/// Standardized error response structure for API endpoints
/// </summary>
public record ErrorResponse
{
    /// <summary>
    /// Human-readable error message
    /// </summary>
    [JsonPropertyName("error")]
    public string Error { get; init; } = string.Empty;

    /// <summary>
    /// Additional error details (optional)
    /// </summary>
    [JsonPropertyName("details")]
    public string? Details { get; init; }

    /// <summary>
    /// HTTP status code
    /// </summary>
    [JsonPropertyName("status_code")]
    public int StatusCode { get; init; }

    /// <summary>
    /// Timestamp when error occurred
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Correlation ID for error tracing
    /// </summary>
    [JsonPropertyName("correlation_id")]
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Validation error with field-specific details
/// </summary>
public record ValidationErrorResponse : ErrorResponse
{
    /// <summary>
    /// Field validation errors
    /// </summary>
    [JsonPropertyName("field_errors")]
    public Dictionary<string, List<string>> FieldErrors { get; init; } = new();
}
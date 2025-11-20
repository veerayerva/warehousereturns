using System.Text.Json.Serialization;

namespace WarehouseReturns.DocumentIntelligence.Models;

/// <summary>
/// Standardized error response model for API operations
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Error code for categorizing the error
    /// </summary>
    [JsonPropertyName("error_code")]
    public ErrorCode ErrorCode { get; set; }

    /// <summary>
    /// Human-readable error message
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Detailed error description
    /// </summary>
    [JsonPropertyName("details")]
    public string Details { get; set; } = string.Empty;

    /// <summary>
    /// Correlation ID for tracking
    /// </summary>
    [JsonPropertyName("correlation_id")]
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when error occurred
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional error context
    /// </summary>
    [JsonPropertyName("context")]
    public Dictionary<string, object> Context { get; set; } = new();

    /// <summary>
    /// Validation errors (if applicable)
    /// </summary>
    [JsonPropertyName("validation_errors")]
    public List<ValidationError> ValidationErrors { get; set; } = new();
}

/// <summary>
/// Individual validation error for field-level errors
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Field name that failed validation
    /// </summary>
    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Validation error message
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Attempted value that failed validation
    /// </summary>
    [JsonPropertyName("attempted_value")]
    public object AttemptedValue { get; set; } = new object();

    /// <summary>
    /// Validation rule that was violated
    /// </summary>
    [JsonPropertyName("validation_rule")]
    public string ValidationRule { get; set; } = string.Empty;
}
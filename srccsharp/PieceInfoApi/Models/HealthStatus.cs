using System.Text.Json.Serialization;

namespace WarehouseReturns.PieceInfoApi.Models;

/// <summary>
/// Comprehensive health status information for service monitoring
/// </summary>
public record HealthStatus
{
    /// <summary>
    /// Overall health status (healthy/unhealthy)
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Timestamp of health check execution
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Correlation ID for tracing health check requests
    /// </summary>
    [JsonPropertyName("correlation_id")]
    public string CorrelationId { get; init; } = string.Empty;

    /// <summary>
    /// API version information
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// Service name identifier
    /// </summary>
    [JsonPropertyName("service")]
    public string Service { get; init; } = "pieceinfo-api";

    /// <summary>
    /// Environment name (development, staging, production)
    /// </summary>
    [JsonPropertyName("environment")]
    public string Environment { get; init; } = string.Empty;

    /// <summary>
    /// Component-level health status
    /// </summary>
    [JsonPropertyName("components")]
    public Dictionary<string, string> Components { get; init; } = new();

    /// <summary>
    /// Configuration validation results
    /// </summary>
    [JsonPropertyName("configuration")]
    public HealthConfiguration Configuration { get; init; } = new();

    /// <summary>
    /// Configuration issues (if any)
    /// </summary>
    [JsonPropertyName("configuration_issues")]
    public List<string>? ConfigurationIssues { get; init; }
}

/// <summary>
/// Configuration health information
/// </summary>
public record HealthConfiguration
{
    [JsonPropertyName("base_url")]
    public string BaseUrl { get; init; } = string.Empty;

    [JsonPropertyName("timeout_seconds")]
    public double TimeoutSeconds { get; init; }

    [JsonPropertyName("max_retries")]
    public int MaxRetries { get; init; }

    [JsonPropertyName("max_batch_size")]
    public int MaxBatchSize { get; init; }

    [JsonPropertyName("subscription_key_configured")]
    public bool SubscriptionKeyConfigured { get; init; }

    [JsonPropertyName("ssl_verification")]
    public string SslVerification { get; init; } = string.Empty;

    [JsonPropertyName("log_level")]
    public string LogLevel { get; init; } = string.Empty;
}
using System.Text.Json.Serialization;

namespace WarehouseReturns.DocumentIntelligence.Models;

/// <summary>
/// Health status enumeration for service monitoring.
/// 
/// Represents the operational state of services and components
/// in the document intelligence system.
/// </summary>
public enum HealthStatus
{
    /// <summary>Service is fully operational with normal performance</summary>
    Healthy,
    
    /// <summary>Service is operational but with degraded performance or non-critical issues</summary>
    Degraded,
    
    /// <summary>Service has critical issues and may not function properly</summary>
    Unhealthy
}

/// <summary>
/// Comprehensive health check response for the document processing service.
/// 
/// Provides detailed status information about the service and all its dependencies
/// including Azure Document Intelligence, Blob Storage, and configuration validation.
/// </summary>
/// <example>
/// <code>
/// {
///   "status": "Healthy",
///   "timestamp": "2024-01-15T10:30:00Z",
///   "service_version": "1.0.0",
///   "health_checks": [
///     {
///       "component": "DocumentIntelligence",
///       "status": "Healthy",
///       "response_time": 234,
///       "description": "Azure Document Intelligence API is operational"
///     }
///   ]
/// }
/// </code>
/// </example>
public class HealthCheckResponse
{
    /// <summary>
    /// Overall health status of the service
    /// </summary>
    [JsonPropertyName("status")]
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Timestamp when the health check was performed
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Service version information
    /// </summary>
    [JsonPropertyName("service_version")]
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Environment where the service is running (Development, Staging, Production)
    /// </summary>
    [JsonPropertyName("environment")]
    public string Environment { get; set; } = "Unknown";

    /// <summary>
    /// Individual component health check results
    /// </summary>
    [JsonPropertyName("health_checks")]
    public List<ComponentHealthCheck> HealthChecks { get; set; } = new();

    /// <summary>
    /// Service performance metrics
    /// </summary>
    [JsonPropertyName("performance_metrics")]
    public PerformanceMetrics? PerformanceMetrics { get; set; }

    /// <summary>
    /// Overall response time for the health check in milliseconds
    /// </summary>
    [JsonPropertyName("total_response_time_ms")]
    public long TotalResponseTimeMs { get; set; }

    /// <summary>
    /// Any system-wide issues or warnings
    /// </summary>
    [JsonPropertyName("system_issues")]
    public List<string> SystemIssues { get; set; } = new();

    /// <summary>
    /// Recommended actions if issues are found
    /// </summary>
    [JsonPropertyName("recommendations")]
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Health check result for an individual service component.
/// 
/// Represents the status and performance of a specific dependency
/// such as Azure Document Intelligence API, Blob Storage, or configuration.
/// </summary>
public class ComponentHealthCheck
{
    /// <summary>
    /// Name of the component being checked
    /// </summary>
    [JsonPropertyName("component")]
    public string Component { get; set; } = string.Empty;

    /// <summary>
    /// Health status of this component
    /// </summary>
    [JsonPropertyName("status")]
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Response time for this component check in milliseconds
    /// </summary>
    [JsonPropertyName("response_time_ms")]
    public long ResponseTimeMs { get; set; }

    /// <summary>
    /// Human-readable description of the component status
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Detailed error information if the component is unhealthy
    /// </summary>
    [JsonPropertyName("error_details")]
    public string? ErrorDetails { get; set; }

    /// <summary>
    /// Specific issues identified with this component
    /// </summary>
    [JsonPropertyName("issues")]
    public List<string> Issues { get; set; } = new();

    /// <summary>
    /// Recommended actions for resolving component issues
    /// </summary>
    [JsonPropertyName("recommendations")]
    public List<string> Recommendations { get; set; } = new();

    /// <summary>
    /// Component-specific metrics and data
    /// </summary>
    [JsonPropertyName("metrics")]
    public Dictionary<string, object>? Metrics { get; set; }

    /// <summary>
    /// Last known good timestamp for this component
    /// </summary>
    [JsonPropertyName("last_success")]
    public DateTime? LastSuccess { get; set; }
}

/// <summary>
/// Performance metrics for the document processing service.
/// 
/// Tracks key performance indicators for monitoring service health
/// and identifying potential bottlenecks or degradation.
/// </summary>
public class PerformanceMetrics
{
    /// <summary>
    /// Average document processing time in milliseconds over recent requests
    /// </summary>
    [JsonPropertyName("avg_processing_time_ms")]
    public double AverageProcessingTimeMs { get; set; }

    /// <summary>
    /// Number of documents processed in the last hour
    /// </summary>
    [JsonPropertyName("requests_per_hour")]
    public int RequestsPerHour { get; set; }

    /// <summary>
    /// Success rate percentage for recent document processing attempts
    /// </summary>
    [JsonPropertyName("success_rate_percentage")]
    public double SuccessRatePercentage { get; set; }

    /// <summary>
    /// Average confidence score for successful extractions
    /// </summary>
    [JsonPropertyName("avg_confidence_score")]
    public double AverageConfidenceScore { get; set; }

    /// <summary>
    /// Percentage of documents that required storage due to low confidence
    /// </summary>
    [JsonPropertyName("storage_rate_percentage")]
    public double StorageRatePercentage { get; set; }

    /// <summary>
    /// Memory usage in MB
    /// </summary>
    [JsonPropertyName("memory_usage_mb")]
    public double MemoryUsageMb { get; set; }

    /// <summary>
    /// CPU usage percentage
    /// </summary>
    [JsonPropertyName("cpu_usage_percentage")]
    public double CpuUsagePercentage { get; set; }

    /// <summary>
    /// Available disk space in GB
    /// </summary>
    [JsonPropertyName("available_disk_gb")]
    public double AvailableDiskGb { get; set; }
}
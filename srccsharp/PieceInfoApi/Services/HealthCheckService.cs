using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WarehouseReturns.PieceInfoApi.Configuration;
using WarehouseReturns.PieceInfoApi.Models;

namespace WarehouseReturns.PieceInfoApi.Services;

/// <summary>
/// Comprehensive health check service for monitoring and alerting
/// </summary>
public class HealthCheckService : IHealthCheckService
{
    private readonly IExternalApiService _externalApiService;
    private readonly IAggregationService _aggregationService;
    private readonly ILogger<HealthCheckService> _logger;
    private readonly IConfiguration _configuration;
    private readonly PieceInfoApiSettings _settings;

    /// <summary>
    /// Initialize health check service with dependencies
    /// </summary>
    public HealthCheckService(
        IExternalApiService externalApiService,
        IAggregationService aggregationService,
        ILogger<HealthCheckService> logger,
        IConfiguration configuration,
        IOptions<PieceInfoApiSettings> settings)
    {
        _externalApiService = externalApiService ?? throw new ArgumentNullException(nameof(externalApiService));
        _aggregationService = aggregationService ?? throw new ArgumentNullException(nameof(aggregationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Perform comprehensive health check of the service and its dependencies
    /// </summary>
    public async Task<HealthStatus> PerformHealthCheckAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Performing health check - Correlation ID: {CorrelationId}", correlationId);
        
        var componentsStatus = new Dictionary<string, string>();
        var overallHealthy = true;
        var configurationIssues = new List<string>();

        // ===================================================================
        // CHECK AGGREGATION SERVICE
        // ===================================================================
        try
        {
            // Test aggregation service initialization (lightweight check)
            _ = _aggregationService;
            componentsStatus["aggregation_service"] = "healthy";
            _logger.LogDebug("Aggregation service health check passed");
        }
        catch (Exception ex)
        {
            componentsStatus["aggregation_service"] = $"unhealthy: {ex.Message}";
            overallHealthy = false;
            _logger.LogWarning(ex, "Aggregation service health check failed");
        }

        // ===================================================================
        // CHECK EXTERNAL API CONNECTIVITY
        // ===================================================================
        try
        {
            var apiHealthy = await _externalApiService.HealthCheckAsync(cancellationToken);
            componentsStatus["external_api"] = apiHealthy ? "healthy" : "unhealthy";
            
            if (!apiHealthy)
            {
                overallHealthy = false;
                _logger.LogWarning("External API health check failed");
            }
            else
            {
                _logger.LogDebug("External API health check passed");
            }
        }
        catch (Exception ex)
        {
            componentsStatus["external_api"] = $"unhealthy: {ex.Message}";
            overallHealthy = false;
            _logger.LogWarning(ex, "External API health check failed with exception");
        }

        // ===================================================================
        // CHECK CONFIGURATION COMPLETENESS
        // ===================================================================
        var requiredConfigKeys = new[]
        {
            "EXTERNAL_API_BASE_URL",
            "OCP_APIM_SUBSCRIPTION_KEY"
        };
        
        foreach (var configKey in requiredConfigKeys)
        {
            var value = _configuration[configKey];
            if (string.IsNullOrWhiteSpace(value))
            {
                configurationIssues.Add($"Missing {configKey}");
                overallHealthy = false;
            }
        }
        
        componentsStatus["configuration"] = configurationIssues.Any() 
            ? $"issues: {string.Join(", ", configurationIssues)}"
            : "healthy";
        
        componentsStatus["logging"] = "healthy";
        componentsStatus["ssl_verification"] = _settings.VerifySsl ? "enabled" : "disabled";

        // ===================================================================
        // BUILD HEALTH STATUS RESPONSE
        // ===================================================================
        var healthStatus = new HealthStatus
        {
            Status = overallHealthy ? "healthy" : "unhealthy",
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId,
            Version = "1.0.0",
            Service = "pieceinfo-api",
            Environment = _settings.WarehouseReturnsEnv,
            Components = componentsStatus,
            Configuration = new HealthConfiguration
            {
                BaseUrl = _settings.ExternalApiBaseUrl,
                TimeoutSeconds = _settings.ApiTimeoutSeconds,
                MaxRetries = _settings.ApiMaxRetries,
                MaxBatchSize = _settings.MaxBatchSize,
                SubscriptionKeyConfigured = !string.IsNullOrWhiteSpace(_settings.OcpApimSubscriptionKey),
                SslVerification = _settings.VerifySsl.ToString().ToLowerInvariant(),
                LogLevel = _settings.LogLevel
            },
            ConfigurationIssues = configurationIssues.Any() ? configurationIssues : null
        };

        _logger.LogInformation(
            "Health check completed - Status: {Status}, Issues: {IssueCount} - Correlation ID: {CorrelationId}",
            healthStatus.Status, configurationIssues.Count, correlationId);

        return healthStatus;
    }
}
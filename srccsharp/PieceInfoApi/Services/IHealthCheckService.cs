using WarehouseReturns.PieceInfoApi.Models;

namespace WarehouseReturns.PieceInfoApi.Services;

/// <summary>
/// Interface for health check service providing system health monitoring
/// </summary>
public interface IHealthCheckService
{
    /// <summary>
    /// Perform comprehensive health check of the service and its dependencies
    /// </summary>
    /// <param name="correlationId">Correlation ID for request tracing</param>
    /// <param name="cancellationToken">Cancellation token for request timeout</param>
    /// <returns>Detailed health status information</returns>
    Task<HealthStatus> PerformHealthCheckAsync(string correlationId, CancellationToken cancellationToken = default);
}
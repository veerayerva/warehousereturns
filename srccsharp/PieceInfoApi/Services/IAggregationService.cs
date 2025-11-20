using WarehouseReturns.PieceInfoApi.Models;

namespace WarehouseReturns.PieceInfoApi.Services;

/// <summary>
/// Interface for piece information aggregation service
/// </summary>
public interface IAggregationService
{
    /// <summary>
    /// Get comprehensive aggregated piece information by orchestrating multiple API calls
    /// </summary>
    /// <param name="pieceNumber">Unique piece inventory identifier</param>
    /// <param name="correlationId">Correlation ID for request tracing</param>
    /// <param name="cancellationToken">Cancellation token for request timeout</param>
    /// <returns>Aggregated piece information from all data sources</returns>
    Task<AggregatedPieceInfo> GetAggregatedPieceInfoAsync(
        string pieceNumber, 
        string correlationId, 
        CancellationToken cancellationToken = default);
}
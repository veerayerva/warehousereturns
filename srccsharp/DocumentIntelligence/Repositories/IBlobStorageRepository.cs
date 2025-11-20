using WarehouseReturns.DocumentIntelligence.Models;

namespace WarehouseReturns.DocumentIntelligence.Repositories;

/// <summary>
/// Interface for blob storage operations
/// </summary>
public interface IBlobStorageRepository
{
    /// <summary>
    /// Store a low-confidence document in blob storage for review
    /// </summary>
    /// <param name="analysisId">Analysis ID for tracking</param>
    /// <param name="documentBytes">Document binary content</param>
    /// <param name="filename">Original filename</param>
    /// <param name="contentType">MIME type</param>
    /// <param name="serialField">Extracted serial field results</param>
    /// <param name="metadata">Additional metadata</param>
    /// <param name="correlationId">Correlation ID for tracing</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Storage information with blob details</returns>
    Task<StorageInformation> StoreLowConfidenceDocumentAsync(
        string analysisId,
        byte[] documentBytes,
        string filename,
        string contentType,
        SerialFieldResult serialField,
        Dictionary<string, string> metadata,
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensure the blob container exists and is properly configured
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if container is ready</returns>
    Task<bool> EnsureContainerExistsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get blob storage health status
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health check results</returns>
    Task<Dictionary<string, object>> HealthCheckAsync(CancellationToken cancellationToken = default);
}
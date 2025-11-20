using WarehouseReturns.PieceInfoApi.Models;

namespace WarehouseReturns.PieceInfoApi.Services;

/// <summary>
/// Interface for external API service providing HTTP communication with external data sources
/// </summary>
public interface IExternalApiService
{
    /// <summary>
    /// Get piece inventory location details from external API
    /// </summary>
    /// <param name="pieceNumber">Unique piece inventory identifier</param>
    /// <param name="cancellationToken">Cancellation token for request timeout</param>
    /// <returns>Piece inventory response data</returns>
    Task<PieceInventoryResponse> GetPieceInventoryAsync(string pieceNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get product master information from external API
    /// </summary>
    /// <param name="sku">Stock keeping unit identifier</param>
    /// <param name="cancellationToken">Cancellation token for request timeout</param>
    /// <returns>Product master response data</returns>
    Task<ProductMasterResponse> GetProductMasterAsync(string sku, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get vendor details from external API
    /// </summary>
    /// <param name="vendorCode">Vendor identifier code</param>
    /// <param name="cancellationToken">Cancellation token for request timeout</param>
    /// <returns>Vendor details response data</returns>
    Task<VendorDetailsResponse> GetVendorDetailsAsync(string vendorCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Perform health check on external API connectivity
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for request timeout</param>
    /// <returns>True if external API is accessible, false otherwise</returns>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}
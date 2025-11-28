using VendorReturnsService.Models;

namespace VendorReturnsService.Services;

/// <summary>
/// Interface for PieceInfo API operations.
/// </summary>
public interface IPieceInfoService
{
    /// <summary>
    /// Retrieves piece information by piece number.
    /// </summary>
    Task<PieceInfoResponse> GetPieceInfoAsync(string pieceNumber, string correlationId);
}

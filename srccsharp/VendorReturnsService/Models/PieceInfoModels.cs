namespace VendorReturnsService.Models;

/// <summary>
/// Response from PieceInfo API.
/// </summary>
public class PieceInfoResponse
{
    public bool Success { get; set; }
    public PieceInfoData? Data { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Piece information data.
/// </summary>
public class PieceInfoData
{
    public string? SkuNumber { get; set; }
    public string? WarehouseLocation { get; set; }
    public string? Vendor { get; set; }
    public string? Family { get; set; }
    public string? ModelNumber { get; set; }
    public string? RackLocation { get; set; }
}

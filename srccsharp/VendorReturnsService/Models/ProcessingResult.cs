namespace VendorReturnsService.Models;

/// <summary>
/// Result of processing a vendor return item.
/// </summary>
public class ProcessingResult
{
    public bool Success { get; set; }
    public string ListItemId { get; set; } = string.Empty;
    public string? PieceNumber { get; set; }
    public string? SerialNumber { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ProcessStatus { get; set; }
    public Dictionary<string, string> ProcessingDetails { get; set; } = new();
}

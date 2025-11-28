namespace VendorReturnsService.Models;

/// <summary>
/// Result from Document Intelligence processing.
/// </summary>
public class DocumentIntelligenceResult
{
    public bool Success { get; set; }
    public Dictionary<string, string> ExtractedFields { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public double? ConfidenceScore { get; set; }
}

/// <summary>
/// Configuration for a Document Intelligence service instance.
/// </summary>
public class DocumentIntelligenceConfig
{
    public string ServiceName { get; set; } = string.Empty; // "PieceImage" or "SerialImage" - used for logging only
}

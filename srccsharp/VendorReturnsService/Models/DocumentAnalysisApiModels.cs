namespace VendorReturnsService.Models;

/// <summary>
/// Response model from Document Intelligence API
/// </summary>
public class DocumentAnalysisApiResponse
{
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public FieldResult? SerialField { get; set; }
    public FieldResult? PieceField { get; set; }
}

/// <summary>
/// Field extraction result with value and confidence score
/// </summary>
public class FieldResult
{
    public string? Value { get; set; }
    public double? Confidence { get; set; }
}

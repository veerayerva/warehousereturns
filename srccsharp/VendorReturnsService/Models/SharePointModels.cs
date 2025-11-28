namespace VendorReturnsService.Models;

/// <summary>
/// Represents a vendor return item in SharePoint.
/// </summary>
public class VendorReturnItem
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ProcessStatus { get; set; }
    public string? PieceNumber { get; set; }
    public string? SerialNumber { get; set; }
    public string? SkuNumber { get; set; }
    public string? WarehouseLocation { get; set; }
    public string? Vendor { get; set; }
    public string? Family { get; set; }
    public string? ModelNumber { get; set; }
    public string? RackLocation { get; set; }
    
    // Image fields for Attachment source
    public string? PieceImage { get; set; }
    public string? SerialImage { get; set; }
    
    // URL fields for SharePointDrive source
    public string? PieceImageUrl { get; set; }
    public string? SerialImageUrl { get; set; }
}

/// <summary>
/// Update model for SharePoint list item.
/// </summary>
public class SharePointUpdateModel
{
    public string? PieceNumber { get; set; }
    public string? SerialNumber { get; set; }
    public string? SkuNumber { get; set; }
    public string? WarehouseLocation { get; set; }
    public string? Vendor { get; set; }
    public string? Family { get; set; }
    public string? ModelNumber { get; set; }
    public string? RackLocation { get; set; }
    public string? Status { get; set; }
    public string? ProcessStatus { get; set; }
}

/// <summary>
/// Attachment metadata from SharePoint.
/// </summary>
public class AttachmentInfo
{
    public string FileName { get; set; } = string.Empty;
    public string ServerRelativeUrl { get; set; } = string.Empty;
}

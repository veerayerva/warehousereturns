using VendorReturnsService.Models;

namespace VendorReturnsService.Services;

/// <summary>
/// Interface for SharePoint operations.
/// </summary>
public interface ISharePointService
{
    /// <summary>
    /// Retrieves list items with specified status.
    /// </summary>
    Task<List<VendorReturnItem>> GetItemsByStatusAsync(string status, string? excludeProcessStatus, string correlationId);
    
    /// <summary>
    /// Retrieves a single list item by ID.
    /// </summary>
    Task<VendorReturnItem?> GetItemByIdAsync(string listItemId, string correlationId);
    
    /// <summary>
    /// Updates a list item with specified fields.
    /// </summary>
    Task<bool> UpdateItemAsync(string listItemId, SharePointUpdateModel updates, string correlationId);
    
    /// <summary>
    /// Downloads an attachment from a list item.
    /// </summary>
    Task<byte[]?> DownloadAttachmentAsync(string listItemId, string fileName, string correlationId);
    
    /// <summary>
    /// Downloads an image from SharePoint Drive by URL.
    /// </summary>
    Task<byte[]?> DownloadImageFromDriveAsync(string imageUrl, string correlationId);
}

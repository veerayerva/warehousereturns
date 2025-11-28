using VendorReturnsService.Models;

namespace VendorReturnsService.Services;

/// <summary>
/// Interface for vendor return processing orchestration.
/// </summary>
public interface IVendorReturnProcessor
{
    /// <summary>
    /// Processes a single vendor return item through the complete workflow.
    /// </summary>
    Task<ProcessingResult> ProcessReturnItemAsync(string listItemId, string correlationId);
}

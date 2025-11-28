using VendorReturnsService.Models;

namespace VendorReturnsService.Services;

/// <summary>
/// Interface for Document Intelligence operations.
/// </summary>
public interface IDocumentIntelligenceService
{
    /// <summary>
    /// Analyzes a document image and extracts fields using specified model.
    /// </summary>
    Task<DocumentIntelligenceResult> AnalyzeDocumentAsync(byte[] imageData, DocumentIntelligenceConfig config, string correlationId);
}

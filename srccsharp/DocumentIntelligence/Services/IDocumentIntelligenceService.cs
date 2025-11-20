using WarehouseReturns.DocumentIntelligence.Models;

namespace WarehouseReturns.DocumentIntelligence.Services;

/// <summary>
/// Interface for Azure Document Intelligence service operations
/// </summary>
public interface IDocumentIntelligenceService
{
    /// <summary>
    /// Analyze a document from URL using Azure Document Intelligence
    /// </summary>
    /// <param name="request">URL-based analysis request</param>
    /// <param name="correlationId">Correlation ID for tracing</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Analysis results and error (if any)</returns>
    Task<(AzureDocumentIntelligenceResponse?, ErrorResponse?)> AnalyzeDocumentFromUrlAsync(
        DocumentAnalysisUrlRequest request,
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyze a document from bytes using Azure Document Intelligence
    /// </summary>
    /// <param name="documentBytes">Document binary content</param>
    /// <param name="request">File-based analysis request</param>
    /// <param name="filename">Original filename</param>
    /// <param name="contentType">MIME type</param>
    /// <param name="correlationId">Correlation ID for tracing</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Analysis results and error (if any)</returns>
    Task<(AzureDocumentIntelligenceResponse?, ErrorResponse?)> AnalyzeDocumentFromBytesAsync(
        byte[] documentBytes,
        DocumentAnalysisFileRequest request,
        string filename,
        string contentType,
        string correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Perform health check on the Document Intelligence service
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health check results</returns>
    Task<Dictionary<string, object>> HealthCheckAsync(CancellationToken cancellationToken = default);
}
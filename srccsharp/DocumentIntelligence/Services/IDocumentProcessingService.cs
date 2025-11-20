using WarehouseReturns.DocumentIntelligence.Models;

namespace WarehouseReturns.DocumentIntelligence.Services;

/// <summary>
/// Document processing service interface for comprehensive document analysis operations.
/// 
/// This service orchestrates document intelligence operations by coordinating between
/// Azure Document Intelligence API calls, confidence evaluation, and blob storage
/// management for low-confidence results requiring human review.
/// 
/// Key Features:
/// - URL-based document processing with automatic download
/// - File upload processing (future implementation)
/// - Confidence-based storage routing for quality assurance
/// - Health monitoring and service availability checks
/// - Comprehensive error handling and retry mechanisms
/// </summary>
/// <example>
/// <code>
/// // URL-based processing
/// var request = new DocumentAnalysisUrlRequest
/// {
///     DocumentUrl = "https://example.com/document.pdf",
///     ConfidenceThreshold = 0.8
/// };
/// var result = await processingService.ProcessDocumentFromUrlAsync(request, correlationId);
/// 
/// // Health monitoring
/// var health = await processingService.HealthCheckAsync();
/// if (health.Status == HealthStatus.Healthy)
/// {
///     // Service is operational
/// }
/// </code>
/// </example>
public interface IDocumentProcessingService
{
    /// <summary>
    /// Process a document from a URL for serial number extraction.
    /// 
    /// Downloads the document from the provided URL, analyzes it using Azure Document Intelligence,
    /// evaluates confidence levels, and stores low-confidence documents for manual review.
    /// 
    /// Processing Flow:
    /// 1. Validate URL and request parameters
    /// 2. Download document content with retry logic
    /// 3. Submit to Azure Document Intelligence API
    /// 4. Extract and validate serial number fields
    /// 5. Evaluate confidence against threshold
    /// 6. Store document if confidence is below threshold
    /// 7. Return comprehensive analysis results
    /// </summary>
    /// <param name="request">Document analysis request containing URL and processing parameters</param>
    /// <param name="correlationId">Correlation ID for tracking and logging</param>
    /// <returns>
    /// Comprehensive analysis response including:
    /// - Extracted serial number with confidence score
    /// - Analysis metadata and processing information
    /// - Storage details for low-confidence documents
    /// - Error information if processing failed
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null</exception>
    /// <exception cref="ArgumentException">Thrown when request contains invalid parameters</exception>
    /// <exception cref="HttpRequestException">Thrown when document download fails</exception>
    /// <exception cref="InvalidOperationException">Thrown when Azure Document Intelligence API fails</exception>
    /// <example>
    /// <code>
    /// var request = new DocumentAnalysisUrlRequest
    /// {
    ///     DocumentUrl = "https://storage.example.com/documents/invoice-12345.pdf",
    ///     DocumentType = "invoice",
    ///     ModelId = "serialnumber-v1.0",
    ///     ConfidenceThreshold = 0.75
    /// };
    /// 
    /// try
    /// {
    ///     var result = await service.ProcessDocumentFromUrlAsync(request, "corr-123");
    ///     
    ///     if (result.SerialField?.ConfidenceAcceptable == true)
    ///     {
    ///         Console.WriteLine($"Serial: {result.SerialField.Value}");
    ///     }
    ///     else
    ///     {
    ///         Console.WriteLine($"Low confidence, stored for review: {result.StorageInfo?.BlobName}");
    ///     }
    /// }
    /// catch (HttpRequestException ex)
    /// {
    ///     // Handle document download failure
    /// }
    /// </code>
    /// </example>
    Task<DocumentAnalysisResponse> ProcessDocumentFromUrlAsync(
        DocumentAnalysisUrlRequest request, 
        string correlationId);

    /// <summary>
    /// Process a document from uploaded file data for serial number extraction.
    /// 
    /// Analyzes uploaded file content using Azure Document Intelligence, evaluates
    /// confidence levels, and manages storage for documents requiring review.
    /// 
    /// This method provides the same analysis capabilities as URL-based processing
    /// but works with file upload scenarios where documents are provided as byte arrays.
    /// </summary>
    /// <param name="request">File upload request containing document data and processing parameters</param>
    /// <param name="correlationId">Correlation ID for tracking and logging</param>
    /// <returns>
    /// Comprehensive analysis response with extraction results and storage information
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null</exception>
    /// <exception cref="ArgumentException">Thrown when file data is invalid</exception>
    /// <exception cref="InvalidOperationException">Thrown when analysis fails</exception>
    /// <example>
    /// <code>
    /// var fileBytes = await File.ReadAllBytesAsync("document.pdf");
    /// var request = new DocumentAnalysisFileRequest
    /// {
    ///     FileData = fileBytes,
    ///     FileName = "document.pdf",
    ///     ContentType = "application/pdf",
    ///     ConfidenceThreshold = 0.8
    /// };
    /// 
    /// var result = await service.ProcessDocumentFromFileAsync(request, "corr-456");
    /// </code>
    /// </example>
    Task<DocumentAnalysisResponse> ProcessDocumentFromFileAsync(
        DocumentAnalysisFileRequest request, 
        string correlationId);

    /// <summary>
    /// Perform comprehensive health check of the document processing service and its dependencies.
    /// 
    /// Validates connectivity and operational status of:
    /// - Azure Document Intelligence API endpoints
    /// - Azure Blob Storage connectivity
    /// - Configuration validation and API key verification
    /// - Service performance metrics and response times
    /// 
    /// Health Status Levels:
    /// - Healthy: All services operational, normal response times
    /// - Degraded: Some non-critical issues, may have slower response
    /// - Unhealthy: Critical failures, service may not function properly
    /// </summary>
    /// <returns>
    /// Detailed health status including:
    /// - Overall service health status
    /// - Individual component health checks
    /// - Performance metrics and response times
    /// - Configuration validation results
    /// - Recommendations for issue resolution
    /// </returns>
    /// <exception cref="TimeoutException">Thrown when health check operations timeout</exception>
    /// <example>
    /// <code>
    /// var health = await service.HealthCheckAsync();
    /// 
    /// Console.WriteLine($"Overall Status: {health.Status}");
    /// 
    /// foreach (var check in health.HealthChecks)
    /// {
    ///     Console.WriteLine($"{check.Component}: {check.Status} ({check.ResponseTime}ms)");
    ///     
    ///     if (check.Status != HealthStatus.Healthy)
    ///     {
    ///         Console.WriteLine($"  Issues: {string.Join(", ", check.Issues)}");
    ///         Console.WriteLine($"  Recommendations: {string.Join(", ", check.Recommendations)}");
    ///     }
    /// }
    /// </code>
    /// </example>
    Task<HealthCheckResponse> HealthCheckAsync();
}
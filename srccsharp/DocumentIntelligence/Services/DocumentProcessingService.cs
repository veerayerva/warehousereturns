using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using WarehouseReturns.DocumentIntelligence.Configuration;
using WarehouseReturns.DocumentIntelligence.Models;
using WarehouseReturns.DocumentIntelligence.Repositories;

namespace WarehouseReturns.DocumentIntelligence.Services;

/// <summary>
/// Comprehensive document processing service implementation for Azure Document Intelligence operations.
/// 
/// This service orchestrates the complete document analysis workflow including:
/// - Document download from URLs with retry logic and validation
/// - Azure Document Intelligence API integration for field extraction
/// - Confidence evaluation and quality assurance routing
/// - Blob storage management for low-confidence documents requiring review
/// - Health monitoring and dependency validation
/// - Comprehensive error handling and logging
/// 
/// The service implements enterprise-grade patterns including:
/// - Structured logging with correlation IDs
/// - Retry policies for transient failures
/// - Performance monitoring and metrics collection
/// - Security validation and input sanitization
/// </summary>
/// <example>
/// <code>
/// // Dependency injection setup
/// services.AddScoped&lt;IDocumentProcessingService, DocumentProcessingService&gt;();
/// 
/// // Usage in controller or function
/// var request = new DocumentAnalysisUrlRequest 
/// { 
///     DocumentUrl = "https://example.com/document.pdf",
///     ConfidenceThreshold = 0.8 
/// };
/// 
/// var result = await processingService.ProcessDocumentFromUrlAsync(request, correlationId);
/// </code>
/// </example>
public class DocumentProcessingService : IDocumentProcessingService
{
    private readonly IDocumentIntelligenceService _documentIntelligenceService;
    private readonly IBlobStorageRepository _blobStorageRepository;
    private readonly HttpClient _httpClient;
    private readonly ILogger<DocumentProcessingService> _logger;
    private readonly DocumentProcessingSettings _processingSettings;
    private readonly DocumentIntelligenceSettings _intelligenceSettings;
    private readonly BlobStorageSettings _blobSettings;

    /// <summary>
    /// Initializes a new instance of the DocumentProcessingService with required dependencies.
    /// </summary>
    /// <param name="documentIntelligenceService">Azure Document Intelligence API service</param>
    /// <param name="blobStorageRepository">Blob storage repository for document management</param>
    /// <param name="httpClient">HTTP client for document download operations</param>
    /// <param name="logger">Structured logging service</param>
    /// <param name="processingOptions">Document processing configuration options</param>
    /// <param name="intelligenceOptions">Azure Document Intelligence configuration options</param>
    /// <param name="blobOptions">Blob storage configuration options</param>
    public DocumentProcessingService(
        IDocumentIntelligenceService documentIntelligenceService,
        IBlobStorageRepository blobStorageRepository,
        HttpClient httpClient,
        ILogger<DocumentProcessingService> logger,
        IOptions<DocumentProcessingSettings> processingOptions,
        IOptions<DocumentIntelligenceSettings> intelligenceOptions,
        IOptions<BlobStorageSettings> blobOptions)
    {
        _documentIntelligenceService = documentIntelligenceService ?? throw new ArgumentNullException(nameof(documentIntelligenceService));
        _blobStorageRepository = blobStorageRepository ?? throw new ArgumentNullException(nameof(blobStorageRepository));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _processingSettings = processingOptions?.Value ?? throw new ArgumentNullException(nameof(processingOptions));
        _intelligenceSettings = intelligenceOptions?.Value ?? throw new ArgumentNullException(nameof(intelligenceOptions));
        _blobSettings = blobOptions?.Value ?? throw new ArgumentNullException(nameof(blobOptions));

        // Configure HTTP client timeout
        _httpClient.Timeout = TimeSpan.FromSeconds(_processingSettings.AZURE_API_TIMEOUT);
    }

    /// <inheritdoc />
    public async Task<DocumentAnalysisResponse> ProcessDocumentFromUrlAsync(
        DocumentAnalysisUrlRequest request, 
        string correlationId)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(correlationId))
            correlationId = Guid.NewGuid().ToString();

        var stopwatch = Stopwatch.StartNew();
        var analysisId = Guid.NewGuid().ToString();

        _logger.LogInformation(
            "[DOCUMENT-PROCESSING-START] Analysis-ID: {AnalysisId}, URL: {DocumentUrl}, " +
            "Model: {ModelId}, Confidence-Threshold: {ConfidenceThreshold}, Correlation-ID: {CorrelationId}",
            analysisId, request.DocumentUrl, request.ModelId, request.ConfidenceThreshold, correlationId);

        try
        {
            // Step 1: Validate request
            ValidateRequest(request);

            // Step 2: Call Azure Document Intelligence directly with URL
            var (analysisResult, error) = await _documentIntelligenceService.AnalyzeDocumentFromUrlAsync(
                request, correlationId);

            if (error != null || analysisResult == null)
            {
                return CreateErrorResponse(analysisId, error?.Message ?? "Analysis failed", stopwatch.ElapsedMilliseconds, correlationId);
            }

            // Step 3: Convert Azure response to our response model
            var response = await ConvertToDocumentAnalysisResponseAsync(
                analysisId, analysisResult, request, correlationId);

            stopwatch.Stop();
            response.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation(
                "[DOCUMENT-PROCESSING-COMPLETE] Analysis-ID: {AnalysisId}, Status: {Status}, " +
                "Serial-Found: {SerialFound}, Confidence: {Confidence}, Processing-Time: {ProcessingTime}ms, " +
                "Correlation-ID: {CorrelationId}",
                analysisId, response.Status, response.SerialField?.Value != null, 
                response.SerialField?.Confidence ?? 0, response.ProcessingTimeMs, correlationId);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, 
                "[DOCUMENT-PROCESSING-ERROR] Analysis-ID: {AnalysisId}, Error: {ErrorMessage}, " +
                "Processing-Time: {ProcessingTime}ms, Correlation-ID: {CorrelationId}",
                analysisId, ex.Message, stopwatch.ElapsedMilliseconds, correlationId);

            return CreateErrorResponse(analysisId, ex.Message, stopwatch.ElapsedMilliseconds, correlationId);
        }
    }

    /// <inheritdoc />
    public async Task<DocumentAnalysisResponse> ProcessDocumentFromFileAsync(
        DocumentAnalysisFileRequest request, 
        string correlationId)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(correlationId))
            correlationId = Guid.NewGuid().ToString();

        var stopwatch = Stopwatch.StartNew();
        var analysisId = Guid.NewGuid().ToString();

        _logger.LogInformation(
            "[FILE-PROCESSING-START] Analysis-ID: {AnalysisId}, Filename: {Filename}, " +
            "Content-Type: {ContentType}, File-Size: {FileSize} bytes, Correlation-ID: {CorrelationId}",
            analysisId, request.Filename, request.ContentType, request.FileContent.Length, correlationId);

        try
        {
            // Step 1: Validate request
            ValidateFileRequest(request);

            // Step 2: Call Azure Document Intelligence with file data
            var (analysisResult, error) = await _documentIntelligenceService.AnalyzeDocumentFromBytesAsync(
                request.FileContent, request, request.Filename, request.ContentType, correlationId);

            if (error != null || analysisResult == null)
            {
                return CreateErrorResponse(analysisId, error?.Message ?? "Analysis failed", stopwatch.ElapsedMilliseconds, correlationId);
            }

            // Step 3: Convert Azure response to our response model
            var response = await ConvertToDocumentAnalysisResponseAsync(
                analysisId, analysisResult, request, correlationId);

            stopwatch.Stop();
            response.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation(
                "[FILE-PROCESSING-COMPLETE] Analysis-ID: {AnalysisId}, Status: {Status}, " +
                "Serial-Found: {SerialFound}, Confidence: {Confidence}, Processing-Time: {ProcessingTime}ms, " +
                "Correlation-ID: {CorrelationId}",
                analysisId, response.Status, response.SerialField?.Value != null, 
                response.SerialField?.Confidence ?? 0, response.ProcessingTimeMs, correlationId);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, 
                "[FILE-PROCESSING-ERROR] Analysis-ID: {AnalysisId}, Error: {ErrorMessage}, " +
                "Processing-Time: {ProcessingTime}ms, Correlation-ID: {CorrelationId}",
                analysisId, ex.Message, stopwatch.ElapsedMilliseconds, correlationId);

            return CreateErrorResponse(analysisId, ex.Message, stopwatch.ElapsedMilliseconds, correlationId);
        }
    }

    /// <inheritdoc />
    public async Task<HealthCheckResponse> HealthCheckAsync()
    {
        var correlationId = Guid.NewGuid().ToString();
        var stopwatch = Stopwatch.StartNew();
        
        _logger.LogInformation("[HEALTH-CHECK-START] Correlation-ID: {CorrelationId}", correlationId);

        var healthResponse = new HealthCheckResponse
        {
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
            HealthChecks = new List<ComponentHealthCheck>()
        };

        try
        {
            // Check Document Intelligence Service
            await CheckDocumentIntelligenceHealthAsync(healthResponse);

            // Check Blob Storage
            await CheckBlobStorageHealthAsync(healthResponse);

            // Check Configuration
            CheckConfigurationHealth(healthResponse);

            // Determine overall status
            DetermineOverallHealth(healthResponse);

            stopwatch.Stop();
            healthResponse.TotalResponseTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation(
                "[HEALTH-CHECK-COMPLETE] Overall-Status: {Status}, Total-Time: {TotalTime}ms, " +
                "Components-Checked: {ComponentCount}, Correlation-ID: {CorrelationId}",
                healthResponse.Status, healthResponse.TotalResponseTimeMs, 
                healthResponse.HealthChecks.Count, correlationId);

            return healthResponse;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "[HEALTH-CHECK-ERROR] Error: {ErrorMessage}, Correlation-ID: {CorrelationId}", 
                ex.Message, correlationId);

            healthResponse.Status = HealthStatus.Unhealthy;
            healthResponse.TotalResponseTimeMs = stopwatch.ElapsedMilliseconds;
            healthResponse.SystemIssues.Add($"Health check failed: {ex.Message}");
            healthResponse.Recommendations.Add("Investigate system logs for detailed error information");

            return healthResponse;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Validate document analysis request parameters
    /// </summary>
    private static void ValidateRequest(DocumentAnalysisUrlRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DocumentUrl))
            throw new ArgumentException("Document URL is required", nameof(request));

        if (!Uri.TryCreate(request.DocumentUrl, UriKind.Absolute, out var uri))
            throw new ArgumentException("Invalid document URL format", nameof(request));

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException("Document URL must use HTTP or HTTPS protocol", nameof(request));

        if (request.ConfidenceThreshold < 0.0 || request.ConfidenceThreshold > 1.0)
            throw new ArgumentException("Confidence threshold must be between 0.0 and 1.0", nameof(request));

        if (string.IsNullOrWhiteSpace(request.ModelId))
            throw new ArgumentException("Model ID is required", nameof(request));
    }

    /// <summary>
    /// Validate file upload request parameters
    /// </summary>
    private static void ValidateFileRequest(DocumentAnalysisFileRequest request)
    {
        if (request.FileContent == null || request.FileContent.Length == 0)
            throw new ArgumentException("File content is required", nameof(request));

        if (string.IsNullOrWhiteSpace(request.Filename))
            throw new ArgumentException("Filename is required", nameof(request));

        if (string.IsNullOrWhiteSpace(request.ContentType))
            throw new ArgumentException("Content type is required", nameof(request));

        if (request.ConfidenceThreshold < 0.0 || request.ConfidenceThreshold > 1.0)
            throw new ArgumentException("Confidence threshold must be between 0.0 and 1.0", nameof(request));

        if (string.IsNullOrWhiteSpace(request.ModelId))
            throw new ArgumentException("Model ID is required", nameof(request));
    }

    /// <summary>
    /// Convert Azure Document Intelligence response to our response model
    /// </summary>
    private async Task<DocumentAnalysisResponse> ConvertToDocumentAnalysisResponseAsync(
        string analysisId,
        AzureDocumentIntelligenceResponse azureResponse,
        object originalRequest,
        string correlationId)
    {
        var response = new DocumentAnalysisResponse
        {
            AnalysisId = analysisId,
            CorrelationId = correlationId,
            AnalysisMetadata = new AnalysisMetadata
            {
                ModelId = azureResponse.ModelId,
                ApiVersion = azureResponse.ApiVersion,
                ConfidenceThreshold = GetConfidenceThreshold(originalRequest),
                PageCount = azureResponse.AnalyzeResult?.Pages?.Count ?? 0
            }
        };

        // Extract serial field if available
        if (azureResponse.AnalyzeResult?.Documents?.Any() == true)
        {
            var document = azureResponse.AnalyzeResult.Documents[0];
            if (document.Fields.TryGetValue("Serial", out var serialField) ||
                document.Fields.TryGetValue("SerialNumber", out serialField))
            {
                var extractionSuccess = azureResponse.Status == "succeeded" && !string.IsNullOrWhiteSpace(serialField.Content);
                var meetsThreshold = serialField.Confidence >= response.AnalysisMetadata.ConfidenceThreshold;
                
                // Determine field extraction status (matching Python logic)
                FieldExtractionStatus fieldStatus;
                if (!extractionSuccess)
                {
                    fieldStatus = FieldExtractionStatus.NotFound;
                }
                else if (meetsThreshold)
                {
                    fieldStatus = FieldExtractionStatus.Extracted;
                }
                else
                {
                    fieldStatus = FieldExtractionStatus.LowConfidence;
                }
                
                response.SerialField = new SerialFieldResult
                {
                    Value = meetsThreshold ? serialField.Content : null, // Only return value if confidence is sufficient (matching Python)
                    Confidence = serialField.Confidence,
                    Status = fieldStatus,
                    ConfidenceAcceptable = meetsThreshold,
                    BoundingRegion = serialField.BoundingRegions.Count > 0 
                        ? ConvertBoundingRegion(serialField.BoundingRegions[0])
                        : null,
                    Spans = serialField.Spans.Select(ConvertTextSpan).ToList()
                };

                // Store document if confidence is below threshold
                if (!response.SerialField.ConfidenceAcceptable && _blobSettings.ENABLE_BLOB_STORAGE)
                {
                    await StoreDocumentForReviewAsync(response, originalRequest, correlationId);
                }
                
                // Determine overall status (matching Python logic)
                if (extractionSuccess && meetsThreshold)
                {
                    response.Status = AnalysisStatus.Succeeded;
                }
                else if (extractionSuccess && !meetsThreshold)
                {
                    response.Status = AnalysisStatus.RequiresReview;
                }
                else
                {
                    response.Status = AnalysisStatus.Failed;
                }
            }
            else
            {
                // No serial field found
                response.Status = AnalysisStatus.Failed;
                response.SerialField = new SerialFieldResult
                {
                    Value = null,
                    Confidence = 0.0,
                    Status = FieldExtractionStatus.NotFound,
                    ConfidenceAcceptable = false
                };
            }
        }
        else
        {
            // No documents found or analysis failed
            response.Status = AnalysisStatus.Failed;
        }

        return response;
    }

    /// <summary>
    /// Store document for manual review when confidence is low
    /// </summary>
    private async Task StoreDocumentForReviewAsync(
        DocumentAnalysisResponse response, 
        object originalRequest, 
        string correlationId)
    {
        try
        {
            byte[] documentBytes;
            string filename;
            string contentType;

            // Get document data based on request type
            switch (originalRequest)
            {
                case DocumentAnalysisUrlRequest urlRequest:
                {
                    // Download document for storage
                    using var httpResponse = await _httpClient.GetAsync(urlRequest.DocumentUrl);
                    documentBytes = await httpResponse.Content.ReadAsByteArrayAsync();
                    filename = Path.GetFileName(urlRequest.DocumentUrl);
                    contentType = httpResponse.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                    break;
                }

                case DocumentAnalysisFileRequest fileRequest:
                    documentBytes = fileRequest.FileContent;
                    filename = fileRequest.Filename;
                    contentType = fileRequest.ContentType;
                    break;

                default:
                    throw new ArgumentException("Unknown request type", nameof(originalRequest));
            }

            var metadata = CreateMetadata(originalRequest, response);
            var storageInfo = await _blobStorageRepository.StoreLowConfidenceDocumentAsync(
                response.AnalysisId!,
                documentBytes,
                filename,
                contentType,
                response.SerialField!,
                metadata,
                correlationId);

            response.StorageInfo = storageInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "[STORAGE-ERROR] Analysis-ID: {AnalysisId}, Error: {ErrorMessage}, " +
                "Correlation-ID: {CorrelationId}", response.AnalysisId, ex.Message, correlationId);

            response.StorageInfo = new StorageInformation
            {
                Stored = false,
                StorageReason = $"Storage failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Get confidence threshold from request object
    /// </summary>
    private static double GetConfidenceThreshold(object request)
    {
        return request switch
        {
            DocumentAnalysisUrlRequest urlRequest => urlRequest.ConfidenceThreshold ?? 0.3,
            DocumentAnalysisFileRequest fileRequest => fileRequest.ConfidenceThreshold ?? 0.3,
            _ => 0.3
        };
    }

    /// <summary>
    /// Create metadata dictionary for blob storage
    /// </summary>
    private static Dictionary<string, string> CreateMetadata(object request, DocumentAnalysisResponse result)
    {
        var metadata = new Dictionary<string, string>
        {
            ["analysis_id"] = result.AnalysisId ?? string.Empty,
            ["correlation_id"] = result.CorrelationId ?? string.Empty,
            ["model_id"] = result.AnalysisMetadata?.ModelId ?? string.Empty,
            ["confidence"] = result.SerialField?.Confidence.ToString("F3") ?? "0.000",
            ["extraction_status"] = result.SerialField?.Status.ToString() ?? "Unknown",
            ["processing_timestamp"] = DateTime.UtcNow.ToString("O")
        };

        switch (request)
        {
            case DocumentAnalysisUrlRequest urlRequest:
                metadata["source_type"] = "url";
                metadata["source_url"] = urlRequest.DocumentUrl;
                break;
            case DocumentAnalysisFileRequest fileRequest:
                metadata["source_type"] = "upload";
                metadata["filename"] = fileRequest.Filename;
                metadata["content_type"] = fileRequest.ContentType;
                break;
        }

        return metadata;
    }

    /// <summary>
    /// Convert Azure bounding region to our model
    /// </summary>
    private static BoundingRegion ConvertBoundingRegion(Models.BoundingRegion azureBoundingRegion)
    {
        return new BoundingRegion
        {
            PageNumber = azureBoundingRegion.PageNumber,
            Polygon = new List<double>(azureBoundingRegion.Polygon)
        };
    }

    /// <summary>
    /// Convert Azure text span to our model
    /// </summary>
    private static TextSpan ConvertTextSpan(Models.TextSpan azureTextSpan)
    {
        return new TextSpan
        {
            Offset = azureTextSpan.Offset,
            Length = azureTextSpan.Length
        };
    }

    /// <summary>
    /// Create error response for failed processing
    /// </summary>
    private DocumentAnalysisResponse CreateErrorResponse(string analysisId, string errorMessage, long processingTimeMs, string correlationId)
    {
        var errorCode = ErrorCode.ProcessingError;

        return new DocumentAnalysisResponse
        {
            AnalysisId = analysisId,
            Status = AnalysisStatus.Failed,
            CorrelationId = correlationId,
            ProcessingTimeMs = processingTimeMs,
            Error = new ErrorResponse
            {
                ErrorCode = errorCode,
                Message = errorMessage,
                CorrelationId = correlationId,
                Timestamp = DateTime.UtcNow
            }
        };
    }

    #region Health Check Methods

    /// <summary>
    /// Check Azure Document Intelligence service health
    /// </summary>
    private async Task CheckDocumentIntelligenceHealthAsync(HealthCheckResponse response)
    {
        const string StatusKey = "status";
        const string HealthyValue = "healthy";
        
        var check = new ComponentHealthCheck
        {
            Component = "DocumentIntelligence"
        };

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var healthResult = await _documentIntelligenceService.HealthCheckAsync();
            stopwatch.Stop();

            check.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            check.Status = healthResult.ContainsKey(StatusKey) && healthResult[StatusKey].ToString() == HealthyValue 
                ? HealthStatus.Healthy : HealthStatus.Unhealthy;
            check.Description = $"Azure Document Intelligence API is {(check.Status == HealthStatus.Healthy ? "operational" : "unavailable")}";
            
            if (check.Status != HealthStatus.Healthy)
            {
                check.Issues.Add("Document Intelligence API is not responding correctly");
                check.Recommendations.Add("Verify Azure Document Intelligence endpoint and API key configuration");
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            check.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            check.Status = HealthStatus.Unhealthy;
            check.Description = "Document Intelligence service check failed";
            check.ErrorDetails = ex.Message;
            check.Issues.Add("Cannot connect to Azure Document Intelligence service");
            check.Recommendations.Add("Check network connectivity and service configuration");
        }

        response.HealthChecks.Add(check);
    }

    /// <summary>
    /// Check blob storage health
    /// </summary>
    private async Task CheckBlobStorageHealthAsync(HealthCheckResponse response)
    {
        const string StatusKey = "status";
        const string HealthyValue = "healthy";
        
        var check = new ComponentHealthCheck
        {
            Component = "BlobStorage"
        };

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var healthResult = await _blobStorageRepository.HealthCheckAsync();
            stopwatch.Stop();

            check.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            check.Status = healthResult.ContainsKey(StatusKey) && healthResult[StatusKey].ToString() == HealthyValue
                ? HealthStatus.Healthy : HealthStatus.Unhealthy;
            check.Description = $"Azure Blob Storage is {(check.Status == HealthStatus.Healthy ? "accessible" : "unavailable")}";
            
            if (check.Status != HealthStatus.Healthy)
            {
                check.Issues.Add("Blob storage connectivity issues");
                check.Recommendations.Add("Verify Azure Storage connection string and container permissions");
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            check.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            check.Status = HealthStatus.Unhealthy;
            check.Description = "Blob storage check failed";
            check.ErrorDetails = ex.Message;
            check.Issues.Add("Cannot connect to Azure Blob Storage");
            check.Recommendations.Add("Check storage connection string and network connectivity");
        }

        response.HealthChecks.Add(check);
    }

    /// <summary>
    /// Check configuration health
    /// </summary>
    private void CheckConfigurationHealth(HealthCheckResponse response)
    {
        var check = new ComponentHealthCheck
        {
            Component = "Configuration",
            ResponseTimeMs = 0
        };

        var issues = new List<string>();

        if (string.IsNullOrWhiteSpace(_intelligenceSettings.DOCUMENT_INTELLIGENCE_ENDPOINT))
            issues.Add("Document Intelligence endpoint not configured");

        if (string.IsNullOrWhiteSpace(_intelligenceSettings.DOCUMENT_INTELLIGENCE_KEY))
            issues.Add("Document Intelligence API key not configured");

        if (_blobSettings.ENABLE_BLOB_STORAGE && string.IsNullOrWhiteSpace(_blobSettings.AZURE_STORAGE_CONNECTION_STRING))
            issues.Add("Blob storage enabled but connection string not configured");

        if (_processingSettings.CONFIDENCE_THRESHOLD < 0.0 || _processingSettings.CONFIDENCE_THRESHOLD > 1.0)
            issues.Add("Invalid confidence threshold configuration");

        check.Status = issues.Count == 0 ? HealthStatus.Healthy : HealthStatus.Unhealthy;
        check.Description = $"Configuration validation {(check.Status == HealthStatus.Healthy ? "passed" : "failed")}";
        check.Issues = issues;

        if (issues.Count > 0)
        {
            check.Recommendations.Add("Review application configuration and environment variables");
            check.Recommendations.Add("Ensure all required settings are properly configured");
        }

        response.HealthChecks.Add(check);
    }

    /// <summary>
    /// Determine overall health status based on component checks
    /// </summary>
    private static void DetermineOverallHealth(HealthCheckResponse response)
    {
        var healthyCount = response.HealthChecks.Count(c => c.Status == HealthStatus.Healthy);
        var degradedCount = response.HealthChecks.Count(c => c.Status == HealthStatus.Degraded);
        var unhealthyCount = response.HealthChecks.Count(c => c.Status == HealthStatus.Unhealthy);

        if (unhealthyCount > 0)
        {
            response.Status = HealthStatus.Unhealthy;
            response.SystemIssues.Add($"{unhealthyCount} critical component(s) are unhealthy");
            response.Recommendations.Add("Address critical component issues before proceeding with operations");
        }
        else if (degradedCount > 0)
        {
            response.Status = HealthStatus.Degraded;
            response.SystemIssues.Add($"{degradedCount} component(s) have performance issues");
            response.Recommendations.Add("Monitor degraded components and consider optimization");
        }
        else
        {
            response.Status = HealthStatus.Healthy;
        }

        response.PerformanceMetrics = new PerformanceMetrics
        {
            AverageProcessingTimeMs = response.HealthChecks.Count > 0 ? response.HealthChecks.Average(c => c.ResponseTimeMs) : 0,
            SuccessRatePercentage = response.HealthChecks.Count > 0 ? (double)healthyCount / response.HealthChecks.Count * 100 : 0
        };
    }

    #endregion

    #endregion
}
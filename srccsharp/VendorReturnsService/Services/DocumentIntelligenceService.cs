using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;
using VendorReturnsService.Configuration;
using VendorReturnsService.Models;

namespace VendorReturnsService.Services;

/// <summary>
/// Service for Document Intelligence operations - calls the Document Intelligence API wrapper.
/// </summary>
public class DocumentIntelligenceService : IDocumentIntelligenceService
{
    private readonly DocumentIntelligenceSettings _settings;
    private readonly ILogger<DocumentIntelligenceService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HttpClient _httpClient;

    public DocumentIntelligenceService(
        DocumentIntelligenceSettings settings,
        ILogger<DocumentIntelligenceService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _settings = settings;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _httpClient = _httpClientFactory.CreateClient("DocumentIntelligence");
        
        ConfigureHttpClient();
    }

    /// <summary>
    /// Analyzes a document image and extracts fields using the Document Intelligence API.
    /// </summary>
    public async Task<DocumentIntelligenceResult> AnalyzeDocumentAsync(
        byte[] imageData,
        DocumentIntelligenceConfig config,
        string correlationId)
    {
        try
        {
            _logger.LogInformation(
                "[{CorrelationId}] Calling Document Intelligence API for {ServiceName}",
                correlationId, config.ServiceName);

            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(imageData);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
            content.Add(fileContent, "file", $"{config.ServiceName}.jpg");

            // Add correlation ID header
            if (_httpClient.DefaultRequestHeaders.Contains("X-Correlation-ID"))
            {
                _httpClient.DefaultRequestHeaders.Remove("X-Correlation-ID");
            }
            _httpClient.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);

            var endpoint = $"{_settings.ApiEndpoint.TrimEnd('/')}/process-document";
            var response = await _httpClient.PostAsync(endpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "[{CorrelationId}] Document Intelligence API error - StatusCode: {StatusCode}, Error: {ErrorContent}",
                    correlationId, response.StatusCode, errorContent);

                return new DocumentIntelligenceResult
                {
                    Success = false,
                    ErrorMessage = $"API returned {response.StatusCode}: {errorContent}"
                };
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<DocumentAnalysisApiResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (apiResponse == null || apiResponse.Status != "success")
            {
                _logger.LogWarning(
                    "[{CorrelationId}] Document Intelligence API returned non-success status: {Status}",
                    correlationId, apiResponse?.Status);

                return new DocumentIntelligenceResult
                {
                    Success = false,
                    ErrorMessage = apiResponse?.Message ?? "Unknown error from API"
                };
            }

            // Map API response to our result format
            var extractedFields = new Dictionary<string, string>();
            double? confidence = null;

            if (apiResponse.SerialField != null && !string.IsNullOrEmpty(apiResponse.SerialField.Value))
            {
                extractedFields["SerialNumber"] = apiResponse.SerialField.Value;
                confidence = apiResponse.SerialField.Confidence;
            }

            if (apiResponse.PieceField != null && !string.IsNullOrEmpty(apiResponse.PieceField.Value))
            {
                extractedFields["PieceNumber"] = apiResponse.PieceField.Value;
                if (!confidence.HasValue)
                    confidence = apiResponse.PieceField.Confidence;
            }

            _logger.LogInformation(
                "[{CorrelationId}] Document Intelligence API success - Extracted {Count} fields with confidence {Confidence}",
                correlationId, extractedFields.Count, confidence);

            return new DocumentIntelligenceResult
            {
                Success = true,
                ExtractedFields = extractedFields,
                ConfidenceScore = confidence
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[{CorrelationId}] Error calling Document Intelligence API for {ServiceName}",
                correlationId, config.ServiceName);

            return new DocumentIntelligenceResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
        finally
        {
            _httpClient.DefaultRequestHeaders.Remove("X-Correlation-ID");
        }
    }

    private void ConfigureHttpClient()
    {
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
        _httpClient.BaseAddress = new Uri(_settings.ApiEndpoint);
        
        if (!string.IsNullOrEmpty(_settings.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("x-functions-key", _settings.ApiKey);
        }
    }
}

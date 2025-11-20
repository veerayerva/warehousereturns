using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using WarehouseReturns.ReturnsProcessing.Configuration;
using WarehouseReturns.ReturnsProcessing.Models;

namespace WarehouseReturns.ReturnsProcessing.Services;

/// <summary>
/// Document Intelligence API service interface
/// </summary>
public interface IDocumentIntelligenceApiService
{
    Task<DocumentAnalysisResponse?> AnalyzeDocumentFromBytesAsync(byte[] documentData, string fileName, string contentType, decimal confidenceThreshold, string correlationId);
    Task<bool> TestConnectionAsync();
}

/// <summary>
/// Piece Info API service interface  
/// </summary>
public interface IPieceInfoApiService
{
    Task<PieceInfoResponse?> GetPieceInfoAsync(string serial, string correlationId);
    Task<bool> TestConnectionAsync();
}

/// <summary>
/// Document Intelligence API service implementation
/// </summary>
public class DocumentIntelligenceApiService : IDocumentIntelligenceApiService
{
    private readonly DocumentIntelligenceApiSettings _settings;
    private readonly ILogger<DocumentIntelligenceApiService> _logger;
    private readonly HttpClient _httpClient;

    public DocumentIntelligenceApiService(
        IOptions<DocumentIntelligenceApiSettings> settings,
        ILogger<DocumentIntelligenceApiService> logger,
        HttpClient httpClient)
    {
        _settings = settings.Value;
        _logger = logger;
        _httpClient = httpClient;
        
        ConfigureHttpClient();
    }

    /// <summary>
    /// Analyze document from byte data using Document Intelligence API
    /// </summary>
    public async Task<DocumentAnalysisResponse?> AnalyzeDocumentFromBytesAsync(
        byte[] documentData, 
        string fileName, 
        string contentType, 
        decimal confidenceThreshold, 
        string correlationId)
    {
        try
        {
            _logger.LogInformation(
                "[DOC-INTEL-API] Starting analysis - FileName: {FileName}, Size: {Size} bytes, Correlation: {CorrelationId}",
                fileName, documentData.Length, correlationId);

            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(documentData);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            content.Add(fileContent, "file", fileName);

            // Add correlation ID header if available
            if (!string.IsNullOrEmpty(correlationId))
            {
                _httpClient.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);
            }

            var endpoint = $"{_settings.DOCUMENT_INTELLIGENCE_ENDPOINT.TrimEnd('/')}/process-document";
            var response = await _httpClient.PostAsync(endpoint, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "[DOC-INTEL-API] HTTP error - StatusCode: {StatusCode}, Content: {ErrorContent}, Correlation: {CorrelationId}",
                    response.StatusCode, errorContent, correlationId);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<DocumentAnalysisResponse>(responseContent, GetJsonOptions());

            _logger.LogInformation(
                "[DOC-INTEL-API] Analysis success - Serial: {Serial}, Confidence: {Confidence}, Status: {Status}, Correlation: {CorrelationId}",
                result?.SerialField?.Value, result?.SerialField?.Confidence, result?.Status, correlationId);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "[DOC-INTEL-API] HTTP request error - FileName: {FileName}, Correlation: {CorrelationId}",
                fileName, correlationId);
            return null;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex,
                "[DOC-INTEL-API] Request timeout - FileName: {FileName}, Timeout: {Timeout}s, Correlation: {CorrelationId}",
                fileName, _settings.DOCUMENT_INTELLIGENCE_TIMEOUT_SECONDS, correlationId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[DOC-INTEL-API] Unexpected error - FileName: {FileName}, Correlation: {CorrelationId}",
                fileName, correlationId);
            return null;
        }
        finally
        {
            // Clean up headers
            _httpClient.DefaultRequestHeaders.Remove("X-Correlation-ID");
        }
    }

    /// <summary>
    /// Test connection to Document Intelligence API
    /// </summary>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var endpoint = $"{_settings.DOCUMENT_INTELLIGENCE_ENDPOINT.TrimEnd('/')}/health";
            var response = await _httpClient.GetAsync(endpoint);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private void ConfigureHttpClient()
    {
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.DOCUMENT_INTELLIGENCE_TIMEOUT_SECONDS);
        
        if (!string.IsNullOrEmpty(_settings.DOCUMENT_INTELLIGENCE_API_KEY))
        {
            _httpClient.DefaultRequestHeaders.Add("x-functions-key", _settings.DOCUMENT_INTELLIGENCE_API_KEY);
        }
    }

    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}

/// <summary>
/// Piece Info API service implementation
/// </summary>
public class PieceInfoApiService : IPieceInfoApiService
{
    private readonly PieceInfoApiSettings _settings;
    private readonly ILogger<PieceInfoApiService> _logger;
    private readonly HttpClient _httpClient;

    public PieceInfoApiService(
        IOptions<PieceInfoApiSettings> settings,
        ILogger<PieceInfoApiService> logger,
        HttpClient httpClient)
    {
        _settings = settings.Value;
        _logger = logger;
        _httpClient = httpClient;
        
        ConfigureHttpClient();
    }

    /// <summary>
    /// Get piece information by serial number
    /// </summary>
    public async Task<PieceInfoResponse?> GetPieceInfoAsync(string serial, string correlationId)
    {
        try
        {
            _logger.LogInformation(
                "[PIECE-INFO-API] Starting lookup - Serial: {Serial}, Correlation: {CorrelationId}",
                serial, correlationId);

            // Add correlation ID header if available
            if (!string.IsNullOrEmpty(correlationId))
            {
                _httpClient.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);
            }

            var endpoint = $"{_settings.PIECE_INFO_API_ENDPOINT.TrimEnd('/')}/pieces/{Uri.EscapeDataString(serial)}";
            var response = await _httpClient.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogWarning(
                        "[PIECE-INFO-API] Piece not found - Serial: {Serial}, Correlation: {CorrelationId}",
                        serial, correlationId);
                    return null;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "[PIECE-INFO-API] HTTP error - StatusCode: {StatusCode}, Content: {ErrorContent}, Correlation: {CorrelationId}",
                    response.StatusCode, errorContent, correlationId);
                return null;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<PieceInfoResponse>(responseContent, GetJsonOptions());

            _logger.LogInformation(
                "[PIECE-INFO-API] Lookup success - Serial: {Serial}, SKU: {SKU}, Family: {Family}, Correlation: {CorrelationId}",
                serial, result?.SKU, result?.Family, correlationId);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "[PIECE-INFO-API] HTTP request error - Serial: {Serial}, Correlation: {CorrelationId}",
                serial, correlationId);
            return null;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex,
                "[PIECE-INFO-API] Request timeout - Serial: {Serial}, Timeout: {Timeout}s, Correlation: {CorrelationId}",
                serial, _settings.PIECE_INFO_API_TIMEOUT_SECONDS, correlationId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[PIECE-INFO-API] Unexpected error - Serial: {Serial}, Correlation: {CorrelationId}",
                serial, correlationId);
            return null;
        }
        finally
        {
            // Clean up headers
            _httpClient.DefaultRequestHeaders.Remove("X-Correlation-ID");
        }
    }

    /// <summary>
    /// Test connection to Piece Info API
    /// </summary>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var endpoint = $"{_settings.PIECE_INFO_API_ENDPOINT.TrimEnd('/')}/health";
            var response = await _httpClient.GetAsync(endpoint);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private void ConfigureHttpClient()
    {
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.PIECE_INFO_API_TIMEOUT_SECONDS);
        
        if (!string.IsNullOrEmpty(_settings.PIECE_INFO_API_KEY))
        {
            _httpClient.DefaultRequestHeaders.Add("x-functions-key", _settings.PIECE_INFO_API_KEY);
        }
    }

    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}
using Microsoft.Extensions.Logging;
using System.Text.Json;
using VendorReturnsService.Configuration;
using VendorReturnsService.Models;

namespace VendorReturnsService.Services;

/// <summary>
/// Service for PieceInfo API operations.
/// </summary>
public class PieceInfoService : IPieceInfoService
{
    private readonly PieceInfoSettings _settings;
    private readonly ILogger<PieceInfoService> _logger;
    private readonly HttpClient _httpClient;

    public PieceInfoService(
        PieceInfoSettings settings,
        ILogger<PieceInfoService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _settings = settings;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("PieceInfo");
    }

    /// <summary>
    /// Retrieves piece information by piece number.
    /// </summary>
    public async Task<PieceInfoResponse> GetPieceInfoAsync(string pieceNumber, string correlationId)
    {
        try
        {
            _logger.LogInformation(
                "[{CorrelationId}] Fetching piece info for PieceNumber={PieceNumber}",
                correlationId, pieceNumber);

            var requestUrl = $"{_settings.ApiBaseUrl}/api/piece/{pieceNumber}";

            var request = new HttpRequestMessage(HttpMethod.Get, requestUrl);
            
            // Add API key if configured
            if (!string.IsNullOrEmpty(_settings.ApiKey))
            {
                request.Headers.Add("X-API-Key", _settings.ApiKey);
            }

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var pieceInfoData = JsonSerializer.Deserialize<PieceInfoData>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (pieceInfoData == null)
            {
                _logger.LogWarning(
                    "[{CorrelationId}] PieceInfo API returned null data for PieceNumber={PieceNumber}",
                    correlationId, pieceNumber);

                return new PieceInfoResponse
                {
                    Success = false,
                    ErrorMessage = "API returned null data"
                };
            }

            _logger.LogInformation(
                "[{CorrelationId}] Successfully retrieved piece info: SKU={SkuNumber}, Vendor={Vendor}",
                correlationId, pieceInfoData.SkuNumber, pieceInfoData.Vendor);

            return new PieceInfoResponse
            {
                Success = true,
                Data = pieceInfoData
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "[{CorrelationId}] HTTP error fetching piece info for PieceNumber={PieceNumber}",
                correlationId, pieceNumber);

            return new PieceInfoResponse
            {
                Success = false,
                ErrorMessage = $"HTTP error: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[{CorrelationId}] Error fetching piece info for PieceNumber={PieceNumber}",
                correlationId, pieceNumber);

            return new PieceInfoResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}

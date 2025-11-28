using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using VendorReturnsService.Configuration;
using VendorReturnsService.Models;
using VendorReturnsService.Services;

namespace VendorReturnsService.Utilities;

/// <summary>
/// Handles retrieving images from different sources (Attachment or SharePoint Drive).
/// </summary>
public class ImageSourceHandler
{
    private readonly SharePointSettings _settings;
    private readonly ISharePointService _sharePointService;
    private readonly ILogger<ImageSourceHandler> _logger;

    public ImageSourceHandler(
        SharePointSettings settings,
        ISharePointService sharePointService,
        ILogger<ImageSourceHandler> logger)
    {
        _settings = settings;
        _sharePointService = sharePointService;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves piece image data based on configured source.
    /// </summary>
    public async Task<byte[]?> GetPieceImageAsync(VendorReturnItem item, string correlationId)
    {
        if (_settings.ImageSource == "Attachment")
        {
            return await GetImageFromAttachmentAsync(
                item.Id,
                item.PieceImage,
                "PieceImage",
                correlationId);
        }
        else if (_settings.ImageSource == "SharePointDrive")
        {
            return await GetImageFromDriveAsync(
                item.PieceImageUrl,
                "PieceImage",
                correlationId);
        }
        else
        {
            _logger.LogError(
                "[{CorrelationId}] Invalid ImageSource configuration: {ImageSource}",
                correlationId, _settings.ImageSource);
            return null;
        }
    }

    /// <summary>
    /// Retrieves serial image data based on configured source.
    /// </summary>
    public async Task<byte[]?> GetSerialImageAsync(VendorReturnItem item, string correlationId)
    {
        if (_settings.ImageSource == "Attachment")
        {
            return await GetImageFromAttachmentAsync(
                item.Id,
                item.SerialImage,
                "SerialImage",
                correlationId);
        }
        else if (_settings.ImageSource == "SharePointDrive")
        {
            return await GetImageFromDriveAsync(
                item.SerialImageUrl,
                "SerialImage",
                correlationId);
        }
        else
        {
            _logger.LogError(
                "[{CorrelationId}] Invalid ImageSource configuration: {ImageSource}",
                correlationId, _settings.ImageSource);
            return null;
        }
    }

    /// <summary>
    /// Retrieves image from SharePoint attachment field.
    /// </summary>
    private async Task<byte[]?> GetImageFromAttachmentAsync(
        string listItemId,
        string? attachmentFieldValue,
        string imageName,
        string correlationId)
    {
        if (string.IsNullOrEmpty(attachmentFieldValue))
        {
            _logger.LogWarning(
                "[{CorrelationId}] {ImageName} attachment field is empty for item {ListItemId}",
                correlationId, imageName, listItemId);
            return null;
        }

        try
        {
            // Parse JSON to extract filename
            var json = JToken.Parse(attachmentFieldValue);
            string? fileName = null;

            if (json is JArray array && array.Count > 0)
            {
                fileName = array[0]["fileName"]?.ToString();
            }
            else if (json is JObject obj)
            {
                fileName = obj["fileName"]?.ToString();
            }

            if (string.IsNullOrEmpty(fileName))
            {
                _logger.LogWarning(
                    "[{CorrelationId}] Could not extract filename from {ImageName} field for item {ListItemId}",
                    correlationId, imageName, listItemId);
                return null;
            }

            _logger.LogInformation(
                "[{CorrelationId}] Downloading {ImageName} attachment: {FileName}",
                correlationId, imageName, fileName);

            return await _sharePointService.DownloadAttachmentAsync(listItemId, fileName, correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[{CorrelationId}] Error retrieving {ImageName} attachment for item {ListItemId}",
                correlationId, imageName, listItemId);
            return null;
        }
    }

    /// <summary>
    /// Retrieves image from SharePoint Drive URL.
    /// </summary>
    private async Task<byte[]?> GetImageFromDriveAsync(
        string? imageUrl,
        string imageName,
        string correlationId)
    {
        if (string.IsNullOrEmpty(imageUrl))
        {
            _logger.LogWarning(
                "[{CorrelationId}] {ImageName} URL is empty",
                correlationId, imageName);
            return null;
        }

        _logger.LogInformation(
            "[{CorrelationId}] Downloading {ImageName} from Drive: {ImageUrl}",
            correlationId, imageName, imageUrl);

        return await _sharePointService.DownloadImageFromDriveAsync(imageUrl, correlationId);
    }
}

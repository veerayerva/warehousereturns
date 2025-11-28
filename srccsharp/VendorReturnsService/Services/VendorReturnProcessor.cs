using Microsoft.Extensions.Logging;
using VendorReturnsService.Configuration;
using VendorReturnsService.Models;
using VendorReturnsService.Utilities;

namespace VendorReturnsService.Services;

/// <summary>
/// Orchestrates the complete vendor return processing workflow.
/// </summary>
public class VendorReturnProcessor : IVendorReturnProcessor
{
    private readonly ISharePointService _sharePointService;
    private readonly IDocumentIntelligenceService _documentIntelligenceService;
    private readonly IPieceInfoService _pieceInfoService;
    private readonly ImageSourceHandler _imageSourceHandler;
    private readonly ILogger<VendorReturnProcessor> _logger;

    public VendorReturnProcessor(
        ISharePointService sharePointService,
        IDocumentIntelligenceService documentIntelligenceService,
        IPieceInfoService pieceInfoService,
        ImageSourceHandler imageSourceHandler,
        ILogger<VendorReturnProcessor> logger)
    {
        _sharePointService = sharePointService;
        _documentIntelligenceService = documentIntelligenceService;
        _pieceInfoService = pieceInfoService;
        _imageSourceHandler = imageSourceHandler;
        _logger = logger;
    }

    /// <summary>
    /// Processes a single vendor return item through the complete workflow.
    /// </summary>
    public async Task<ProcessingResult> ProcessReturnItemAsync(string listItemId, string correlationId)
    {
        var result = new ProcessingResult
        {
            ListItemId = listItemId,
            ProcessingDetails = new Dictionary<string, string>()
        };

        try
        {
            _logger.LogInformation(
                "[{CorrelationId}] Starting processing for item {ListItemId}",
                correlationId, listItemId);

            // Step 1: Retrieve SharePoint item
            var item = await _sharePointService.GetItemByIdAsync(listItemId, correlationId);
            if (item == null)
            {
                result.Success = false;
                result.ErrorMessage = "Item not found in SharePoint";
                result.ProcessStatus = "Failed";
                result.ProcessingDetails["Error"] = "Item not found";
                
                _logger.LogError("[{CorrelationId}] Item {ListItemId} not found", correlationId, listItemId);
                return result;
            }

            result.ProcessingDetails["Status"] = item.Status;

            // Step 2: Get piece image (CRITICAL - must succeed)
            var pieceImageData = await _imageSourceHandler.GetPieceImageAsync(item, correlationId);
            if (pieceImageData == null)
            {
                result.Success = false;
                result.ErrorMessage = "Failed to retrieve piece image";
                result.ProcessStatus = "Failed";
                result.ProcessingDetails["Error"] = "Piece image not found or failed to download";

                await UpdateProcessStatusAsync(listItemId, "Failed", correlationId);
                
                _logger.LogError(
                    "[{CorrelationId}] CRITICAL: Failed to retrieve piece image for item {ListItemId}",
                    correlationId, listItemId);
                return result;
            }

            result.ProcessingDetails["PieceImageSize"] = $"{pieceImageData.Length} bytes";

            // Step 3: Process piece image with Document Intelligence (CRITICAL)
            var pieceImageConfig = new DocumentIntelligenceConfig
            {
                ServiceName = "PieceImage"
            };

            var pieceImageResult = await _documentIntelligenceService.AnalyzeDocumentAsync(
                pieceImageData,
                pieceImageConfig,
                correlationId);

            if (!pieceImageResult.Success)
            {
                result.Success = false;
                result.ErrorMessage = $"Piece image analysis failed: {pieceImageResult.ErrorMessage}";
                result.ProcessStatus = "Failed";
                result.ProcessingDetails["Error"] = "Piece image Document Intelligence failed";

                await UpdateProcessStatusAsync(listItemId, "Failed", correlationId);
                
                _logger.LogError(
                    "[{CorrelationId}] CRITICAL: Piece image analysis failed for item {ListItemId}: {Error}",
                    correlationId, listItemId, pieceImageResult.ErrorMessage);
                return result;
            }

            // Extract PieceNumber (CRITICAL)
            if (!pieceImageResult.ExtractedFields.TryGetValue("PieceNumber", out var pieceNumber) ||
                string.IsNullOrEmpty(pieceNumber))
            {
                result.Success = false;
                result.ErrorMessage = "PieceNumber not extracted from piece image";
                result.ProcessStatus = "Failed";
                result.ProcessingDetails["Error"] = "PieceNumber field missing";

                await UpdateProcessStatusAsync(listItemId, "Failed", correlationId);
                
                _logger.LogError(
                    "[{CorrelationId}] CRITICAL: PieceNumber not found in piece image for item {ListItemId}",
                    correlationId, listItemId);
                return result;
            }

            result.PieceNumber = pieceNumber;
            result.ProcessingDetails["PieceNumber"] = pieceNumber;
            result.ProcessingDetails["PieceImageConfidence"] = pieceImageResult.ConfidenceScore?.ToString() ?? "N/A";

            _logger.LogInformation(
                "[{CorrelationId}] Extracted PieceNumber={PieceNumber} for item {ListItemId}",
                correlationId, pieceNumber, listItemId);

            // Step 4: Get serial image (optional - failure logged but not critical)
            string? serialNumber = null;
            var serialImageData = await _imageSourceHandler.GetSerialImageAsync(item, correlationId);
            
            if (serialImageData != null)
            {
                result.ProcessingDetails["SerialImageSize"] = $"{serialImageData.Length} bytes";

                var serialImageConfig = new DocumentIntelligenceConfig
                {
                    ServiceName = "SerialImage"
                };

                var serialImageResult = await _documentIntelligenceService.AnalyzeDocumentAsync(
                    serialImageData,
                    serialImageConfig,
                    correlationId);

                if (serialImageResult.Success &&
                    serialImageResult.ExtractedFields.TryGetValue("SerialNumber", out serialNumber))
                {
                    result.SerialNumber = serialNumber;
                    result.ProcessingDetails["SerialNumber"] = serialNumber;
                    result.ProcessingDetails["SerialImageConfidence"] = serialImageResult.ConfidenceScore?.ToString() ?? "N/A";

                    _logger.LogInformation(
                        "[{CorrelationId}] Extracted SerialNumber={SerialNumber} for item {ListItemId}",
                        correlationId, serialNumber, listItemId);
                }
                else
                {
                    _logger.LogWarning(
                        "[{CorrelationId}] Serial image analysis failed or SerialNumber not found for item {ListItemId}",
                        correlationId, listItemId);
                    result.ProcessingDetails["SerialImageError"] = serialImageResult.ErrorMessage ?? "SerialNumber not extracted";
                }
            }
            else
            {
                _logger.LogWarning(
                    "[{CorrelationId}] Serial image not available for item {ListItemId}",
                    correlationId, listItemId);
                result.ProcessingDetails["SerialImageError"] = "Image not found";
            }

            // Step 5: Call PieceInfo API to get vendor/location data
            var pieceInfoResponse = await _pieceInfoService.GetPieceInfoAsync(pieceNumber, correlationId);
            
            if (!pieceInfoResponse.Success || pieceInfoResponse.Data == null)
            {
                _logger.LogWarning(
                    "[{CorrelationId}] PieceInfo API failed for PieceNumber={PieceNumber}, item {ListItemId}: {Error}",
                    correlationId, pieceNumber, listItemId, pieceInfoResponse.ErrorMessage);
                result.ProcessingDetails["PieceInfoError"] = pieceInfoResponse.ErrorMessage ?? "Unknown error";
            }

            // Step 6: Update SharePoint with all extracted data
            var updates = new SharePointUpdateModel
            {
                PieceNumber = pieceNumber,
                SerialNumber = serialNumber,
                ProcessStatus = "Completed"
            };

            // Add PieceInfo data if available
            if (pieceInfoResponse.Success && pieceInfoResponse.Data != null)
            {
                updates.SkuNumber = pieceInfoResponse.Data.SkuNumber;
                updates.WarehouseLocation = pieceInfoResponse.Data.WarehouseLocation;
                updates.Vendor = pieceInfoResponse.Data.Vendor;
                updates.Family = pieceInfoResponse.Data.Family;
                updates.ModelNumber = pieceInfoResponse.Data.ModelNumber;
                updates.RackLocation = pieceInfoResponse.Data.RackLocation;

                result.ProcessingDetails["SkuNumber"] = updates.SkuNumber ?? "N/A";
                result.ProcessingDetails["Vendor"] = updates.Vendor ?? "N/A";
            }

            // Only update Status to R2 if everything succeeded
            if (pieceInfoResponse.Success)
            {
                updates.Status = "R2";
                result.ProcessingDetails["StatusUpdated"] = "R2";
            }
            else
            {
                result.ProcessingDetails["StatusUpdated"] = "R1 (PieceInfo failed)";
            }

            var updateSuccess = await _sharePointService.UpdateItemAsync(listItemId, updates, correlationId);
            
            if (!updateSuccess)
            {
                result.Success = false;
                result.ErrorMessage = "Failed to update SharePoint item";
                result.ProcessStatus = "Failed";
                result.ProcessingDetails["Error"] = "SharePoint update failed";

                _logger.LogError(
                    "[{CorrelationId}] Failed to update SharePoint for item {ListItemId}",
                    correlationId, listItemId);
                return result;
            }

            // Success
            result.Success = true;
            result.ProcessStatus = "Completed";

            _logger.LogInformation(
                "[{CorrelationId}] Successfully processed item {ListItemId}: PieceNumber={PieceNumber}, Status={Status}",
                correlationId, listItemId, pieceNumber, updates.Status);

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Unexpected error: {ex.Message}";
            result.ProcessStatus = "Failed";
            result.ProcessingDetails["Exception"] = ex.ToString();

            await UpdateProcessStatusAsync(listItemId, "Failed", correlationId);

            _logger.LogError(ex,
                "[{CorrelationId}] Unexpected error processing item {ListItemId}",
                correlationId, listItemId);

            return result;
        }
    }

    /// <summary>
    /// Updates the ProcessStatus field in SharePoint.
    /// </summary>
    private async Task UpdateProcessStatusAsync(string listItemId, string processStatus, string correlationId)
    {
        try
        {
            await _sharePointService.UpdateItemAsync(
                listItemId,
                new SharePointUpdateModel { ProcessStatus = processStatus },
                correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[{CorrelationId}] Failed to update ProcessStatus for item {ListItemId}",
                correlationId, listItemId);
        }
    }
}

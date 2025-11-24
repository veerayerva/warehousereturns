using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WarehouseReturns.ReturnsProcessing.Configuration;
using WarehouseReturns.ReturnsProcessing.Models;

namespace WarehouseReturns.ReturnsProcessing.Services;

/// <summary>
/// Returns processing service interface
/// </summary>
public interface IReturnsProcessingService
{
    Task<ProcessingResult> ProcessReturnItemAsync(string listItemId, string correlationId);
    Task<bool> ValidateConfigurationAsync();
}

/// <summary>
/// Returns processing service implementation
/// Orchestrates the complete workflow for processing SharePoint return items
/// </summary>
public class ReturnsProcessingService : IReturnsProcessingService
{
    private readonly ISharePointService _sharePointService;
    private readonly IDocumentIntelligenceApiService _documentIntelligenceService;
    private readonly IPieceInfoApiService _pieceInfoService;
    private readonly ProcessingSettings _processingSettings;
    private readonly ILogger<ReturnsProcessingService> _logger;

    public ReturnsProcessingService(
        ISharePointService sharePointService,
        IDocumentIntelligenceApiService documentIntelligenceService,
        IPieceInfoApiService pieceInfoService,
        IOptions<ProcessingSettings> processingSettings,
        ILogger<ReturnsProcessingService> logger)
    {
        _sharePointService = sharePointService;
        _documentIntelligenceService = documentIntelligenceService;
        _pieceInfoService = pieceInfoService;
        _processingSettings = processingSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Process a return item through the complete workflow
    /// </summary>
    public async Task<ProcessingResult> ProcessReturnItemAsync(string listItemId, string correlationId)
    {
        var context = new ProcessingContext
        {
            ListItemId = listItemId,
            CorrelationId = correlationId
        };

        var result = new ProcessingResult
        {
            ListItemId = listItemId,
            CorrelationId = correlationId,
            Status = "Processing"
        };

        try
        {
            _logger.LogInformation(
                "[RETURNS-PROCESSING] Starting workflow - ItemId: {ItemId}, Correlation: {CorrelationId}",
                listItemId, correlationId);

            context.AddStep("Processing started");

            // Discovery: Get all available SharePoint lists first
            context.AddStep("Starting SharePoint list discovery");
            var availableLists = await _sharePointService.DiscoverAvailableListsAsync(correlationId);
            context.AddStep($"SharePoint list discovery completed - Found {availableLists.Count} lists");

            // Step 1: Get SharePoint list item
            context.AddStep("Fetching SharePoint list item");
            var qcItem = await _sharePointService.GetListItemAsync(listItemId, correlationId);

            if (qcItem == null)
            {
                var errorMsg = "SharePoint list item not found";
                context.AddError(errorMsg);
                result.Status = "Failed";
                result.ErrorMessage = errorMsg;
                return result;
            }

            context.AddStep($"Retrieved list item: {qcItem.Title}");

            // Step 2: Download image from SerialImageLink or PieceImage
            string imageSource = "unknown";
            if (!string.IsNullOrWhiteSpace(qcItem.SerialImageLink?.Url))
            {
                // Priority 1: Use SerialImageLink.Url
                imageSource = "SerialImageLink.Url";
                context.ImageFileName = Path.GetFileName(new Uri(qcItem.SerialImageLink.Url).LocalPath);
                context.AddStep($"Attempting to download from SerialImageLink: {qcItem.SerialImageLink.Url}");
                
                var (imageData, contentType) = await _sharePointService.DownloadImageFromSharePointUrlAsync(
                    qcItem.SerialImageLink.Url,
                    correlationId);
                
                context.ImageData = imageData;
                context.ImageContentType = contentType ?? "application/octet-stream";
            }
            else if (!string.IsNullOrWhiteSpace(qcItem.PieceImage))
            {
                // Priority 2: Fallback to PieceImage attachment
                imageSource = "PieceImage attachment";
                context.ImageFileName = qcItem.PieceImage;
                context.AddStep($"Attempting to download from PieceImage attachment: {qcItem.PieceImage}");
                context.ImageData = await _sharePointService.GetAttachmentAsync(
                    listItemId,
                    qcItem.PieceImage,
                    correlationId);
                // For attachments, detect content type from filename extension
                context.ImageContentType = GetContentTypeFromFileName(context.ImageFileName);
            }
            else
            {
                var errorMsg = "No image source available (SerialImageLink.Url or PieceImage required)";
                context.AddError(errorMsg);
                result.Status = "Failed";
                result.ErrorMessage = errorMsg;
                return result;
            }

            if (context.ImageData == null || context.ImageData.Length == 0)
            {
                var errorMsg = $"Failed to download image from {imageSource}";
                context.AddError(errorMsg);
                result.Status = "Failed";
                result.ErrorMessage = errorMsg;
                return result;
            }

            context.AddStep($"Downloaded image from {imageSource}: {context.ImageData.Length} bytes, Content-Type: {context.ImageContentType}");

            // Step 3: Validate file size and type
            if (context.ImageData.Length > _processingSettings.MAX_FILE_SIZE_MB * 1024 * 1024)
            {
                var errorMsg = $"Image file too large: {context.ImageData.Length / (1024 * 1024)}MB > {_processingSettings.MAX_FILE_SIZE_MB}MB";
                context.AddError(errorMsg);
                result.Status = "Failed";
                result.ErrorMessage = errorMsg;
                return result;
            }

            if (!_processingSettings.IsFileTypeSupported(context.ImageFileName))
            {
                var errorMsg = $"Unsupported file type: {Path.GetExtension(context.ImageFileName)}";
                context.AddError(errorMsg);
                result.Status = "Failed";
                result.ErrorMessage = errorMsg;
                return result;
            }

            context.AddStep("Image validation passed");

            // Step 4: Analyze document with Document Intelligence
            context.AddStep("Starting document intelligence analysis");
            context.DocumentResult = await _documentIntelligenceService.AnalyzeDocumentFromBytesAsync(
                context.ImageData,
                context.ImageFileName,
                context.ImageContentType,
                _processingSettings.CONFIDENCE_THRESHOLD,
                correlationId);

            if (context.DocumentResult == null)
            {
                var errorMsg = "Document Intelligence analysis failed";
                context.AddError(errorMsg);
                result.Status = "Failed";
                result.ErrorMessage = errorMsg;
                return result;
            }

            var serial = context.DocumentResult.SerialField?.Value;
            var documentConfidence = context.DocumentResult.SerialField?.Confidence ?? 0m;
            
            context.AddStep($"Document analysis completed - Serial: {serial}, Confidence: {documentConfidence}");

            // Step 5: Get piece information if serial was extracted
            if (!string.IsNullOrEmpty(serial))
            {
                context.AddStep($"Looking up piece info for serial: {serial}");
                context.PieceInfoResult = await _pieceInfoService.GetPieceInfoAsync(serial, correlationId);

                if (context.PieceInfoResult != null)
                {
                    context.AddStep($"Piece info found - SKU: {context.PieceInfoResult.SKU}, Family: {context.PieceInfoResult.Family}");
                    result.SKU = context.PieceInfoResult.SKU;
                    result.Family = context.PieceInfoResult.Family;
                }
                else
                {
                    context.AddStep("Piece info not found - continuing without enrichment");
                }
            }
            else
            {
                context.AddStep("No serial extracted - skipping piece info lookup");
            }

            // Step 6: Calculate overall confidence and determine status
            var pieceInfoFound = context.PieceInfoResult != null;
            var hasEnrichment = !string.IsNullOrEmpty(result.SKU) || !string.IsNullOrEmpty(result.Family);
            
            result.Serial = serial;
            result.ConfidenceScore = ProcessingResult.CalculateOverallConfidence(
                documentConfidence, 
                pieceInfoFound, 
                hasEnrichment);

            // Determine final status
            if (documentConfidence >= _processingSettings.CONFIDENCE_THRESHOLD)
            {
                result.Status = "Completed";
                context.AddStep($"Processing completed successfully - Overall confidence: {result.ConfidenceScore}");
            }
            else
            {
                result.Status = "LowConfidence";
                context.AddStep($"Processing completed with low confidence - Overall confidence: {result.ConfidenceScore}");
            }

            // Step 7: Update SharePoint list item
            context.AddStep("Updating SharePoint list item");
            await _sharePointService.UpdateListItemAsync(listItemId, result, correlationId);
            context.AddStep("SharePoint update completed");

            _logger.LogInformation(
                "[RETURNS-PROCESSING] Workflow completed - ItemId: {ItemId}, Status: {Status}, Serial: {Serial}, Confidence: {Confidence}, Duration: {Duration}ms, Correlation: {CorrelationId}",
                listItemId, result.Status, result.Serial, result.ConfidenceScore, context.GetElapsedTime().TotalMilliseconds, correlationId);

            return result;
        }
        catch (Exception ex)
        {
            var errorMsg = $"Unexpected error during processing: {ex.Message}";
            context.AddError(errorMsg);
            
            result.Status = "Failed";
            result.ErrorMessage = errorMsg;

            _logger.LogError(ex,
                "[RETURNS-PROCESSING] Workflow failed - ItemId: {ItemId}, Duration: {Duration}ms, Steps: {Steps}, Errors: {Errors}, Correlation: {CorrelationId}",
                listItemId, context.GetElapsedTime().TotalMilliseconds, string.Join(" | ", context.ProcessingSteps), string.Join(" | ", context.Errors), correlationId);

            // Try to update SharePoint with error status
            try
            {
                await _sharePointService.UpdateListItemAsync(listItemId, result, correlationId);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx,
                    "[RETURNS-PROCESSING] Failed to update SharePoint with error status - ItemId: {ItemId}, Correlation: {CorrelationId}",
                    listItemId, correlationId);
            }
        }

        return result;
    }

    /// <summary>
    /// Detect content type from file extension
    /// </summary>
    private string GetContentTypeFromFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "application/octet-stream";

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".tiff" or ".tif" => "image/tiff",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Validate service configuration and connectivity
    /// </summary>
    public async Task<bool> ValidateConfigurationAsync()
    {
        try
        {
            _logger.LogInformation("[RETURNS-PROCESSING] Validating configuration and connectivity");

            var sharePointTest = await _sharePointService.TestConnectionAsync();
            var docIntelTest = await _documentIntelligenceService.TestConnectionAsync();
            var pieceInfoTest = await _pieceInfoService.TestConnectionAsync();

            _logger.LogInformation(
                "[RETURNS-PROCESSING] Configuration validation - SharePoint: {SharePoint}, DocIntel: {DocIntel}, PieceInfo: {PieceInfo}",
                sharePointTest, docIntelTest, pieceInfoTest);

            return sharePointTest && docIntelTest && pieceInfoTest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RETURNS-PROCESSING] Configuration validation failed");
            return false;
        }
    }
}
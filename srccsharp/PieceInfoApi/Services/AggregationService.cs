using Microsoft.Extensions.Logging;
using System.Text.Json;
using WarehouseReturns.PieceInfoApi.Models;

namespace WarehouseReturns.PieceInfoApi.Services;

/// <summary>
/// Production-ready service for aggregating piece information from multiple external APIs
/// 
/// This service follows a sequential data collection pattern:
/// 1. Fetch piece inventory data using the piece number
/// 2. Extract SKU and vendor code from inventory data
/// 3. Fetch product master data using the SKU
/// 4. Fetch vendor details using the vendor code
/// 5. Aggregate all data into a unified response structure
/// </summary>
public class AggregationService : IAggregationService
{
    private readonly IExternalApiService _externalApiService;
    private readonly ILogger<AggregationService> _logger;

    /// <summary>
    /// Initialize aggregation service with external API client
    /// </summary>
    public AggregationService(
        IExternalApiService externalApiService,
        ILogger<AggregationService> logger)
    {
        _externalApiService = externalApiService ?? throw new ArgumentNullException(nameof(externalApiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _logger.LogInformation("AggregationService initialized successfully");
    }

    /// <summary>
    /// Get comprehensive aggregated piece information by orchestrating multiple API calls
    /// </summary>
    public async Task<AggregatedPieceInfo> GetAggregatedPieceInfoAsync(
        string pieceNumber, 
        string correlationId, 
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation(
            "Starting aggregation process for piece: {PieceNumber} - Correlation ID: {CorrelationId}",
            pieceNumber, correlationId);

        // Input validation
        if (string.IsNullOrWhiteSpace(pieceNumber))
        {
            throw new ArgumentException("piece_number must be a non-empty string", nameof(pieceNumber));
        }

        pieceNumber = pieceNumber.Trim();
        if (pieceNumber.Length < 3)
        {
            throw new ArgumentException("piece_number must be at least 3 characters long", nameof(pieceNumber));
        }

        try
        {
            // ===================================================================
            // STEP 1: Get piece inventory location data
            // ===================================================================
            _logger.LogDebug(
                "Step 1/3: Fetching piece inventory data for {PieceNumber} - Correlation ID: {CorrelationId}",
                pieceNumber, correlationId);
            
            var pieceInventory = await _externalApiService.GetPieceInventoryAsync(pieceNumber, cancellationToken);
            
            // Extract critical fields required for subsequent API calls
            var sku = pieceInventory.Sku;
            var vendorCode = pieceInventory.Vendor;
            
            // Validate extracted data
            if (string.IsNullOrWhiteSpace(sku))
            {
                _logger.LogError(
                    "SKU not found in piece inventory data for {PieceNumber} - Correlation ID: {CorrelationId}",
                    pieceNumber, correlationId);
                throw new InvalidOperationException("SKU not found in piece inventory data - cannot proceed with product lookup");
            }
            
            if (string.IsNullOrWhiteSpace(vendorCode))
            {
                _logger.LogError(
                    "Vendor code not found in piece inventory data for {PieceNumber} - Correlation ID: {CorrelationId}",
                    pieceNumber, correlationId);
                throw new InvalidOperationException("Vendor code not found in piece inventory data - cannot proceed with vendor lookup");
            }
            
            _logger.LogInformation(
                "Successfully extracted SKU: {Sku}, Vendor: {VendorCode} - Correlation ID: {CorrelationId}",
                sku, vendorCode, correlationId);

            // ===================================================================
            // STEP 2: Get product master data using SKU
            // ===================================================================
            _logger.LogDebug(
                "Step 2/3: Fetching product master data for SKU: {Sku} - Correlation ID: {CorrelationId}",
                sku, correlationId);
            
            ProductMasterResponse productMaster;
            try
            {
                productMaster = await _externalApiService.GetProductMasterAsync(sku, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to fetch product master data for SKU: {Sku} - continuing with empty data - Correlation ID: {CorrelationId}",
                    sku, correlationId);
                productMaster = new ProductMasterResponse(); // Continue with empty data
            }

            // ===================================================================
            // STEP 3: Get vendor details using vendor code
            // ===================================================================
            _logger.LogDebug(
                "Step 3/3: Fetching vendor details for vendor: {VendorCode} - Correlation ID: {CorrelationId}",
                vendorCode, correlationId);
            
            VendorDetailsResponse vendorDetails;
            try
            {
                vendorDetails = await _externalApiService.GetVendorDetailsAsync(vendorCode, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to fetch vendor details for vendor: {VendorCode} - continuing with empty data - Correlation ID: {CorrelationId}",
                    vendorCode, correlationId);
                vendorDetails = new VendorDetailsResponse(); // Continue with empty data
            }

            // ===================================================================
            // STEP 4: Aggregate and structure the response data
            // ===================================================================
            _logger.LogDebug(
                "Step 4/4: Aggregating data from all sources for piece: {PieceNumber} - Correlation ID: {CorrelationId}",
                pieceNumber, correlationId);
            
            // Build the aggregated response with data from all three APIs
            var aggregatedData = new AggregatedPieceInfo
            {
                // =============== PIECE INVENTORY DATA ===============
                PieceInventoryKey = pieceInventory.PieceInventoryKey ?? pieceNumber,
                Sku = sku,
                VendorCode = vendorCode,
                WarehouseLocation = pieceInventory.WarehouseLocation ?? string.Empty,
                RackLocation = pieceInventory.RackLocation ?? string.Empty,
                SerialNumber = pieceInventory.SerialNumber ?? string.Empty,
                Family = pieceInventory.Family ?? string.Empty,
                PurchaseReferenceNumber = pieceInventory.PurchaseReferenceNumber ?? string.Empty,
                
                // =============== PRODUCT MASTER DATA ===============
                Description = productMaster.Description ?? string.Empty,
                ModelNo = productMaster.ModelNo ?? string.Empty,
                Brand = productMaster.Brand ?? string.Empty,
                Category = productMaster.Category ?? string.Empty,
                Group = productMaster.Group ?? string.Empty,
                
                // =============== VENDOR DETAILS DATA ===============
                VendorName = vendorDetails.Name ?? string.Empty,
                
                // Vendor address as nested object with all fields defaulted
                VendorAddress = new VendorAddress
                {
                    AddressLine1 = vendorDetails.AddressLine1 ?? string.Empty,
                    AddressLine2 = vendorDetails.AddressLine2 ?? string.Empty,
                    City = vendorDetails.City ?? string.Empty,
                    State = vendorDetails.State ?? string.Empty,
                    ZipCode = vendorDetails.ZipCode ?? string.Empty
                },
                
                // Vendor contact information with email fields
                VendorContact = new VendorContact
                {
                    RepName = vendorDetails.RepName ?? string.Empty,
                    PrimaryRepEmail = vendorDetails.PrimaryRepEmail ?? string.Empty,
                    SecondaryRepEmail = vendorDetails.SecondaryRepEmail ?? string.Empty,
                    ExecEmail = vendorDetails.ExecEmail // Explicitly allow null
                },
                
                // Vendor policies with proper boolean conversion
                VendorPolicies = new VendorPolicies
                {
                    SerialNumberRequired = ConvertToBoolean(vendorDetails.SerialNumberRequired),
                    VendorReturn = ConvertToBoolean(vendorDetails.VendorReturn)
                },
                
                // Response metadata for tracking
                Metadata = new ResponseMetadata
                {
                    CorrelationId = correlationId,
                    Timestamp = DateTime.UtcNow,
                    Version = "1.0.0",
                    Source = "pieceinfo-api"
                }
            };
            
            // Calculate processing time for monitoring
            var endTime = DateTime.UtcNow;
            var processingTime = (endTime - startTime).TotalSeconds;
            
            _logger.LogInformation(
                "Piece info aggregated successfully: {PieceNumber} in {ProcessingTime:F2}s - SKU: {Sku}, Vendor: {VendorCode} - Correlation ID: {CorrelationId}",
                pieceNumber, processingTime, aggregatedData.Sku, aggregatedData.VendorCode, correlationId);
            
            return aggregatedData;
        }
        catch (ArgumentException)
        {
            // Re-raise validation errors without additional logging
            throw;
        }
        catch (Exception ex)
        {
            // Log detailed error information for troubleshooting
            var endTime = DateTime.UtcNow;
            var processingTime = (endTime - startTime).TotalSeconds;
            
            _logger.LogError(ex,
                "Aggregation failed: {PieceNumber} after {ProcessingTime:F2}s - Error: {ErrorType} - Correlation ID: {CorrelationId}",
                pieceNumber, processingTime, ex.GetType().Name, correlationId);
            
            // Re-raise with additional context
            throw new InvalidOperationException($"Aggregation failed for piece {pieceNumber}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Convert various value representations to boolean
    /// </summary>
    private static bool ConvertToBoolean(object? value)
    {
        if (value is null)
            return false;
            
        if (value is bool boolValue)
            return boolValue;
            
        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => Math.Abs(jsonElement.GetDouble()) > 1e-10,
                JsonValueKind.String => jsonElement.GetString()?.Trim().ToLowerInvariant() is "true" or "1" or "yes" or "on",
                _ => false
            };
        }
            
        if (value is int intValue)
            return intValue != 0;
            
        if (value is double doubleValue)
            return Math.Abs(doubleValue) > 1e-10;
            
        if (value is string stringValue)
            return stringValue.Trim().ToLowerInvariant() is "true" or "1" or "yes" or "on";
            
        try
        {
            return Convert.ToBoolean(value);
        }
        catch
        {
            return false;
        }
    }
}
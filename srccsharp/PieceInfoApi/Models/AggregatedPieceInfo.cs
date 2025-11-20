using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WarehouseReturns.PieceInfoApi.Models;

/// <summary>
/// Comprehensive aggregated piece information combining data from multiple external APIs
/// 
/// This model represents the unified response structure that aggregates:
/// - Piece Inventory Location data (warehouse, rack, serial numbers)
/// - Product Master data (descriptions, models, brands, categories)
/// - Vendor Details (contact information, addresses, policies)
/// </summary>
public record AggregatedPieceInfo
{
    /// <summary>
    /// Original piece inventory identifier
    /// </summary>
    [JsonPropertyName("piece_inventory_key")]
    public string PieceInventoryKey { get; init; } = string.Empty;

    /// <summary>
    /// Stock keeping unit identifier extracted from inventory data
    /// </summary>
    [JsonPropertyName("sku")]
    public string Sku { get; init; } = string.Empty;

    /// <summary>
    /// Vendor identifier code extracted from inventory data
    /// </summary>
    [JsonPropertyName("vendor_code")]
    public string VendorCode { get; init; } = string.Empty;

    /// <summary>
    /// Physical warehouse location code
    /// </summary>
    [JsonPropertyName("warehouse_location")]
    public string WarehouseLocation { get; init; } = string.Empty;

    /// <summary>
    /// Specific rack/shelf location within warehouse
    /// </summary>
    [JsonPropertyName("rack_location")]
    public string RackLocation { get; init; } = string.Empty;

    /// <summary>
    /// Device serial number for tracking and warranty
    /// </summary>
    [JsonPropertyName("serial_number")]
    public string SerialNumber { get; init; } = string.Empty;

    /// <summary>
    /// Product family classification
    /// </summary>
    [JsonPropertyName("family")]
    public string Family { get; init; } = string.Empty;

    /// <summary>
    /// Purchase reference number for procurement tracking
    /// </summary>
    [JsonPropertyName("purchase_reference_number")]
    public string PurchaseReferenceNumber { get; init; } = string.Empty;

    /// <summary>
    /// Product description from master data
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Product model number
    /// </summary>
    [JsonPropertyName("model_no")]
    public string ModelNo { get; init; } = string.Empty;

    /// <summary>
    /// Product brand name
    /// </summary>
    [JsonPropertyName("brand")]
    public string Brand { get; init; } = string.Empty;

    /// <summary>
    /// Product category classification
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Product group classification
    /// </summary>
    [JsonPropertyName("group")]
    public string Group { get; init; } = string.Empty;

    /// <summary>
    /// Vendor company name
    /// </summary>
    [JsonPropertyName("vendor_name")]
    public string VendorName { get; init; } = string.Empty;

    /// <summary>
    /// Vendor address information
    /// </summary>
    [JsonPropertyName("vendor_address")]
    public VendorAddress VendorAddress { get; init; } = new();

    /// <summary>
    /// Vendor contact information
    /// </summary>
    [JsonPropertyName("vendor_contact")]
    public VendorContact VendorContact { get; init; } = new();

    /// <summary>
    /// Vendor business policies
    /// </summary>
    [JsonPropertyName("vendor_policies")]
    public VendorPolicies VendorPolicies { get; init; } = new();

    /// <summary>
    /// Response metadata for tracking and monitoring
    /// </summary>
    [JsonPropertyName("metadata")]
    public ResponseMetadata? Metadata { get; init; }
}

/// <summary>
/// Vendor address information
/// </summary>
public record VendorAddress
{
    [JsonPropertyName("address_line1")]
    public string AddressLine1 { get; init; } = string.Empty;

    [JsonPropertyName("address_line2")]
    public string AddressLine2 { get; init; } = string.Empty;

    [JsonPropertyName("city")]
    public string City { get; init; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; init; } = string.Empty;

    [JsonPropertyName("zip_code")]
    public string ZipCode { get; init; } = string.Empty;
}

/// <summary>
/// Vendor contact information
/// </summary>
public record VendorContact
{
    [JsonPropertyName("rep_name")]
    public string RepName { get; init; } = string.Empty;

    [JsonPropertyName("primary_rep_email")]
    public string PrimaryRepEmail { get; init; } = string.Empty;

    [JsonPropertyName("secondary_rep_email")]
    public string SecondaryRepEmail { get; init; } = string.Empty;

    [JsonPropertyName("exec_email")]
    public string? ExecEmail { get; init; }
}

/// <summary>
/// Vendor business policies for returns and serial number requirements
/// </summary>
public record VendorPolicies
{
    [JsonPropertyName("serial_number_required")]
    public bool SerialNumberRequired { get; init; }

    [JsonPropertyName("vendor_return")]
    public bool VendorReturn { get; init; }
}

/// <summary>
/// Response metadata for tracking and monitoring
/// </summary>
public record ResponseMetadata
{
    [JsonPropertyName("correlation_id")]
    public string CorrelationId { get; init; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; }

    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0.0";

    [JsonPropertyName("source")]
    public string Source { get; init; } = "pieceinfo-api";
}
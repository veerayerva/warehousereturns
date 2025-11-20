using System.Text.Json.Serialization;

namespace WarehouseReturns.PieceInfoApi.Models;

/// <summary>
/// Response model for piece inventory location API
/// </summary>
public record PieceInventoryResponse
{
    [JsonPropertyName("pieceInventoryKey")]
    public string? PieceInventoryKey { get; init; }

    [JsonPropertyName("sku")]
    public string? Sku { get; init; }

    [JsonPropertyName("vendor")]
    public string? Vendor { get; init; }

    [JsonPropertyName("warehouseLocation")]
    public string? WarehouseLocation { get; init; }

    [JsonPropertyName("rackLocation")]
    public string? RackLocation { get; init; }

    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; init; }

    [JsonPropertyName("family")]
    public string? Family { get; init; }

    [JsonPropertyName("purchaseReferenceNumber")]
    public string? PurchaseReferenceNumber { get; init; }
}

/// <summary>
/// Response model for product master API
/// </summary>
public record ProductMasterResponse
{
    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("modelNo")]
    public string? ModelNo { get; init; }

    [JsonPropertyName("brand")]
    public string? Brand { get; init; }

    [JsonPropertyName("category")]
    public string? Category { get; init; }

    [JsonPropertyName("group")]
    public string? Group { get; init; }
}

/// <summary>
/// Response model for vendor details API
/// </summary>
public record VendorDetailsResponse
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("addressLine1")]
    public string? AddressLine1 { get; init; }

    [JsonPropertyName("addressLine2")]
    public string? AddressLine2 { get; init; }

    [JsonPropertyName("city")]
    public string? City { get; init; }

    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("zipCode")]
    public string? ZipCode { get; init; }

    [JsonPropertyName("repName")]
    public string? RepName { get; init; }

    [JsonPropertyName("primaryRepEmail")]
    public string? PrimaryRepEmail { get; init; }

    [JsonPropertyName("secondaryRepEmail")]
    public string? SecondaryRepEmail { get; init; }

    [JsonPropertyName("execEmail")]
    public string? ExecEmail { get; init; }

    [JsonPropertyName("serialNumberRequired")]
    public object? SerialNumberRequired { get; init; }

    [JsonPropertyName("vendorReturn")]
    public object? VendorReturn { get; init; }
}
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RichardSzalay.MockHttp;
using Moq;

namespace WarehouseReturns.PieceInfoApi.Tests.Helpers;

/// <summary>
/// Helper class for creating mock HTTP responses for testing external API calls
/// </summary>
public static class MockHttpHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Create a mock HTTP client with predefined responses for piece inventory API
    /// </summary>
    public static HttpClient CreateMockPieceInventoryClient(string pieceNumber, bool shouldReturnSuccess = true)
    {
        var mockHttp = new MockHttpMessageHandler();
        var expectedUrl = $"*/piece-inventory-location/{pieceNumber}";
        
        if (shouldReturnSuccess)
        {
            var response = new
            {
                piece_inventory_key = pieceNumber,
                sku = "67007500",
                vendor_code = "VIZIA",
                warehouse_location = "WHKCTY",
                rack_location = "R03-019-03",
                serial_number = "SZVOU5GB1600294",
                family = "ELECTRONICS",
                purchase_reference_number = "PO-2024-001234"
            };
            
            mockHttp.When(HttpMethod.Get, expectedUrl)
                   .Respond("application/json", JsonSerializer.Serialize(response, JsonOptions));
        }
        else
        {
            mockHttp.When(HttpMethod.Get, expectedUrl)
                   .Respond(HttpStatusCode.NotFound, "application/json", 
                           JsonSerializer.Serialize(new { error = "Piece not found" }, JsonOptions));
        }
        
        return mockHttp.ToHttpClient();
    }

    /// <summary>
    /// Create a mock HTTP client with predefined responses for product master API
    /// </summary>
    public static HttpClient CreateMockProductMasterClient(string sku, bool shouldReturnSuccess = true)
    {
        var mockHttp = new MockHttpMessageHandler();
        var expectedUrl = $"*/product-master/{sku}";
        
        if (shouldReturnSuccess)
        {
            var response = new
            {
                sku,
                description = "ALL-IN-ONE SOUNDBAR",
                model_no = "V-SB2921-C6",
                brand = "VIZIO",
                category = "AUDIO",
                group = "SOUNDBARS"
            };
            
            mockHttp.When(HttpMethod.Get, expectedUrl)
                   .Respond("application/json", JsonSerializer.Serialize(response, JsonOptions));
        }
        else
        {
            mockHttp.When(HttpMethod.Get, expectedUrl)
                   .Respond(HttpStatusCode.NotFound, "application/json", 
                           JsonSerializer.Serialize(new { error = "Product not found" }, JsonOptions));
        }
        
        return mockHttp.ToHttpClient();
    }

    /// <summary>
    /// Create a mock HTTP client with predefined responses for vendor details API
    /// </summary>
    public static HttpClient CreateMockVendorDetailsClient(string vendorCode, bool shouldReturnSuccess = true)
    {
        var mockHttp = new MockHttpMessageHandler();
        var expectedUrl = $"*/vendor/{vendorCode}";
        
        if (shouldReturnSuccess)
        {
            var response = new
            {
                vendor_code = vendorCode,
                vendor_name = "NIGHT & DAY",
                address = new
                {
                    address_line1 = "123 Vendor Street",
                    address_line2 = "Suite 100",
                    city = "Commerce City",
                    state = "CA",
                    zip_code = "90210"
                },
                contact = new
                {
                    rep_name = "John Smith",
                    primary_rep_email = "john.smith@vendor.com",
                    secondary_rep_email = "support@vendor.com",
                    exec_email = "exec@vendor.com"
                },
                policies = new
                {
                    serial_number_required = true,
                    vendor_return = true
                }
            };
            
            mockHttp.When(HttpMethod.Get, expectedUrl)
                   .Respond("application/json", JsonSerializer.Serialize(response, JsonOptions));
        }
        else
        {
            mockHttp.When(HttpMethod.Get, expectedUrl)
                   .Respond(HttpStatusCode.NotFound, "application/json", 
                           JsonSerializer.Serialize(new { error = "Vendor not found" }, JsonOptions));
        }
        
        return mockHttp.ToHttpClient();
    }

    /// <summary>
    /// Create a comprehensive mock HTTP client for all external APIs
    /// </summary>
    public static HttpClient CreateComprehensiveMockClient(
        string pieceNumber = "170080637",
        string sku = "67007500", 
        string vendorCode = "VIZIA",
        bool allSuccess = true,
        bool pieceInventorySuccess = true,
        bool productMasterSuccess = true,
        bool vendorDetailsSuccess = true)
    {
        var mockHttp = new MockHttpMessageHandler();

        // Piece Inventory API Mock
        if (allSuccess && pieceInventorySuccess)
        {
            var pieceResponse = new
            {
                piece_inventory_key = pieceNumber,
                sku,
                vendor_code = vendorCode,
                warehouse_location = "WHKCTY",
                rack_location = "R03-019-03",
                serial_number = "SZVOU5GB1600294",
                family = "ELECTRONICS",
                purchase_reference_number = "PO-2024-001234"
            };
            
            mockHttp.When(HttpMethod.Get, $"*/piece-inventory-location/{pieceNumber}")
                   .Respond("application/json", JsonSerializer.Serialize(pieceResponse, JsonOptions));
        }
        else
        {
            mockHttp.When(HttpMethod.Get, $"*/piece-inventory-location/{pieceNumber}")
                   .Respond(HttpStatusCode.NotFound);
        }

        // Product Master API Mock
        if (allSuccess && productMasterSuccess)
        {
            var productResponse = new
            {
                sku,
                description = "ALL-IN-ONE SOUNDBAR",
                model_no = "V-SB2921-C6",
                brand = "VIZIO",
                category = "AUDIO",
                group = "SOUNDBARS"
            };
            
            mockHttp.When(HttpMethod.Get, $"*/product-master/{sku}")
                   .Respond("application/json", JsonSerializer.Serialize(productResponse, JsonOptions));
        }
        else
        {
            mockHttp.When(HttpMethod.Get, $"*/product-master/{sku}")
                   .Respond(HttpStatusCode.NotFound);
        }

        // Vendor Details API Mock
        if (allSuccess && vendorDetailsSuccess)
        {
            var vendorResponse = new
            {
                vendor_code = vendorCode,
                vendor_name = "NIGHT & DAY",
                address = new
                {
                    address_line1 = "123 Vendor Street",
                    address_line2 = "Suite 100",
                    city = "Commerce City",
                    state = "CA",
                    zip_code = "90210"
                },
                contact = new
                {
                    rep_name = "John Smith",
                    primary_rep_email = "john.smith@vendor.com",
                    secondary_rep_email = "support@vendor.com",
                    exec_email = "exec@vendor.com"
                },
                policies = new
                {
                    serial_number_required = true,
                    vendor_return = true
                }
            };
            
            mockHttp.When(HttpMethod.Get, $"*/vendor/{vendorCode}")
                   .Respond("application/json", JsonSerializer.Serialize(vendorResponse, JsonOptions));
        }
        else
        {
            mockHttp.When(HttpMethod.Get, $"*/vendor/{vendorCode}")
                   .Respond(HttpStatusCode.NotFound);
        }

        return mockHttp.ToHttpClient();
    }

    /// <summary>
    /// Create a mock HTTP client that simulates timeout scenarios
    /// </summary>
    public static HttpClient CreateTimeoutMockClient(string url = "*")
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, url)
               .Respond(async () =>
               {
                   await Task.Delay(31000); // Simulate timeout (longer than typical 30s timeout)
                   return new HttpResponseMessage(HttpStatusCode.OK);
               });
        
        return mockHttp.ToHttpClient();
    }

    /// <summary>
    /// Create a mock HTTP client that simulates server error scenarios
    /// </summary>
    public static HttpClient CreateServerErrorMockClient(HttpStatusCode errorCode = HttpStatusCode.InternalServerError)
    {
        var mockHttp = new MockHttpMessageHandler();
        mockHttp.When(HttpMethod.Get, "*")
               .Respond(errorCode, "application/json", 
                       JsonSerializer.Serialize(new { error = "Internal server error" }, JsonOptions));
        
        return mockHttp.ToHttpClient();
    }
}

/// <summary>
/// Helper class for creating test logger instances
/// </summary>
public static class TestLoggerHelper
{
    /// <summary>
    /// Create a test logger using Microsoft.Extensions.Logging.Testing
    /// </summary>
    public static ILogger<T> CreateTestLogger<T>() where T : class
    {
        var loggerFactory = LoggerFactory.Create(builder => 
        {
            builder.AddConsole()
                   .SetMinimumLevel(LogLevel.Debug);
        });
        
        return loggerFactory.CreateLogger<T>();
    }

    /// <summary>
    /// Create a mock logger for testing
    /// </summary>
    public static Mock<ILogger<T>> CreateMockLogger<T>() where T : class
    {
        return new Mock<ILogger<T>>();
    }
}

/// <summary>
/// Helper class for creating test data objects
/// </summary>
public static class TestDataHelper
{
    /// <summary>
    /// Create a valid AggregatedPieceInfo test object
    /// </summary>
    public static WarehouseReturns.PieceInfoApi.Models.AggregatedPieceInfo CreateValidAggregatedPieceInfo(
        string pieceNumber = "170080637",
        string sku = "67007500",
        string vendorCode = "VIZIA")
    {
        return new WarehouseReturns.PieceInfoApi.Models.AggregatedPieceInfo
        {
            PieceInventoryKey = pieceNumber,
            Sku = sku,
            VendorCode = vendorCode,
            WarehouseLocation = "WHKCTY",
            RackLocation = "R03-019-03",
            SerialNumber = "SZVOU5GB1600294",
            Family = "ELECTRONICS",
            PurchaseReferenceNumber = "PO-2024-001234",
            Description = "ALL-IN-ONE SOUNDBAR",
            ModelNo = "V-SB2921-C6",
            Brand = "VIZIO",
            Category = "AUDIO",
            Group = "SOUNDBARS",
            VendorName = "NIGHT & DAY",
            VendorAddress = new WarehouseReturns.PieceInfoApi.Models.VendorAddress
            {
                AddressLine1 = "123 Vendor Street",
                AddressLine2 = "Suite 100",
                City = "Commerce City",
                State = "CA",
                ZipCode = "90210"
            },
            VendorContact = new WarehouseReturns.PieceInfoApi.Models.VendorContact
            {
                RepName = "John Smith",
                PrimaryRepEmail = "john.smith@vendor.com",
                SecondaryRepEmail = "support@vendor.com",
                ExecEmail = "exec@vendor.com"
            },
            VendorPolicies = new WarehouseReturns.PieceInfoApi.Models.VendorPolicies
            {
                SerialNumberRequired = true,
                VendorReturn = true
            },
            Metadata = new WarehouseReturns.PieceInfoApi.Models.ResponseMetadata
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0",
                Source = "pieceinfo-api"
            }
        };
    }
}
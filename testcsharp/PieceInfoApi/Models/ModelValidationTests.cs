using Xunit;
using FluentAssertions;
using WarehouseReturns.PieceInfoApi.Models;

namespace WarehouseReturns.PieceInfoApi.Tests.Models;

/// <summary>
/// Simple unit tests for data models
/// Tests model initialization, property setting, and basic validation
/// </summary>
public class ModelValidationTests
{
    [Fact]
    public void AggregatedPieceInfo_DefaultConstructor_ShouldInitializeWithEmptyValues()
    {
        // Act
        var model = new AggregatedPieceInfo();

        // Assert
        model.Should().NotBeNull();
        model.PieceInventoryKey.Should().BeEmpty();
        model.Sku.Should().BeEmpty();
        model.VendorCode.Should().BeEmpty();
        model.WarehouseLocation.Should().BeEmpty();
        model.RackLocation.Should().BeEmpty();
        model.SerialNumber.Should().BeEmpty();
        model.Description.Should().BeEmpty();
        model.VendorName.Should().BeEmpty();
    }

    [Fact]
    public void AggregatedPieceInfo_WithInitValues_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        const string pieceNumber = "170080637";
        const string sku = "67007500";
        const string vendorCode = "VIZIA";
        const string description = "ALL-IN-ONE SOUNDBAR";

        // Act
        var model = new AggregatedPieceInfo
        {
            PieceInventoryKey = pieceNumber,
            Sku = sku,
            VendorCode = vendorCode,
            Description = description,
            WarehouseLocation = "WHKCTY",
            RackLocation = "R03-019-03",
            SerialNumber = "SZVOU5GB1600294"
        };

        // Assert
        model.PieceInventoryKey.Should().Be(pieceNumber);
        model.Sku.Should().Be(sku);
        model.VendorCode.Should().Be(vendorCode);
        model.Description.Should().Be(description);
        model.WarehouseLocation.Should().Be("WHKCTY");
        model.RackLocation.Should().Be("R03-019-03");
        model.SerialNumber.Should().Be("SZVOU5GB1600294");
    }

    [Fact]
    public void VendorAddress_DefaultConstructor_ShouldInitializeWithEmptyValues()
    {
        // Act
        var address = new VendorAddress();

        // Assert
        address.Should().NotBeNull();
        address.AddressLine1.Should().BeEmpty();
        address.AddressLine2.Should().BeEmpty();
        address.City.Should().BeEmpty();
        address.State.Should().BeEmpty();
        address.ZipCode.Should().BeEmpty();
    }

    [Fact]
    public void VendorContact_DefaultConstructor_ShouldInitializeWithEmptyValues()
    {
        // Act
        var contact = new VendorContact();

        // Assert
        contact.Should().NotBeNull();
        contact.RepName.Should().BeEmpty();
        contact.PrimaryRepEmail.Should().BeEmpty();
        contact.SecondaryRepEmail.Should().BeEmpty();
        contact.ExecEmail.Should().BeNull();
    }

    [Fact]
    public void VendorPolicies_DefaultConstructor_ShouldInitializeWithFalseValues()
    {
        // Act
        var policies = new VendorPolicies();

        // Assert
        policies.Should().NotBeNull();
        policies.SerialNumberRequired.Should().BeFalse();
        policies.VendorReturn.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void VendorPolicies_WithBooleanValues_ShouldSetCorrectly(bool serialNumberRequired, bool vendorReturn)
    {
        // Act
        var policies = new VendorPolicies
        {
            SerialNumberRequired = serialNumberRequired,
            VendorReturn = vendorReturn
        };

        // Assert
        policies.SerialNumberRequired.Should().Be(serialNumberRequired);
        policies.VendorReturn.Should().Be(vendorReturn);
    }

    [Fact]
    public void ResponseMetadata_WithValues_ShouldSetCorrectly()
    {
        // Arrange
        var correlationId = Guid.NewGuid().ToString();
        var timestamp = DateTime.UtcNow;
        const string version = "1.0.0";
        const string source = "pieceinfo-api";

        // Act
        var metadata = new ResponseMetadata
        {
            CorrelationId = correlationId,
            Timestamp = timestamp,
            Version = version,
            Source = source
        };

        // Assert
        metadata.CorrelationId.Should().Be(correlationId);
        metadata.Timestamp.Should().Be(timestamp);
        metadata.Version.Should().Be(version);
        metadata.Source.Should().Be(source);
    }

    [Fact]
    public void CompleteAggregatedPieceInfo_WithAllProperties_ShouldBeValid()
    {
        // Arrange & Act
        var model = CreateCompleteModel();

        // Assert
        model.Should().NotBeNull();
        model.PieceInventoryKey.Should().NotBeEmpty();
        model.Sku.Should().NotBeEmpty();
        model.VendorCode.Should().NotBeEmpty();
        model.VendorAddress.Should().NotBeNull();
        model.VendorContact.Should().NotBeNull();
        model.VendorPolicies.Should().NotBeNull();
        model.Metadata.Should().NotBeNull();
        
        // Verify nested objects are properly set
        model.VendorAddress.AddressLine1.Should().Be("123 Vendor Street");
        model.VendorContact.RepName.Should().Be("John Smith");
        model.VendorPolicies.SerialNumberRequired.Should().BeTrue();
        model.Metadata!.Source.Should().Be("pieceinfo-api");
    }

    [Fact]
    public void VendorAddress_WithCompleteAddress_ShouldFormatCorrectly()
    {
        // Arrange
        var address = new VendorAddress
        {
            AddressLine1 = "123 Main Street",
            AddressLine2 = "Suite 456",
            City = "Commerce City",
            State = "CA",
            ZipCode = "90210"
        };

        // Act & Assert
        address.AddressLine1.Should().Be("123 Main Street");
        address.AddressLine2.Should().Be("Suite 456");
        address.City.Should().Be("Commerce City");
        address.State.Should().Be("CA");
        address.ZipCode.Should().Be("90210");
    }

    [Fact]
    public void VendorContact_WithAllEmails_ShouldSetCorrectly()
    {
        // Arrange
        var contact = new VendorContact
        {
            RepName = "Jane Doe",
            PrimaryRepEmail = "jane.doe@vendor.com",
            SecondaryRepEmail = "support@vendor.com",
            ExecEmail = "exec@vendor.com"
        };

        // Act & Assert
        contact.RepName.Should().Be("Jane Doe");
        contact.PrimaryRepEmail.Should().Be("jane.doe@vendor.com");
        contact.SecondaryRepEmail.Should().Be("support@vendor.com");
        contact.ExecEmail.Should().Be("exec@vendor.com");
    }

    [Theory]
    [InlineData("170080637", "67007500", "VIZIA")]
    [InlineData("123456789", "87654321", "SAMSUNG")]
    [InlineData("999888777", "11223344", "SONY")]
    public void AggregatedPieceInfo_WithDifferentValues_ShouldAcceptAllValidInputs(
        string pieceNumber, string sku, string vendorCode)
    {
        // Act
        var model = new AggregatedPieceInfo
        {
            PieceInventoryKey = pieceNumber,
            Sku = sku,
            VendorCode = vendorCode
        };

        // Assert
        model.PieceInventoryKey.Should().Be(pieceNumber);
        model.Sku.Should().Be(sku);
        model.VendorCode.Should().Be(vendorCode);
    }

    private static AggregatedPieceInfo CreateCompleteModel()
    {
        return new AggregatedPieceInfo
        {
            PieceInventoryKey = "170080637",
            Sku = "67007500",
            VendorCode = "VIZIA",
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
            VendorAddress = new VendorAddress
            {
                AddressLine1 = "123 Vendor Street",
                AddressLine2 = "Suite 100",
                City = "Commerce City",
                State = "CA",
                ZipCode = "90210"
            },
            VendorContact = new VendorContact
            {
                RepName = "John Smith",
                PrimaryRepEmail = "john.smith@vendor.com",
                SecondaryRepEmail = "support@vendor.com",
                ExecEmail = "exec@vendor.com"
            },
            VendorPolicies = new VendorPolicies
            {
                SerialNumberRequired = true,
                VendorReturn = true
            },
            Metadata = new ResponseMetadata
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0",
                Source = "pieceinfo-api"
            }
        };
    }
}
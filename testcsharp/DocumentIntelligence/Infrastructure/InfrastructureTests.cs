using Xunit;
using FluentAssertions;
using System.Text.Json;

namespace WarehouseReturns.DocumentIntelligence.Tests.Infrastructure;

/// <summary>
/// Basic infrastructure tests to validate test infrastructure and patterns for DocumentIntelligence
/// </summary>
public class InfrastructureTests
{
    [Fact]
    public void Test_Infrastructure_Should_Be_Working()
    {
        // Arrange
        var testValue = "DocumentIntelligence Infrastructure Tests";
        
        // Act
        var result = testValue.ToUpperInvariant();
        
        // Assert
        result.Should().Be("DOCUMENTINTELLIGENCE INFRASTRUCTURE TESTS");
        testValue.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(1, 1, 2)]
    [InlineData(5, 3, 8)]
    [InlineData(-1, 1, 0)]
    [InlineData(0, 0, 0)]
    public void Basic_Math_Operations_Should_Work_Correctly(int a, int b, int expected)
    {
        // Act
        var result = a + b;
        
        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Guid_Generation_Should_Create_Unique_Values()
    {
        // Act
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        
        // Assert
        guid1.Should().NotBe(guid2);
        guid1.Should().NotBe(Guid.Empty);
        guid2.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void DateTime_Should_Be_Reasonable()
    {
        // Arrange
        var before = DateTime.UtcNow;
        
        // Act
        System.Threading.Thread.Sleep(1); // Small delay
        var after = DateTime.UtcNow;
        
        // Assert
        after.Should().BeAfter(before);
        after.Year.Should().BeGreaterThan(2020);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void String_Validation_Should_Detect_Empty_Values(string? input)
    {
        // Act & Assert
        string.IsNullOrWhiteSpace(input).Should().BeTrue();
    }

    [Theory]
    [InlineData("https://example.com/document.pdf")]
    [InlineData("prebuilt-invoice")]
    [InlineData("test-correlation-id")]
    public void String_Validation_Should_Accept_Valid_Values(string input)
    {
        // Act & Assert
        string.IsNullOrWhiteSpace(input).Should().BeFalse();
        input.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Collections_Should_Work_As_Expected()
    {
        // Arrange
        var items = new List<string> { "pdf", "jpeg", "png", "tiff" };
        
        // Act
        var count = items.Count;
        var firstItem = items.FirstOrDefault();
        
        // Assert
        count.Should().Be(4);
        firstItem.Should().Be("pdf");
        items.Should().Contain("jpeg");
        items.Should().NotContain("doc");
    }

    [Fact]
    public void Exception_Handling_Should_Work()
    {
        // Act & Assert
        Action act = () => throw new InvalidOperationException("Test exception");
        
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("Test exception");
    }

    [Fact]
    public void Json_Serialization_Should_Work()
    {
        // Arrange
        var testObject = new { Name = "Test Document", Type = "PDF", Size = 1024 };
        
        // Act
        var json = JsonSerializer.Serialize(testObject);
        var deserializedObject = JsonSerializer.Deserialize<object>(json);
        
        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("Test Document");
        deserializedObject.Should().NotBeNull();
    }

    [Theory]
    [InlineData("application/pdf", true)]
    [InlineData("image/jpeg", true)]
    [InlineData("image/png", true)]
    [InlineData("text/plain", false)]
    [InlineData("application/xml", false)]
    public void Content_Type_Validation_Should_Work(string contentType, bool isSupported)
    {
        // Arrange
        var supportedTypes = new[] { "application/pdf", "image/jpeg", "image/png", "image/tiff" };
        
        // Act
        var result = supportedTypes.Contains(contentType);
        
        // Assert
        result.Should().Be(isSupported);
    }

    [Theory]
    [InlineData(0.0, true)]
    [InlineData(0.5, true)]
    [InlineData(1.0, true)]
    [InlineData(-0.1, false)]
    [InlineData(1.1, false)]
    public void Confidence_Threshold_Validation_Should_Work(double threshold, bool isValid)
    {
        // Act & Assert
        var result = threshold >= 0.0 && threshold <= 1.0;
        result.Should().Be(isValid);
    }

    [Fact]
    public void URL_Validation_Should_Work()
    {
        // Arrange
        var validUrls = new[]
        {
            "https://example.com/document.pdf",
            "https://storage.azure.com/container/file.jpg"
        };
        
        var invalidUrls = new[]
        {
            "ftp://example.com/file.pdf",
            "not-a-url",
            ""
        };
        
        // Act & Assert
        foreach (var url in validUrls)
        {
            Uri.TryCreate(url, UriKind.Absolute, out var uri).Should().BeTrue();
            uri?.Scheme.Should().Be("https");
        }
        
        foreach (var url in invalidUrls)
        {
            if (string.IsNullOrEmpty(url))
            {
                url.Should().BeNullOrEmpty();
            }
            else
            {
                // Check that the URL is either invalid or not HTTP/HTTPS
                var isValidUri = Uri.TryCreate(url, UriKind.Absolute, out var uri);
                if (isValidUri)
                {
                    // If it's a valid URI, it should not be HTTP or HTTPS for our purposes
                    uri!.Scheme.Should().NotBe("http");
                    uri.Scheme.Should().NotBe("https");
                }
                else
                {
                    // URL should be invalid
                    isValidUri.Should().BeFalse();
                }
            }
        }
    }

    [Fact]
    public void Model_ID_Validation_Should_Work()
    {
        // Arrange
        var validModelIds = new[]
        {
            "prebuilt-invoice",
            "prebuilt-receipt",
            "prebuilt-document",
            "prebuilt-businessCard"
        };
        
        // Act & Assert
        foreach (var modelId in validModelIds)
        {
            modelId.Should().StartWith("prebuilt-");
            modelId.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public void File_Extension_Validation_Should_Work()
    {
        // Arrange
        var supportedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".tiff", ".tif" };
        
        // Act & Assert
        foreach (var extension in supportedExtensions)
        {
            extension.Should().StartWith(".");
            extension.Length.Should().BeGreaterThan(1);
        }
    }

    [Theory]
    [InlineData("document.pdf", ".pdf")]
    [InlineData("image.JPG", ".jpg")]
    [InlineData("file.JPEG", ".jpeg")]
    [InlineData("scan.png", ".png")]
    public void File_Extension_Extraction_Should_Work(string fileName, string expectedExtension)
    {
        // Act
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        
        // Assert
        extension.Should().Be(expectedExtension);
    }

    [Fact]
    public void Correlation_ID_Generation_Should_Work()
    {
        // Act
        var correlationId1 = Guid.NewGuid().ToString();
        var correlationId2 = Guid.NewGuid().ToString();
        
        // Assert
        correlationId1.Should().NotBeNullOrEmpty();
        correlationId2.Should().NotBeNullOrEmpty();
        correlationId1.Should().NotBe(correlationId2);
        correlationId1.Length.Should().Be(36); // Standard GUID string length
    }

    [Fact]
    public void DateTimeOffset_Should_Work_With_Utc()
    {
        // Arrange
        var before = DateTimeOffset.UtcNow;
        
        // Act
        System.Threading.Thread.Sleep(1);
        var after = DateTimeOffset.UtcNow;
        
        // Assert
        after.Should().BeAfter(before);
        after.Offset.Should().Be(TimeSpan.Zero); // UTC offset
    }

    [Fact]
    public void Base64_Encoding_Should_Work()
    {
        // Arrange
        var originalText = "Test document content";
        var bytes = System.Text.Encoding.UTF8.GetBytes(originalText);
        
        // Act
        var base64 = Convert.ToBase64String(bytes);
        var decodedBytes = Convert.FromBase64String(base64);
        var decodedText = System.Text.Encoding.UTF8.GetString(decodedBytes);
        
        // Assert
        base64.Should().NotBeNullOrEmpty();
        decodedText.Should().Be(originalText);
    }
}
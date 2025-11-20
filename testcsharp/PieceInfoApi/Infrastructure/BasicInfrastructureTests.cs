using Xunit;
using FluentAssertions;

namespace WarehouseReturns.PieceInfoApi.Tests.Infrastructure;

/// <summary>
/// Basic unit tests to validate test infrastructure and patterns
/// </summary>
public class BasicInfrastructureTests
{
    [Fact]
    public void Test_Infrastructure_Should_Be_Working()
    {
        // Arrange
        var testValue = "PieceInfo API Tests";
        
        // Act
        var result = testValue.ToUpperInvariant();
        
        // Assert
        result.Should().Be("PIECEINFO API TESTS");
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
    [InlineData("170080637")]
    [InlineData("ABC123")]
    [InlineData("test-value")]
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
        var items = new List<string> { "item1", "item2", "item3" };
        
        // Act
        var count = items.Count;
        var firstItem = items.FirstOrDefault();
        
        // Assert
        count.Should().Be(3);
        firstItem.Should().Be("item1");
        items.Should().Contain("item2");
        items.Should().NotContain("item4");
    }

    [Fact]
    public void Exception_Handling_Should_Work()
    {
        // Act & Assert
        Action act = () => throw new InvalidOperationException("Test exception");
        
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("Test exception");
    }
}
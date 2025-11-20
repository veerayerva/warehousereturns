# PieceInfo API Test Suite

This directory contains comprehensive unit and integration tests for the PieceInfo API.

## Test Structure

```
testcsharp/
├── Unit/                           # Unit tests
│   ├── SimpleExternalApiServiceTests.cs    # Tests for ExternalApiService
│   ├── SimpleAggregationServiceTests.cs    # Tests for AggregationService  
│   └── ModelTests.cs                       # Tests for data models
├── Integration/                    # Integration tests
│   └── PieceInfoFunctionsIntegrationTests.cs  # Tests for Azure Functions
├── Helpers/                        # Test utilities
│   └── TestHelpers.cs             # Mock helpers and test data
├── TestData/                       # Test data files
│   └── SampleApiResponses.json     # Sample API response data
└── PieceInfoApi.Tests.csproj      # Test project file
```

## Test Categories

### Unit Tests
- **ExternalApiService Tests**: Validates HTTP client functionality, parameter validation, and error handling
- **AggregationService Tests**: Tests data aggregation logic, error scenarios, and business rules
- **Model Tests**: Verifies data model initialization, property setting, and validation

### Integration Tests  
- **Function Tests**: Tests Azure Function endpoints, dependency injection, and request/response handling

## Running Tests

### Prerequisites
- .NET 8.0 SDK
- Visual Studio 2022 or VS Code with C# extension

### Command Line
```bash
# Navigate to test directory
cd testcsharp

# Restore packages
dotnet restore

# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test class
dotnet test --filter "ClassName=SimpleExternalApiServiceTests"

# Run tests with detailed output
dotnet test --verbosity detailed
```

### Visual Studio
1. Open the solution in Visual Studio
2. Build the solution (Ctrl+Shift+B)
3. Open Test Explorer (Test → Test Explorer)
4. Run All Tests or select specific tests

### VS Code
1. Install C# extension and .NET Test Explorer extension
2. Open the workspace
3. Use Ctrl+Shift+P → "Test: Run All Tests"

## Test Data

The tests use mock data defined in:
- `TestData/SampleApiResponses.json` - Sample external API responses
- `Helpers/TestHelpers.cs` - Mock HTTP clients and test data generators

## Coverage

The test suite aims for:
- **Unit Tests**: 80%+ code coverage
- **Integration Tests**: Happy path and critical error scenarios
- **Edge Cases**: Input validation, null checks, and error handling

## Test Patterns

### Arrange-Act-Assert (AAA)
All tests follow the AAA pattern for clarity:
```csharp
[Fact]
public void TestMethod_Condition_ExpectedResult()
{
    // Arrange
    var input = "test";
    
    // Act
    var result = MethodUnderTest(input);
    
    // Assert
    result.Should().NotBeNull();
}
```

### Mocking Strategy
- Use Moq for service dependencies
- Use MockHttp for HTTP client testing
- Use FluentAssertions for readable assertions

### Test Naming
- `MethodName_Condition_ExpectedResult`
- `ClassName_Scenario_ExpectedBehavior`

## Continuous Integration

These tests are designed to run in CI/CD pipelines:
- No external dependencies (mocked HTTP calls)
- Fast execution (< 30 seconds total)
- Clear failure messages
- Cross-platform compatible

## Future Enhancements

- Add performance tests
- Add contract tests for external APIs
- Add end-to-end tests with TestContainers
- Add mutation testing for test quality validation
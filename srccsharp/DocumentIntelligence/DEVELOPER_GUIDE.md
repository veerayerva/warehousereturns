# DocumentIntelligence API - Developer Guide

## 📖 Overview

This guide provides comprehensive information for developers working on the DocumentIntelligence API, including architecture details, coding standards, testing strategies, and deployment procedures.

## 🏗️ Architecture Deep Dive

### Project Structure

```
srccsharp/DocumentIntelligence/
├── Configuration/          # Application settings and configuration models
│   └── AppSettings.cs     # Typed configuration classes with validation
├── Functions/             # Azure Functions HTTP endpoints
│   └── DocumentIntelligenceFunctions.cs  # REST API implementation
├── Models/                # Request/Response models and DTOs
│   ├── AzureDocumentIntelligenceModels.cs  # Azure SDK model wrappers
│   ├── DocumentAnalysisRequestModels.cs   # API request models
│   ├── DocumentAnalysisResponseModels.cs  # API response models
│   ├── Enums.cs          # Enumeration definitions
│   ├── ErrorResponseModels.cs            # Error handling models
│   └── HealthCheckModels.cs              # Health monitoring models
├── Repositories/          # Data access layer
│   ├── BlobStorageRepository.cs          # Azure Blob Storage operations
│   └── IBlobStorageRepository.cs         # Repository interface
├── Services/             # Business logic layer
│   ├── DocumentIntelligenceService.cs   # Azure AI service integration
│   ├── DocumentProcessingService.cs     # Orchestration service
│   ├── IDocumentIntelligenceService.cs  # Service interface
│   └── IDocumentProcessingService.cs    # Service interface
├── Program.cs            # Application startup and dependency injection
├── host.json            # Azure Functions runtime configuration
├── local.settings.json  # Local development settings
└── README.md           # Project documentation
```

### Dependency Injection Container

The application uses Microsoft's built-in dependency injection container with the following service registrations:

```csharp
// Program.cs - Service Registration
services.Configure<DocumentIntelligenceSettings>(configuration.GetSection("DocumentIntelligence"));
services.Configure<DocumentProcessingSettings>(configuration.GetSection("DocumentProcessing"));
services.Configure<BlobStorageSettings>(configuration.GetSection("BlobStorage"));

services.AddScoped<IDocumentIntelligenceService, DocumentIntelligenceService>();
services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
services.AddScoped<IBlobStorageRepository, BlobStorageRepository>();

services.AddLogging(builder => builder.AddApplicationInsights());
services.AddApplicationInsightsTelemetry();
```

### Configuration Management

Configuration follows the ASP.NET Core options pattern with strongly-typed settings classes:

```csharp
// Configuration hierarchy
public class AppSettings
{
    public DocumentIntelligenceSettings DocumentIntelligence { get; set; }
    public DocumentProcessingSettings DocumentProcessing { get; set; }
    public BlobStorageSettings BlobStorage { get; set; }
    public LoggingSettings Logging { get; set; }
}

// Usage in services
public class DocumentIntelligenceService
{
    private readonly DocumentIntelligenceSettings _settings;
    
    public DocumentIntelligenceService(IOptions<DocumentIntelligenceSettings> settings)
    {
        _settings = settings.Value;
    }
}
```

## 🔧 Development Standards

### Coding Conventions

**Naming Conventions:**
- Classes: PascalCase (e.g., `DocumentIntelligenceService`)
- Methods: PascalCase (e.g., `AnalyzeDocumentAsync`)
- Properties: PascalCase (e.g., `DocumentUrl`)
- Fields: camelCase with underscore prefix (e.g., `_logger`)
- Constants: PascalCase (e.g., `MaxFileSize`)
- Enums: PascalCase for type and values (e.g., `DocumentType.ProductLabel`)

**File Organization:**
- One public class per file
- File name matches the class name
- Interfaces in separate files with 'I' prefix
- Related classes grouped in appropriate folders

**Code Style:**
```csharp
// ✅ Good: Comprehensive documentation
/// <summary>
/// Analyzes a document from a URL and extracts structured data using Azure Document Intelligence.
/// </summary>
/// <param name="request">Document analysis request containing URL and processing options</param>
/// <param name="correlationId">Unique identifier for request tracking and debugging</param>
/// <param name="cancellationToken">Token to cancel the operation if needed</param>
/// <returns>Analysis result with extracted fields and confidence scores</returns>
/// <exception cref="ArgumentNullException">Thrown when request or correlationId is null</exception>
/// <exception cref="HttpRequestException">Thrown when the document URL is not accessible</exception>
/// <exception cref="TimeoutException">Thrown when the operation exceeds the configured timeout</exception>
public async Task<(AzureDocumentIntelligenceResponse?, ErrorResponse?)> AnalyzeDocumentFromUrlAsync(
    DocumentAnalysisUrlRequest request,
    string correlationId,
    CancellationToken cancellationToken = default)
{
    // Input validation
    ArgumentNullException.ThrowIfNull(request, nameof(request));
    ArgumentException.ThrowIfNullOrWhiteSpace(correlationId, nameof(correlationId));
    
    using var activity = _activitySource.StartActivity("DocumentIntelligence.AnalyzeUrl");
    activity?.SetTag("correlation.id", correlationId);
    activity?.SetTag("document.url", request.DocumentUrl);
    
    try
    {
        _logger.LogInformation("Starting document analysis for URL: {DocumentUrl} [CorrelationId: {CorrelationId}]", 
            request.DocumentUrl, correlationId);
        
        // Implementation...
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Document analysis failed for URL: {DocumentUrl} [CorrelationId: {CorrelationId}]", 
            request.DocumentUrl, correlationId);
        throw;
    }
}
```

### Error Handling Strategy

**Exception Handling Hierarchy:**
1. **Input Validation**: ArgumentNullException, ArgumentException
2. **Business Logic**: Custom domain exceptions
3. **External Service**: HttpRequestException, TimeoutException
4. **Infrastructure**: Azure service exceptions

**Error Response Format:**
```csharp
public class ErrorResponse
{
    public ErrorCode ErrorCode { get; set; }
    public string Message { get; set; }
    public string Details { get; set; }
    public string CorrelationId { get; set; }
    public DateTime Timestamp { get; set; }
    public List<ValidationError> ValidationErrors { get; set; }
}
```

**Logging Strategy:**
```csharp
// Structured logging with correlation
_logger.LogInformation("Document processing started: {DocumentType} Size: {FileSize}MB [CorrelationId: {CorrelationId}]",
    request.DocumentType, fileSizeInMB, correlationId);

// Error logging with context
_logger.LogError(ex, "Document Intelligence API failed: {ErrorMessage} [CorrelationId: {CorrelationId}] [DocumentUrl: {DocumentUrl}]",
    ex.Message, correlationId, request.DocumentUrl);

// Performance logging
using var stopwatch = Stopwatch.StartNew();
// ... operation ...
_logger.LogInformation("Document analysis completed in {ElapsedMs}ms [CorrelationId: {CorrelationId}]",
    stopwatch.ElapsedMilliseconds, correlationId);
```

## 🧪 Testing Strategy

### Test Architecture

The test suite is organized into multiple layers matching the application architecture:

```
testcsharp/DocumentIntelligence/
├── Models/                    # Model validation tests
│   └── ModelValidationTests.cs
├── Functions/                 # HTTP endpoint tests
│   └── DocumentIntelligenceFunctionsTests.cs
├── Services/                  # Business logic tests
│   └── ServicesTests.cs
├── Repositories/              # Data access tests
│   └── RepositoriesTests.cs
├── Infrastructure/            # Integration tests
│   └── InfrastructureTests.cs
├── Helpers/                   # Test utilities
│   └── TestHelpers.cs
└── TestData/                  # Sample data
    └── SampleApiResponses.json
```

### Testing Patterns

**Unit Test Example:**
```csharp
[Fact]
public async Task AnalyzeDocumentFromUrlAsync_WithValidRequest_ReturnsSuccessfulResponse()
{
    // Arrange
    var mockLogger = new Mock<ILogger<DocumentIntelligenceService>>();
    var mockSettings = CreateMockSettings();
    var service = new DocumentIntelligenceService(mockSettings, mockLogger.Object);
    
    var request = new DocumentAnalysisUrlRequest
    {
        DocumentUrl = "https://example.com/test-document.pdf",
        DocumentType = DocumentType.Invoice,
        ConfidenceThreshold = 0.8
    };
    
    // Act
    var (response, error) = await service.AnalyzeDocumentFromUrlAsync(request, "TEST-CORR-001");
    
    // Assert
    response.Should().NotBeNull();
    error.Should().BeNull();
    response.Status.Should().Be(AnalysisStatus.Succeeded);
    response.SerialField.Should().NotBeNull();
    response.SerialField.Confidence.Should().BeGreaterThan(0.8);
}
```

**Integration Test Example:**
```csharp
[Fact]
public async Task ProcessDocument_EndToEnd_WithRealAzureServices()
{
    // Arrange - requires actual Azure resources for integration testing
    var host = new TestHost();
    var client = host.GetTestClient();
    
    var request = new DocumentAnalysisUrlRequest
    {
        DocumentUrl = TestData.SampleInvoiceUrl,
        DocumentType = DocumentType.Invoice
    };
    
    // Act
    var response = await client.PostAsJsonAsync("/api/process-document-url", request);
    
    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.OK);
    var result = await response.Content.ReadFromJsonAsync<DocumentAnalysisResponse>();
    result.Should().NotBeNull();
    result.Status.Should().Be(AnalysisStatus.Succeeded);
}
```

**Mock Helper Utilities:**
```csharp
public static class TestHelpers
{
    public static DocumentAnalysisUrlRequest CreateValidUrlRequest(
        string url = "https://example.com/test.pdf",
        DocumentType type = DocumentType.General)
    {
        return new DocumentAnalysisUrlRequest
        {
            DocumentUrl = url,
            DocumentType = type,
            ConfidenceThreshold = 0.8,
            CorrelationId = Guid.NewGuid().ToString()
        };
    }
    
    public static Mock<ILogger<T>> CreateMockLogger<T>()
    {
        return new Mock<ILogger<T>>();
    }
}
```

### Test Data Management

**Sample Data Organization:**
```json
// TestData/SampleApiResponses.json
{
  "successful_invoice_analysis": {
    "status": "succeeded",
    "serial_field": {
      "value": "INV-2024-001234",
      "confidence": 0.95,
      "status": "extracted"
    }
  },
  "low_confidence_result": {
    "status": "succeeded", 
    "serial_field": {
      "value": "unclear-text",
      "confidence": 0.45,
      "status": "low_confidence"
    }
  }
}
```

## 🚀 Deployment Pipeline

### Build Configuration

**Project File Key Settings:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <AzureFunctionsVersion>v4</AzureFunctionsVersion>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningsAsErrors />
    <WarningsNotAsErrors>CS1591</WarningsNotAsErrors>
  </PropertyGroup>
  
  <PropertyGroup Condition="'$(Configuration)'=='Release'">
    <Optimize>true</Optimize>
    <DebugType>portable</DebugType>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
</Project>
```

### Environment Configuration

**Development Environment:**
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "DOCUMENT_INTELLIGENCE_ENDPOINT": "https://dev-doc-intel.cognitiveservices.azure.com/",
    "DOCUMENT_INTELLIGENCE_KEY": "dev-key-from-keyvault",
    "CONFIDENCE_THRESHOLD": "0.6",
    "ENABLE_BLOB_STORAGE": "false",
    "LOG_LEVEL": "Debug"
  },
  "ConnectionStrings": {
    "ApplicationInsights": "InstrumentationKey=dev-key"
  }
}
```

**Production Environment:**
```json
{
  "IsEncrypted": true,
  "Values": {
    "AzureWebJobsStorage": "@Microsoft.KeyVault(VaultName=prod-keyvault;SecretName=storage-connection)",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "DOCUMENT_INTELLIGENCE_ENDPOINT": "@Microsoft.KeyVault(VaultName=prod-keyvault;SecretName=doc-intel-endpoint)",
    "DOCUMENT_INTELLIGENCE_KEY": "@Microsoft.KeyVault(VaultName=prod-keyvault;SecretName=doc-intel-key)",
    "CONFIDENCE_THRESHOLD": "0.8",
    "ENABLE_BLOB_STORAGE": "true",
    "LOG_LEVEL": "Information"
  }
}
```

## 🔒 Security Guidelines

### Input Validation

**Request Validation:**
```csharp
[Required]
[Url(ErrorMessage = "DocumentUrl must be a valid HTTP or HTTPS URL")]
public string DocumentUrl { get; set; }

[Range(0.0, 1.0, ErrorMessage = "ConfidenceThreshold must be between 0.0 and 1.0")]
public double? ConfidenceThreshold { get; set; }

[StringLength(255, MinimumLength = 1, ErrorMessage = "Filename must be between 1 and 255 characters")]
public string Filename { get; set; }
```

**Content Validation:**
```csharp
// File type validation
private readonly string[] _allowedContentTypes = 
{
    "application/pdf",
    "image/jpeg", 
    "image/png",
    "image/tiff"
};

// Size limit validation
private const long MaxFileSizeBytes = 100 * 1024 * 1024; // 100MB

public bool ValidateFileUpload(byte[] content, string contentType, string filename)
{
    if (content.Length > MaxFileSizeBytes)
        throw new ArgumentException($"File size exceeds maximum limit of {MaxFileSizeBytes / 1024 / 1024}MB");
        
    if (!_allowedContentTypes.Contains(contentType.ToLowerInvariant()))
        throw new ArgumentException($"Content type '{contentType}' is not supported");
        
    // Additional security validations...
}
```

### Secret Management

**Azure Key Vault Integration:**
```csharp
// appsettings.json for Key Vault reference
{
  "DocumentIntelligence": {
    "Endpoint": "@Microsoft.KeyVault(VaultName=my-keyvault;SecretName=doc-intel-endpoint)",
    "Key": "@Microsoft.KeyVault(VaultName=my-keyvault;SecretName=doc-intel-key)"
  }
}

// Managed Identity configuration
services.AddAzureAppConfiguration(options =>
{
    options.Connect(connectionString)
           .ConfigureKeyVault(kv =>
           {
               kv.SetCredential(new DefaultAzureCredential());
           });
});
```

## 📊 Monitoring & Observability

### Application Insights Integration

**Custom Telemetry:**
```csharp
public class DocumentIntelligenceService
{
    private readonly TelemetryClient _telemetryClient;
    
    public async Task<DocumentAnalysisResponse> ProcessDocumentAsync(DocumentAnalysisRequest request)
    {
        using var operation = _telemetryClient.StartOperation<DependencyTelemetry>("DocumentIntelligence.ProcessDocument");
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Process document...
            
            // Track success metrics
            _telemetryClient.TrackMetric("DocumentProcessing.Duration", stopwatch.ElapsedMilliseconds);
            _telemetryClient.TrackMetric("DocumentProcessing.ConfidenceScore", result.Confidence);
            
            operation.Telemetry.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            _telemetryClient.TrackException(ex);
            operation.Telemetry.Success = false;
            throw;
        }
    }
}
```

**Custom Metrics:**
```csharp
// Performance metrics
_telemetryClient.TrackMetric("Document.ProcessingTime", processingTimeMs, 
    new Dictionary<string, string> 
    {
        ["DocumentType"] = request.DocumentType.ToString(),
        ["FileSize"] = fileSizeCategory,
        ["Region"] = Environment.GetEnvironmentVariable("REGION_NAME")
    });

// Business metrics  
_telemetryClient.TrackMetric("Document.ConfidenceScore", confidence,
    new Dictionary<string, string>
    {
        ["ModelId"] = modelId,
        ["DocumentType"] = documentType
    });

// Error tracking
_telemetryClient.TrackException(ex,
    new Dictionary<string, string>
    {
        ["CorrelationId"] = correlationId,
        ["Operation"] = "DocumentAnalysis",
        ["DocumentUrl"] = request.DocumentUrl
    });
```

### Health Monitoring

**Health Check Implementation:**
```csharp
public async Task<HealthCheckResponse> CheckHealthAsync(CancellationToken cancellationToken)
{
    var healthChecks = new List<ComponentHealthCheck>();
    var overallStatus = HealthStatus.Healthy;
    var stopwatch = Stopwatch.StartNew();
    
    // Check Azure Document Intelligence
    var docIntelHealth = await CheckDocumentIntelligenceHealthAsync(cancellationToken);
    healthChecks.Add(docIntelHealth);
    
    // Check Blob Storage
    var storageHealth = await CheckBlobStorageHealthAsync(cancellationToken);
    healthChecks.Add(storageHealth);
    
    // Determine overall status
    if (healthChecks.Any(h => h.Status == HealthStatus.Unhealthy))
        overallStatus = HealthStatus.Unhealthy;
    else if (healthChecks.Any(h => h.Status == HealthStatus.Degraded))
        overallStatus = HealthStatus.Degraded;
    
    return new HealthCheckResponse
    {
        Status = overallStatus,
        Timestamp = DateTime.UtcNow,
        TotalResponseTimeMs = stopwatch.ElapsedMilliseconds,
        HealthChecks = healthChecks
    };
}
```

## 🔧 Troubleshooting Guide

### Common Issues and Solutions

**Azure Function Startup Issues:**
```bash
# Issue: Functions fail to start with dependency injection errors
# Solution: Verify service registration in Program.cs

# Check if all required services are registered
grep -r "AddScoped\|AddSingleton\|AddTransient" Program.cs

# Verify configuration binding
grep -r "Configure<.*Settings>" Program.cs
```

**Performance Issues:**
```bash
# Issue: Slow response times
# Check Application Insights for bottlenecks

# Query for slow requests
requests 
| where name == "ProcessDocument"
| where duration > 30000  // > 30 seconds
| summarize count() by bin(timestamp, 1h), resultCode
| order by timestamp desc
```

**Memory Issues:**
```bash
# Issue: High memory usage or OutOfMemoryException
# Monitor memory usage patterns

performanceCounters
| where category == "Memory" 
| where counter == "Available MBytes"
| summarize avg(value) by bin(timestamp, 5m)
| order by timestamp desc
```

### Debugging Techniques

**Local Debugging:**
```csharp
// Enable detailed logging for debugging
public void Configure(ILoggingBuilder logging)
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Debug);
    
    // Enable Azure SDK logging
    logging.AddFilter("Azure", LogLevel.Debug);
}

// Use correlation IDs for tracing
public async Task ProcessAsync(string correlationId)
{
    using var scope = _logger.BeginScope(new Dictionary<string, object>
    {
        ["CorrelationId"] = correlationId,
        ["Operation"] = "DocumentProcessing"
    });
    
    _logger.LogDebug("Processing started");
    // ... processing logic
    _logger.LogDebug("Processing completed");
}
```

---

## 📚 Additional Resources

- [Azure Functions .NET Documentation](https://docs.microsoft.com/azure/azure-functions/functions-dotnet-class-library)
- [Azure Document Intelligence SDK](https://docs.microsoft.com/azure/applied-ai-services/form-recognizer/)
- [.NET 8.0 Documentation](https://docs.microsoft.com/dotnet/core/whats-new/dotnet-8)
- [xUnit Testing Framework](https://xunit.net/docs/getting-started/netcore/cmdline)
- [Application Insights for .NET](https://docs.microsoft.com/azure/azure-monitor/app/asp-net-core)

**Internal Links:**
- [API Reference Documentation](./api-reference.md)
- [Security Policies](./SECURITY.md)
- [Contributing Guidelines](./CONTRIBUTING.md)
- [Deployment Runbook](./deployment-runbook.md)
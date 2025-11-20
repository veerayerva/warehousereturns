using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Text.Json;
using WarehouseReturns.DocumentIntelligence.Models;

namespace WarehouseReturns.DocumentIntelligence.Tests.Helpers
{
    /// <summary>
    /// Test helper utilities for DocumentIntelligence testing.
    /// Provides mock objects, test data creation, and common test utilities.
    /// </summary>
    public static class TestHelpers
    {
        /// <summary>
        /// Mock logger helper for testing logging functionality
        /// </summary>
        public static class TestLoggerHelper
        {
            public static Mock<ILogger<T>> CreateMockLogger<T>()
            {
                return new Mock<ILogger<T>>();
            }
            
            public static ILogger<T> CreateLogger<T>()
            {
                return CreateMockLogger<T>().Object;
            }
        }

        /// <summary>
        /// Test data creation helper for generating test objects
        /// </summary>
        public static class TestDataHelper
        {
            /// <summary>
            /// Create a valid DocumentAnalysisUrlRequest test object
            /// </summary>
            public static DocumentAnalysisUrlRequest CreateValidUrlRequest(
                string documentUrl = "https://example.com/document.pdf",
                DocumentType documentType = DocumentType.General,
                double? confidenceThreshold = 0.8)
            {
                return new DocumentAnalysisUrlRequest
                {
                    DocumentUrl = documentUrl,
                    DocumentType = documentType,
                    ConfidenceThreshold = confidenceThreshold
                };
            }

            /// <summary>
            /// Create a valid DocumentAnalysisFileRequest test object
            /// </summary>
            public static DocumentAnalysisFileRequest CreateValidFileRequest(
                string filename = "test.pdf",
                string contentType = "application/pdf",
                DocumentType documentType = DocumentType.General,
                double? confidenceThreshold = 0.8)
            {
                return new DocumentAnalysisFileRequest
                {
                    FileContent = new byte[] { 0x25, 0x50, 0x44, 0x46 }, // PDF header
                    Filename = filename,
                    ContentType = contentType,
                    DocumentType = documentType,
                    ConfidenceThreshold = confidenceThreshold
                };
            }

            /// <summary>
            /// Create a sample DocumentAnalysisResponse for testing
            /// </summary>
            public static DocumentAnalysisResponse CreateSampleResponse(
                string analysisId = "test-analysis-123",
                AnalysisStatus status = AnalysisStatus.Succeeded)
            {
                return new DocumentAnalysisResponse
                {
                    AnalysisId = analysisId,
                    Status = status,
                    CorrelationId = "test-correlation-456",
                    SerialField = new SerialFieldResult
                    {
                        Value = "SN123456789",
                        Confidence = 0.95,
                        Status = FieldExtractionStatus.Extracted,
                        ConfidenceAcceptable = true
                    },
                    AnalysisMetadata = new AnalysisMetadata
                    {
                        ModelId = "prebuilt-document",
                        DocumentType = DocumentType.SerialNumber,
                        ConfidenceThreshold = 0.8,
                        PageCount = 1,
                        SourceInfo = new SourceInformation
                        {
                            Source = "test-document.pdf",
                            ContentType = "application/pdf",
                            FileSize = 1024,
                            ProcessingMethod = "upload"
                        }
                    },
                    StorageInfo = new StorageInformation
                    {
                        Stored = false
                    },
                    ProcessingTimeMs = 1500,
                    Timestamp = DateTime.UtcNow
                };
            }

            /// <summary>
            /// Create an ErrorResponse for testing error scenarios
            /// </summary>
            public static ErrorResponse CreateErrorResponse(
                ErrorCode errorCode = ErrorCode.ProcessingError,
                string message = "Test error message")
            {
                return new ErrorResponse
                {
                    ErrorCode = errorCode,
                    Message = message,
                    CorrelationId = "error-correlation-123"
                };
            }

            /// <summary>
            /// Create a HealthCheckResponse for testing health endpoints
            /// </summary>
            public static HealthCheckResponse CreateHealthResponse(
                HealthStatus status = HealthStatus.Healthy)
            {
                return new HealthCheckResponse
                {
                    Status = status,
                    Timestamp = DateTime.UtcNow
                };
            }

            /// <summary>
            /// Load sample API responses from JSON test data
            /// </summary>
            public static T LoadSampleResponse<T>(string responseType)
            {
                try
                {
                    var jsonPath = Path.Combine("TestData", "SampleApiResponses.json");
                    if (!File.Exists(jsonPath))
                    {
                        throw new FileNotFoundException($"Test data file not found: {jsonPath}");
                    }

                    var json = File.ReadAllText(jsonPath);
                    var document = JsonDocument.Parse(json);
                    
                    var responseJson = document.RootElement
                        .GetProperty(responseType)
                        .GetRawText();
                    
                    return JsonSerializer.Deserialize<T>(responseJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    })!;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to load sample response '{responseType}': {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Validation helpers for testing input validation
        /// </summary>
        public static class ValidationHelper
        {
            public static bool IsValidUrl(string url)
            {
                return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                       (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            }

            public static bool IsValidContentType(string contentType)
            {
                var validTypes = new[]
                {
                    "application/pdf",
                    "image/jpeg",
                    "image/png",
                    "image/tiff"
                };
                return validTypes.Contains(contentType);
            }

            public static bool IsValidConfidenceThreshold(double? threshold)
            {
                return !threshold.HasValue || (threshold >= 0.0 && threshold <= 1.0);
            }
        }
    }
}
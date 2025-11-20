using Xunit;
using FluentAssertions;
using WarehouseReturns.DocumentIntelligence.Models;

namespace WarehouseReturns.DocumentIntelligence.Tests.Models
{
    /// <summary>
    /// Test suite for DocumentIntelligence model validation and functionality.
    /// Ensures all model classes work correctly and have proper validation.
    /// </summary>
    public class ModelValidationTests
    {
        [Fact]
        public void DocumentAnalysisUrlRequest_DefaultValues_ShouldBeValid()
        {
            // Arrange & Act
            var request = new DocumentAnalysisUrlRequest
            {
                DocumentUrl = "https://example.com/test-document.pdf"
            };

            // Assert
            request.DocumentUrl.Should().Be("https://example.com/test-document.pdf");
            request.DocumentType.Should().Be(DocumentType.General);
            request.ModelId.Should().BeNull();
            request.ConfidenceThreshold.Should().BeNull();
            request.CorrelationId.Should().BeNull();
        }

        [Fact]
        public void DocumentAnalysisFileRequest_DefaultValues_ShouldBeValid()
        {
            // Arrange & Act
            var request = new DocumentAnalysisFileRequest
            {
                FileContent = new byte[] { 0x01, 0x02, 0x03 },
                Filename = "test.pdf",
                ContentType = "application/pdf"
            };

            // Assert
            request.FileContent.Should().NotBeEmpty();
            request.Filename.Should().Be("test.pdf");
            request.ContentType.Should().Be("application/pdf");
            request.DocumentType.Should().Be(DocumentType.General);
            request.MaxFileSizeMb.Should().Be(10);
            request.AllowedContentTypes.Should().Contain("application/pdf");
        }

        [Fact]
        public void DocumentAnalysisResponse_Creation_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var response = new DocumentAnalysisResponse();

            // Assert
            response.AnalysisId.Should().Be(string.Empty);
            response.Status.Should().Be(AnalysisStatus.Submitted);
            response.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            response.ProcessingTimeMs.Should().Be(0);
        }

        [Fact]
        public void SerialFieldResult_Creation_ShouldHaveDefaultValues()
        {
            // Arrange & Act
            var field = new SerialFieldResult
            {
                Value = "SN123456",
                Confidence = 0.95,
                Status = FieldExtractionStatus.Extracted
            };

            // Assert
            field.Value.Should().Be("SN123456");
            field.Confidence.Should().Be(0.95);
            field.Status.Should().Be(FieldExtractionStatus.Extracted);
            field.ConfidenceAcceptable.Should().BeFalse(); // Default value
        }

        [Fact]
        public void AnalysisMetadata_Creation_ShouldAllowConfiguration()
        {
            // Arrange & Act
            var metadata = new AnalysisMetadata
            {
                ModelId = "prebuilt-document",
                DocumentType = DocumentType.SerialNumber,
                ConfidenceThreshold = 0.8,
                PageCount = 1
            };

            // Assert
            metadata.ModelId.Should().Be("prebuilt-document");
            metadata.DocumentType.Should().Be(DocumentType.SerialNumber);
            metadata.ConfidenceThreshold.Should().Be(0.8);
            metadata.PageCount.Should().Be(1);
        }

        [Fact]
        public void DocumentType_Enum_ShouldHaveCorrectValues()
        {
            // Assert
            DocumentType.SerialNumber.Should().BeDefined();
            DocumentType.ProductLabel.Should().BeDefined();
            DocumentType.General.Should().BeDefined();
        }

        [Fact]
        public void AnalysisStatus_Enum_ShouldHaveCorrectValues()
        {
            // Assert
            AnalysisStatus.Submitted.Should().BeDefined();
            AnalysisStatus.Processing.Should().BeDefined();
            AnalysisStatus.Succeeded.Should().BeDefined();
            AnalysisStatus.Failed.Should().BeDefined();
            AnalysisStatus.RequiresReview.Should().BeDefined();
        }

        [Fact]
        public void FieldExtractionStatus_Enum_ShouldHaveCorrectValues()
        {
            // Assert
            FieldExtractionStatus.Extracted.Should().BeDefined();
            FieldExtractionStatus.LowConfidence.Should().BeDefined();
            FieldExtractionStatus.NotFound.Should().BeDefined();
            FieldExtractionStatus.ExtractionError.Should().BeDefined();
        }

        [Fact]
        public void ErrorCode_Enum_ShouldHaveCorrectValues()
        {
            // Assert
            ErrorCode.InvalidRequest.Should().BeDefined();
            ErrorCode.FileValidationError.Should().BeDefined();
            ErrorCode.AzureServiceError.Should().BeDefined();
            ErrorCode.BlobStorageError.Should().BeDefined();
            ErrorCode.ProcessingError.Should().BeDefined();
        }

        [Theory]
        [InlineData("https://example.com/test.pdf")]
        [InlineData("https://storage.azure.com/documents/invoice.pdf")]
        public void DocumentAnalysisUrlRequest_WithValidUrl_ShouldAcceptUrl(string url)
        {
            // Arrange & Act
            var request = new DocumentAnalysisUrlRequest
            {
                DocumentUrl = url
            };

            // Assert
            request.DocumentUrl.Should().Be(url);
        }

        [Theory]
        [InlineData("application/pdf")]
        [InlineData("image/jpeg")]
        [InlineData("image/png")]
        public void DocumentAnalysisFileRequest_WithValidContentType_ShouldAcceptType(string contentType)
        {
            // Arrange & Act
            var request = new DocumentAnalysisFileRequest
            {
                FileContent = new byte[] { 0x01 },
                Filename = "test.pdf",
                ContentType = contentType
            };

            // Assert
            request.ContentType.Should().Be(contentType);
        }

        [Fact]
        public void StorageInformation_WhenDocumentStored_ShouldHaveStorageDetails()
        {
            // Arrange & Act
            var storage = new StorageInformation
            {
                Stored = true,
                ContainerName = "documents",
                BlobName = "test/document.pdf",
                StorageReason = "low_confidence",
                StorageTimestamp = DateTime.UtcNow
            };

            // Assert
            storage.Stored.Should().BeTrue();
            storage.ContainerName.Should().Be("documents");
            storage.BlobName.Should().Be("test/document.pdf");
            storage.StorageReason.Should().Be("low_confidence");
            storage.StorageTimestamp.Should().NotBeNull();
        }

        [Fact]
        public void SourceInformation_WithFileUpload_ShouldTrackSource()
        {
            // Arrange & Act
            var source = new SourceInformation
            {
                Source = "uploaded-document.pdf",
                ContentType = "application/pdf",
                FileSize = 1024,
                ProcessingMethod = "upload"
            };

            // Assert
            source.Source.Should().Be("uploaded-document.pdf");
            source.ContentType.Should().Be("application/pdf");
            source.FileSize.Should().Be(1024);
            source.ProcessingMethod.Should().Be("upload");
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(0.5)]
        [InlineData(0.95)]
        [InlineData(1.0)]
        public void ConfidenceThreshold_WithValidValue_ShouldAcceptValue(double confidence)
        {
            // Arrange & Act
            var request = new DocumentAnalysisUrlRequest
            {
                DocumentUrl = "https://example.com/test.pdf",
                ConfidenceThreshold = confidence
            };

            // Assert
            request.ConfidenceThreshold.Should().Be(confidence);
        }

        [Fact]
        public void DocumentAnalysisResponse_CompleteExample_ShouldHaveCorrectStructure()
        {
            // Arrange & Act
            var response = new DocumentAnalysisResponse
            {
                AnalysisId = "analysis-456",
                Status = AnalysisStatus.Succeeded,
                CorrelationId = "corr-123",
                SerialField = new SerialFieldResult
                {
                    Value = "SN123456",
                    Confidence = 0.95,
                    Status = FieldExtractionStatus.Extracted,
                    ConfidenceAcceptable = true
                },
                AnalysisMetadata = new AnalysisMetadata
                {
                    ModelId = "prebuilt-document",
                    DocumentType = DocumentType.SerialNumber,
                    ConfidenceThreshold = 0.8,
                    PageCount = 1
                },
                ProcessingTimeMs = 1500
            };

            // Assert
            response.AnalysisId.Should().Be("analysis-456");
            response.Status.Should().Be(AnalysisStatus.Succeeded);
            response.SerialField.Should().NotBeNull();
            response.SerialField!.Value.Should().Be("SN123456");
            response.AnalysisMetadata.Should().NotBeNull();
            response.ProcessingTimeMs.Should().Be(1500);
        }
    }
}
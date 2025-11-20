using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using WarehouseReturns.DocumentIntelligence.Configuration;
using WarehouseReturns.DocumentIntelligence.Models;

namespace WarehouseReturns.DocumentIntelligence.Repositories;

/// <summary>
/// Repository for Azure Blob Storage operations
/// 
/// Manages storage of low-confidence documents for manual review and retraining.
/// Handles container management, metadata storage, and blob lifecycle operations.
/// </summary>
public class BlobStorageRepository : IBlobStorageRepository
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<BlobStorageRepository> _logger;
    private readonly BlobStorageSettings _settings;

    public BlobStorageRepository(
        IOptions<BlobStorageSettings> settings,
        ILogger<BlobStorageRepository> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        try
        {
            _blobServiceClient = new BlobServiceClient(_settings.AZURE_STORAGE_CONNECTION_STRING);
            _containerClient = _blobServiceClient.GetBlobContainerClient(_settings.BLOB_CONTAINER_PREFIX);

            _logger.LogInformation(
                "[BLOB-STORAGE-INIT] Blob storage repository initialized successfully - " +
                "Container: {ContainerName}, Storage-Account: {StorageAccount}",
                _settings.BLOB_CONTAINER_PREFIX,
                GetStorageAccountName());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "[BLOB-STORAGE-INIT] Failed to initialize blob storage repository - " +
                "Container: {ContainerName}",
                _settings.BLOB_CONTAINER_PREFIX);
            throw;
        }
    }

    /// <summary>
    /// Store a low-confidence document in blob storage for review
    /// </summary>
    public async Task<StorageInformation> StoreLowConfidenceDocumentAsync(
        string analysisId,
        byte[] documentBytes,
        string filename,
        string contentType,
        SerialFieldResult serialField,
        Dictionary<string, string> metadata,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "[BLOB-STORAGE-STORE] Starting document storage - Analysis-ID: {AnalysisId}, " +
                "Filename: {Filename}, Size: {FileSize} bytes, Serial-Value: {SerialValue}, " +
                "Confidence: {Confidence}, Correlation-ID: {CorrelationId}",
                analysisId,
                filename,
                documentBytes.Length,
                serialField.Value ?? "null",
                serialField.Confidence,
                correlationId);

            // Ensure container exists
            var containerExists = await EnsureContainerExistsAsync(cancellationToken);
            if (!containerExists)
            {
                throw new InvalidOperationException($"Failed to create or access blob container '{_settings.BLOB_CONTAINER_PREFIX}'. Check storage account configuration and permissions.");
            }

            // Generate blob name with timestamp and analysis ID
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var sanitizedFilename = SanitizeFilename(filename);
            var blobName = $"low-confidence/{timestamp}/{analysisId}_{sanitizedFilename}";

            // Prepare blob metadata
            var blobMetadata = new Dictionary<string, string>
            {
                { "analysis_id", analysisId },
                { "correlation_id", correlationId },
                { "original_filename", filename },
                { "content_type", contentType },
                { "serial_value", serialField.Value ?? "null" },
                { "serial_confidence", serialField.Confidence.ToString("F4") },
                { "serial_status", serialField.Status.ToString() },
                { "upload_timestamp", DateTime.UtcNow.ToString("O") },
                { "storage_reason", "low_confidence" }
            };

            // Add custom metadata
            foreach (var kvp in metadata)
            {
                var key = SanitizeMetadataKey($"custom_{kvp.Key}");
                blobMetadata[key] = kvp.Value;
            }

            // Upload document to blob storage
            var blobClient = _containerClient.GetBlobClient(blobName);
            
            using var stream = new MemoryStream(documentBytes);
            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType
                },
                Metadata = blobMetadata,
                Tags = new Dictionary<string, string>
                {
                    { "confidence_level", "low" },
                    { "requires_review", "true" },
                    { "analysis_date", DateTime.UtcNow.ToString("yyyy-MM-dd") }
                }
            };

            var response = await blobClient.UploadAsync(stream, uploadOptions, cancellationToken);

            // Create metadata JSON file
            var metadataJson = CreateMetadataJson(analysisId, filename, contentType, serialField, metadata, correlationId);
            var metadataBlobName = $"metadata/{timestamp}/{analysisId}_metadata.json";
            var metadataBlobClient = _containerClient.GetBlobClient(metadataBlobName);
            
            using var metadataStream = new MemoryStream(Encoding.UTF8.GetBytes(metadataJson));
            await metadataBlobClient.UploadAsync(
                metadataStream,
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" },
                    Metadata = new Dictionary<string, string>
                    {
                        { "analysis_id", analysisId },
                        { "type", "metadata" },
                        { "created_timestamp", DateTime.UtcNow.ToString("O") }
                    }
                },
                cancellationToken);

            var storageInfo = new StorageInformation
            {
                Stored = true,
                ContainerName = _settings.BLOB_CONTAINER_PREFIX,
                BlobName = blobName,
                StorageReason = "low_confidence",
                StorageTimestamp = DateTime.UtcNow,
                BlobMetadata = blobMetadata
            };

            _logger.LogInformation(
                "[BLOB-STORAGE-SUCCESS] Document stored successfully - Blob-Name: {BlobName}, " +
                "Container: {ContainerName}, ETag: {ETag}, Correlation-ID: {CorrelationId}",
                blobName,
                _settings.BLOB_CONTAINER_PREFIX,
                response.Value.ETag,
                correlationId);

            return storageInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[BLOB-STORAGE-ERROR] Failed to store document - Analysis-ID: {AnalysisId}, " +
                "Filename: {Filename}, Correlation-ID: {CorrelationId}",
                analysisId,
                filename,
                correlationId);

            return new StorageInformation
            {
                Stored = false,
                ContainerName = _settings.BLOB_CONTAINER_PREFIX,
                StorageReason = $"storage_error: {ex.Message}",
                StorageTimestamp = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Ensure the blob container exists and is properly configured
    /// </summary>
    public async Task<bool> EnsureContainerExistsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "[BLOB-CONTAINER-CHECK] Checking if container exists - Container: {ContainerName}",
                _settings.BLOB_CONTAINER_PREFIX);

            // First check if container already exists
            var existsResponse = await _containerClient.ExistsAsync(cancellationToken);
            if (existsResponse.Value)
            {
                _logger.LogInformation(
                    "[BLOB-CONTAINER-EXISTS] Container already exists - Container: {ContainerName}",
                    _settings.BLOB_CONTAINER_PREFIX);
                return true;
            }

            // Create container if it doesn't exist
            _logger.LogInformation(
                "[BLOB-CONTAINER-CREATE] Creating container - Container: {ContainerName}",
                _settings.BLOB_CONTAINER_PREFIX);

            var response = await _containerClient.CreateIfNotExistsAsync(
                PublicAccessType.None,
                metadata: new Dictionary<string, string>
                {
                    { "purpose", "warehouse_returns_document_intelligence" },
                    { "created", DateTime.UtcNow.ToString("O") },
                    { "version", "1.0" }
                },
                cancellationToken: cancellationToken);

            if (response != null)
            {
                _logger.LogInformation(
                    "[BLOB-CONTAINER-CREATED] Container created successfully - " +
                    "Container: {ContainerName}",
                    _settings.BLOB_CONTAINER_PREFIX);
            }
            else
            {
                _logger.LogInformation(
                    "[BLOB-CONTAINER-ALREADY-EXISTS] Container already existed during creation attempt - " +
                    "Container: {ContainerName}",
                    _settings.BLOB_CONTAINER_PREFIX);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[BLOB-CONTAINER-ERROR] Failed to ensure container exists - " +
                "Container: {ContainerName}, Error: {ErrorMessage}",
                _settings.BLOB_CONTAINER_PREFIX,
                ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Get blob storage health status
    /// </summary>
    public async Task<Dictionary<string, object>> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        var healthCheck = new Dictionary<string, object>
        {
            { "service", "Blob Storage" },
            { "status", "unknown" },
            { "timestamp", DateTime.UtcNow },
            { "container_name", _settings.BLOB_CONTAINER_PREFIX },
            { "storage_account", GetStorageAccountName() }
        };

        try
        {
            // Test connectivity by checking if container exists
            var exists = await _containerClient.ExistsAsync(cancellationToken);
            
            if (exists.Value)
            {
                // Get container properties to verify access
                var properties = await _containerClient.GetPropertiesAsync(cancellationToken: cancellationToken);
                
                healthCheck["status"] = "healthy";
                healthCheck["container_exists"] = true;
                healthCheck["last_modified"] = properties.Value.LastModified;
                healthCheck["message"] = "Container is accessible and configured correctly";
            }
            else
            {
                healthCheck["status"] = "degraded";
                healthCheck["container_exists"] = false;
                healthCheck["message"] = "Container does not exist but can be created";
            }

            _logger.LogInformation("Blob storage health check completed successfully");
        }
        catch (Exception ex)
        {
            healthCheck["status"] = "unhealthy";
            healthCheck["error"] = ex.Message;
            healthCheck["message"] = "Unable to access blob storage";
            
            _logger.LogError(ex, "Blob storage health check failed");
        }

        return healthCheck;
    }

    /// <summary>
    /// Get storage account name from connection string
    /// </summary>
    private string GetStorageAccountName()
    {
        try
        {
            var connectionString = _settings.AZURE_STORAGE_CONNECTION_STRING;
            var accountNameStart = connectionString.IndexOf("AccountName=") + "AccountName=".Length;
            var accountNameEnd = connectionString.IndexOf(";", accountNameStart);
            return connectionString.Substring(accountNameStart, accountNameEnd - accountNameStart);
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Sanitize filename for blob storage
    /// </summary>
    private string SanitizeFilename(string filename)
    {
        if (string.IsNullOrEmpty(filename))
            return "unknown_file";

        // Remove or replace invalid characters
        var invalidChars = new char[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };
        foreach (var invalidChar in invalidChars)
        {
            filename = filename.Replace(invalidChar, '_');
        }

        return filename;
    }

    /// <summary>
    /// Sanitize metadata key for Azure blob storage
    /// </summary>
    private string SanitizeMetadataKey(string key)
    {
        if (string.IsNullOrEmpty(key))
            return "unknown_key";

        // Metadata keys must be valid C# identifiers
        key = key.Replace("-", "_").Replace(".", "_").Replace(" ", "_");
        
        // Ensure it starts with a letter
        if (!char.IsLetter(key[0]))
            key = "meta_" + key;

        return key.ToLowerInvariant();
    }

    /// <summary>
    /// Create comprehensive metadata JSON for storage
    /// </summary>
    private string CreateMetadataJson(
        string analysisId,
        string filename,
        string contentType,
        SerialFieldResult serialField,
        Dictionary<string, string> customMetadata,
        string correlationId)
    {
        var metadata = new
        {
            analysis_id = analysisId,
            correlation_id = correlationId,
            document_info = new
            {
                filename = filename,
                content_type = contentType,
                upload_timestamp = DateTime.UtcNow.ToString("O")
            },
            extraction_results = new
            {
                serial_field = new
                {
                    value = serialField.Value,
                    confidence = serialField.Confidence,
                    status = serialField.Status.ToString(),
                    confidence_acceptable = serialField.ConfidenceAcceptable,
                    bounding_region = serialField.BoundingRegion,
                    spans = serialField.Spans
                }
            },
            storage_info = new
            {
                storage_reason = "low_confidence",
                container_name = _settings.BLOB_CONTAINER_PREFIX,
                storage_timestamp = DateTime.UtcNow.ToString("O")
            },
            custom_metadata = customMetadata,
            version = "1.0"
        };

        return JsonSerializer.Serialize(metadata, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}
using System.ComponentModel.DataAnnotations;

namespace WarehouseReturns.DocumentIntelligence.Configuration;

/// <summary>
/// Comprehensive configuration settings for Azure Document Intelligence service integration.
/// 
/// This class contains all necessary configuration parameters for connecting to and using
/// Azure Document Intelligence API for document analysis and field extraction operations.
/// 
/// Security Considerations:
/// - DOCUMENT_INTELLIGENCE_KEY should never be stored in plain text in configuration files
/// - Use Azure Key Vault or environment variables for production deployments
/// - Implement key rotation policies and monitor API usage for anomalies
/// - Restrict network access to Azure Document Intelligence endpoints where possible
/// 
/// Configuration Validation:
/// - Endpoint must be a valid HTTPS URL pointing to your Document Intelligence resource
/// - API version should match the capabilities required by your application
/// - Model IDs must exist in your Document Intelligence resource before use
/// </summary>
/// <example>
/// <code>
/// // Configuration in appsettings.json
/// {
///   "Values": {
///     "DOCUMENT_INTELLIGENCE_ENDPOINT": "https://your-resource.cognitiveservices.azure.com/",
///     "DOCUMENT_INTELLIGENCE_KEY": "your-api-key-here",
///     "DOCUMENT_INTELLIGENCE_API_VERSION": "2024-11-30",
///     "DEFAULT_MODEL_ID": "serialnumber-v1.0"
///   }
/// }
/// 
/// // Usage in dependency injection
/// services.Configure&lt;DocumentIntelligenceSettings&gt;(
///     configuration.GetSection("Values"));
/// </code>
/// </example>
public class DocumentIntelligenceSettings
{
    /// <summary>
    /// Azure Document Intelligence service endpoint URL.
    /// 
    /// This must be the complete HTTPS endpoint URL for your Azure Document Intelligence resource.
    /// The endpoint format is: https://{resource-name}.cognitiveservices.azure.com/
    /// 
    /// Security Requirements:
    /// - Must use HTTPS protocol for secure communication
    /// - Should point to the correct Azure region for optimal performance
    /// - Verify the endpoint is accessible from your deployment environment
    /// 
    /// Configuration Sources (in order of precedence):
    /// 1. Environment variable: DOCUMENT_INTELLIGENCE_ENDPOINT
    /// 2. Azure Key Vault reference: @Microsoft.KeyVault(SecretUri=...)
    /// 3. Application settings file (development only)
    /// </summary>
    /// <example>
    /// Valid endpoints:
    /// - https://eastus-doc-intel.cognitiveservices.azure.com/
    /// - https://westeurope-doc-intel.cognitiveservices.azure.com/
    /// - https://your-company-docai.cognitiveservices.azure.com/
    /// </example>
    [Required(ErrorMessage = "Document Intelligence endpoint URL is required")]
    [Url(ErrorMessage = "Document Intelligence endpoint must be a valid HTTPS URL")]
    [RegularExpression(@"^https://.*\.cognitiveservices\.azure\.com/?$", 
        ErrorMessage = "Endpoint must be a valid Azure Cognitive Services URL")]
    public string DOCUMENT_INTELLIGENCE_ENDPOINT { get; set; } = string.Empty;

    /// <summary>
    /// Azure Document Intelligence API access key for authentication.
    /// 
    /// This is the primary or secondary key from your Document Intelligence resource.
    /// 
    /// CRITICAL SECURITY REQUIREMENTS:
    /// - NEVER store this key in plain text in configuration files
    /// - Use Azure Key Vault for production deployments
    /// - Implement key rotation policies (recommended: every 90 days)
    /// - Monitor API key usage for anomalous patterns
    /// - Consider using Azure Managed Identity where possible
    /// 
    /// Alternative Authentication Methods (preferred for production):
    /// 1. Azure Managed Identity (recommended)
    /// 2. Azure Key Vault references
    /// 3. Environment variables with proper access controls
    /// 4. Azure App Configuration with Key Vault integration
    /// </summary>
    /// <example>
    /// Environment variable configuration:
    /// DOCUMENT_INTELLIGENCE_KEY=your-32-character-api-key
    /// 
    /// Key Vault reference (Azure App Service):
    /// @Microsoft.KeyVault(SecretUri=https://vault.vault.azure.net/secrets/doc-intel-key/)
    /// </example>
    [Required(ErrorMessage = "Document Intelligence API key is required")]
    [StringLength(32, MinimumLength = 32, ErrorMessage = "API key must be exactly 32 characters")]
    [RegularExpression(@"^[a-zA-Z0-9]{32}$", ErrorMessage = "API key must be 32 alphanumeric characters")]
    public string DOCUMENT_INTELLIGENCE_KEY { get; set; } = string.Empty;

    /// <summary>
    /// Azure Document Intelligence API version to use for requests.
    /// 
    /// Specifies which version of the Document Intelligence API to use. Different versions
    /// may have different capabilities, model support, and response formats.
    /// 
    /// Supported API Versions:
    /// - "2024-11-30" (Latest, recommended - includes enhanced serial number extraction)
    /// - "2023-07-31" (Stable - general availability features)
    /// - "2022-08-31" (Legacy - limited model support)
    /// 
    /// Version Selection Guidelines:
    /// - Use latest version for new deployments and best features
    /// - Consider API compatibility when upgrading existing deployments
    /// - Test thoroughly when changing API versions
    /// - Review Microsoft documentation for version-specific capabilities
    /// </summary>
    /// <example>
    /// Recommended configurations:
    /// - Production: "2024-11-30" (latest stable)
    /// - Development: "2024-11-30" (match production)
    /// - Legacy systems: "2023-07-31" (if compatibility required)
    /// </example>
    [Required(ErrorMessage = "API version is required")]
    [RegularExpression(@"^\d{4}-\d{2}-\d{2}$", ErrorMessage = "API version must be in YYYY-MM-DD format")]
    public string DOCUMENT_INTELLIGENCE_API_VERSION { get; set; } = "2024-11-30";

    /// <summary>
    /// Default Document Intelligence model ID for document analysis operations.
    /// 
    /// Specifies which custom or prebuilt model to use when no specific model is provided
    /// in the analysis request. This should correspond to a model trained specifically
    /// for your serial number extraction use case.
    /// 
    /// Model Types and Use Cases:
    /// - Custom Models: Trained on your specific document types and layouts
    /// - Prebuilt Models: General-purpose models provided by Microsoft
    /// - Composed Models: Combinations of multiple models for different document types
    /// 
    /// Model Naming Conventions (recommended):
    /// - Include version: "serialnumber-v1.0", "serialnumber-v2.1"
    /// - Include document type: "invoice-serial-v1.0", "label-serial-v1.0"
    /// - Include training date: "serialnumber-20241201"
    /// 
    /// Model Management Best Practices:
    /// - Maintain model versioning for rollback capabilities
    /// - Test new models thoroughly before production deployment
    /// - Monitor model performance and accuracy metrics
    /// - Plan for model retraining based on accuracy degradation
    /// </summary>
    /// <example>
    /// Model ID examples:
    /// - "serialnumber-v1.0" (versioned custom model)
    /// - "product-label-extraction" (descriptive name)
    /// - "prebuilt-layout" (Microsoft prebuilt model)
    /// - "invoice-serial-composed" (composed model)
    /// </example>
    [Required(ErrorMessage = "Default model ID is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Model ID must be between 1 and 100 characters")]
    [RegularExpression(@"^[a-zA-Z0-9\-_.]+$", ErrorMessage = "Model ID can only contain letters, numbers, hyphens, underscores, and periods")]
    public string DEFAULT_MODEL_ID { get; set; } = "serialnumber";

    /// <summary>
    /// Validate all configuration settings and return any validation errors.
    /// 
    /// This method performs comprehensive validation of all configuration values
    /// to ensure they meet security, format, and business requirements.
    /// </summary>
    /// <returns>List of validation error messages, empty if all settings are valid</returns>
    /// <example>
    /// <code>
    /// var settings = new DocumentIntelligenceSettings();
    /// var errors = settings.ValidateSettings();
    /// if (errors.Any())
    /// {
    ///     foreach (var error in errors)
    ///     {
    ///         Console.WriteLine($"Configuration Error: {error}");
    ///     }
    /// }
    /// </code>
    /// </example>
    public List<string> ValidateSettings()
    {
        var errors = new List<string>();
        var context = new ValidationContext(this);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(this, context, results, true))
        {
            errors.AddRange(results.Select(r => r.ErrorMessage ?? "Unknown validation error"));
        }

        // Additional business rule validations
        if (!string.IsNullOrEmpty(DOCUMENT_INTELLIGENCE_ENDPOINT))
        {
            if (!Uri.TryCreate(DOCUMENT_INTELLIGENCE_ENDPOINT, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps)
            {
                errors.Add("Document Intelligence endpoint must be a valid HTTPS URL");
            }
        }

        return errors;
    }

    /// <summary>
    /// Get the complete API URL for Document Intelligence operations.
    /// 
    /// Constructs the full URL for making API calls to the Document Intelligence service
    /// by combining the endpoint and API version.
    /// </summary>
    /// <returns>Complete API URL for Document Intelligence operations</returns>
    /// <example>
    /// Result: "https://resource.cognitiveservices.azure.com/documentintelligence/2024-11-30"
    /// </example>
    public string GetApiUrl()
    {
        var baseUrl = DOCUMENT_INTELLIGENCE_ENDPOINT.TrimEnd('/');
        return $"{baseUrl}/documentintelligence/{DOCUMENT_INTELLIGENCE_API_VERSION}";
    }
}

/// <summary>
/// Comprehensive configuration settings for Azure Blob Storage integration.
/// 
/// This class manages all configuration parameters required for storing and retrieving
/// documents in Azure Blob Storage, particularly for low-confidence documents that
/// require manual review and quality assurance processes.
/// 
/// Security Considerations:
/// - Connection string contains storage account keys - protect accordingly
/// - Use Azure Key Vault for production connection string storage
/// - Consider using Azure Managed Identity for authentication
/// - Implement proper access controls and container-level permissions
/// - Enable blob versioning and soft delete for data protection
/// 
/// Performance Optimization:
/// - Choose storage tier appropriate for your access patterns
/// - Consider geo-replication for disaster recovery scenarios
/// - Implement proper blob naming conventions for efficient organization
/// - Monitor storage costs and implement lifecycle policies
/// </summary>
/// <example>
/// <code>
/// // Configuration in appsettings.json
/// {
///   "Values": {
///     "AZURE_STORAGE_CONNECTION_STRING": "DefaultEndpointsProtocol=https;AccountName=...",
///     "BLOB_CONTAINER_PREFIX": "warehouse-returns-doc-intel",
///     "ENABLE_BLOB_STORAGE": true
///   }
/// }
/// </code>
/// </example>
public class BlobStorageSettings
{
    /// <summary>
    /// Azure Storage account connection string for blob operations.
    /// 
    /// This connection string provides access to the Azure Storage account where
    /// low-confidence documents and processing artifacts are stored for review.
    /// 
    /// Connection String Format:
    /// DefaultEndpointsProtocol=https;AccountName={account};AccountKey={key};EndpointSuffix=core.windows.net
    /// 
    /// CRITICAL SECURITY REQUIREMENTS:
    /// - NEVER store connection strings in plain text in configuration files
    /// - Use Azure Key Vault for production deployments
    /// - Implement storage account key rotation policies
    /// - Monitor storage access logs for anomalous activity
    /// - Use Shared Access Signatures (SAS) for temporary access where appropriate
    /// 
    /// Alternative Authentication Methods (preferred for production):
    /// 1. Azure Managed Identity (most secure)
    /// 2. Azure Key Vault references
    /// 3. Environment variables with proper access controls
    /// 4. Azure App Configuration with Key Vault integration
    /// </summary>
    /// <example>
    /// Environment variable configuration:
    /// AZURE_STORAGE_CONNECTION_STRING=DefaultEndpointsProtocol=https;AccountName=storage;AccountKey=key==
    /// 
    /// Key Vault reference (Azure App Service):
    /// @Microsoft.KeyVault(SecretUri=https://vault.vault.azure.net/secrets/storage-connection/)
    /// </example>
    [Required(ErrorMessage = "Azure Storage connection string is required")]
    [RegularExpression(@"^DefaultEndpointsProtocol=https;.*AccountName=.*AccountKey=.*", 
        ErrorMessage = "Connection string must be a valid Azure Storage connection string")]
    public string AZURE_STORAGE_CONNECTION_STRING { get; set; } = string.Empty;

    /// <summary>
    /// Blob container name prefix for organizing stored documents.
    /// 
    /// This prefix is used to create container names for different types of documents
    /// and processing scenarios. The actual container name will be constructed using
    /// this prefix plus additional identifiers for organization.
    /// 
    /// Container Naming Strategy:
    /// - Low confidence documents: {prefix}-low-confidence
    /// - Processing artifacts: {prefix}-artifacts
    /// - Archive storage: {prefix}-archive
    /// - Temporary storage: {prefix}-temp
    /// 
    /// Naming Conventions and Restrictions:
    /// - Must be lowercase letters, numbers, and hyphens only
    /// - Must start and end with a letter or number
    /// - Cannot have consecutive hyphens
    /// - Must be between 3 and 63 characters (including additional suffixes)
    /// 
    /// Organization Best Practices:
    /// - Include environment identifier for multi-environment deployments
    /// - Use descriptive names that indicate the document purpose
    /// - Consider data retention and lifecycle policies in naming
    /// </summary>
    /// <example>
    /// Recommended container prefixes:
    /// - "warehouse-returns-doc-intel" (descriptive)
    /// - "docai-prod-warehouse" (with environment)
    /// - "wrm-document-processing" (abbreviated)
    /// 
    /// Resulting container names:
    /// - "warehouse-returns-doc-intel-low-confidence"
    /// - "warehouse-returns-doc-intel-artifacts"
    /// </example>
    [Required(ErrorMessage = "Blob container prefix is required")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Container prefix must be between 3 and 50 characters")]
    [RegularExpression(@"^[a-z0-9]+(-[a-z0-9]+)*$", 
        ErrorMessage = "Container prefix must be lowercase letters, numbers, and hyphens (no consecutive hyphens)")]
    public string BLOB_CONTAINER_PREFIX { get; set; } = "warehouse-returns-doc-intel";

    /// <summary>
    /// Enable or disable blob storage functionality for document archival.
    /// 
    /// When enabled, documents that fail confidence thresholds or require manual
    /// review will be automatically stored in blob storage for later processing.
    /// 
    /// Feature Control:
    /// - true: Documents are stored for manual review (recommended for production)
    /// - false: Documents are not stored (suitable for development/testing)
    /// 
    /// Storage Scenarios When Enabled:
    /// - Serial number extraction confidence below threshold
    /// - Document processing errors that require investigation
    /// - Audit trail requirements for compliance
    /// - Training data collection for model improvement
    /// 
    /// Considerations for Disabling:
    /// - Reduces storage costs in non-production environments
    /// - Eliminates manual review workflow
    /// - May not meet audit or compliance requirements
    /// - Limits ability to improve model accuracy through feedback
    /// </summary>
    /// <example>
    /// Configuration scenarios:
    /// - Production: true (enable for audit trail and quality assurance)
    /// - Staging: true (enable for testing complete workflow)
    /// - Development: false (disable to reduce costs and complexity)
    /// - Testing: false (disable for unit test performance)
    /// </example>
    public bool ENABLE_BLOB_STORAGE { get; set; } = true;

    /// <summary>
    /// Storage tier for blob storage operations to optimize costs and performance.
    /// 
    /// Azure Blob Storage offers different access tiers with varying costs and
    /// performance characteristics. Choose based on access patterns and cost requirements.
    /// 
    /// Available Tiers:
    /// - "Hot": Frequent access, highest storage cost, lowest access cost
    /// - "Cool": Infrequent access (30+ days), moderate cost, higher access cost
    /// - "Archive": Rare access (180+ days), lowest storage cost, highest access cost
    /// 
    /// Tier Selection Guidelines:
    /// - Hot: Documents accessed within 30 days (active review queue)
    /// - Cool: Documents accessed within 180 days (historical reference)
    /// - Archive: Long-term retention, compliance, or training data
    /// </summary>
    /// <example>
    /// Use case examples:
    /// - "Hot": Active review queue for recent low-confidence extractions
    /// - "Cool": Monthly audit reviews and quality assurance checks
    /// - "Archive": Annual compliance reporting and model training datasets
    /// </example>
    [RegularExpression(@"^(Hot|Cool|Archive)$", ErrorMessage = "Storage tier must be Hot, Cool, or Archive")]
    public string STORAGE_TIER { get; set; } = "Cool";

    /// <summary>
    /// Maximum blob size in MB for document storage operations.
    /// 
    /// Limits the size of individual documents that can be stored in blob storage
    /// to prevent excessive storage costs and processing timeouts.
    /// </summary>
    [Range(1, 1000, ErrorMessage = "Maximum blob size must be between 1 and 1000 MB")]
    public int MAX_BLOB_SIZE_MB { get; set; } = 100;

    /// <summary>
    /// Validate all blob storage configuration settings.
    /// 
    /// Performs comprehensive validation of all blob storage configuration values
    /// including connection string format, container naming rules, and business logic.
    /// </summary>
    /// <returns>List of validation error messages, empty if all settings are valid</returns>
    public List<string> ValidateSettings()
    {
        var errors = new List<string>();
        var context = new ValidationContext(this);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(this, context, results, true))
        {
            errors.AddRange(results.Select(r => r.ErrorMessage ?? "Unknown validation error"));
        }

        // Additional business rule validations
        if (ENABLE_BLOB_STORAGE && string.IsNullOrWhiteSpace(AZURE_STORAGE_CONNECTION_STRING))
        {
            errors.Add("Azure Storage connection string is required when blob storage is enabled");
        }

        return errors;
    }

    /// <summary>
    /// Get the complete container name for a specific document storage scenario.
    /// </summary>
    /// <param name="suffix">Container suffix (e.g., "low-confidence", "artifacts")</param>
    /// <returns>Complete container name following naming conventions</returns>
    public string GetContainerName(string suffix)
    {
        return $"{BLOB_CONTAINER_PREFIX}-{suffix}".ToLowerInvariant();
    }
}

/// <summary>
/// Comprehensive configuration settings for document processing operations and performance tuning.
/// 
/// This class contains all configuration parameters that control the behavior of document
/// processing workflows, including confidence thresholds, retry policies, file size limits,
/// and performance optimization settings.
/// 
/// Performance Tuning Guidelines:
/// - Adjust confidence thresholds based on model accuracy and business requirements
/// - Configure retry policies to handle transient Azure service failures
/// - Set appropriate timeout values for different document types and sizes
/// - Monitor processing times and adjust limits based on performance metrics
/// 
/// Quality Assurance:
/// - Higher confidence thresholds improve accuracy but may increase manual review
/// - Lower thresholds reduce manual work but may allow more errors through
/// - Consider business impact of false positives vs false negatives
/// - Implement A/B testing for threshold optimization
/// </summary>
/// <example>
/// <code>
/// // Configuration for high-accuracy scenario
/// {
///   "CONFIDENCE_THRESHOLD": 0.9,
///   "MAX_FILE_SIZE_MB": 25,
///   "AZURE_API_RETRY_ATTEMPTS": 5,
///   "AZURE_API_TIMEOUT": 180
/// }
/// 
/// // Configuration for high-throughput scenario
/// {
///   "CONFIDENCE_THRESHOLD": 0.7,
///   "MAX_FILE_SIZE_MB": 100,
///   "AZURE_API_RETRY_ATTEMPTS": 3,
///   "AZURE_API_TIMEOUT": 60
/// }
/// </code>
/// </example>
public class DocumentProcessingSettings
{
    /// <summary>
    /// Default confidence threshold for field extraction acceptance.
    /// 
    /// This threshold determines when extracted serial numbers are considered
    /// accurate enough for automatic processing versus requiring manual review.
    /// 
    /// Threshold Selection Impact:
    /// - Higher values (0.8-0.95): More accurate results, more manual review
    /// - Medium values (0.6-0.8): Balanced accuracy and automation
    /// - Lower values (0.4-0.6): More automation, higher error risk
    /// 
    /// Business Considerations:
    /// - Cost of manual review vs cost of processing errors
    /// - Regulatory requirements for accuracy
    /// - Downstream system error handling capabilities
    /// - Training data availability for model improvement
    /// 
    /// Optimization Strategies:
    /// - Start with conservative threshold (0.8) and adjust based on results
    /// - Implement different thresholds for different document types
    /// - Monitor false positive/negative rates and adjust accordingly
    /// - Use A/B testing to optimize for business metrics
    /// </summary>
    /// <example>
    /// Threshold selection by use case:
    /// - Financial documents: 0.9 (high accuracy required)
    /// - Inventory management: 0.7 (balanced accuracy and throughput)
    /// - Data entry automation: 0.6 (speed prioritized, errors acceptable)
    /// - Compliance reporting: 0.95 (maximum accuracy required)
    /// </example>
    [Range(0.0, 1.0, ErrorMessage = "Confidence threshold must be between 0.0 and 1.0")]
    public double CONFIDENCE_THRESHOLD { get; set; } = 0.7;

    /// <summary>
    /// Maximum allowed file size in megabytes for document processing.
    /// 
    /// Limits the size of documents that can be processed to prevent excessive
    /// processing times, memory usage, and Azure API timeout issues.
    /// 
    /// Size Considerations:
    /// - Azure Document Intelligence limits: Typically 500MB for analysis
    /// - Processing time: Larger files take exponentially longer to process
    /// - Memory usage: Large files may cause memory pressure in serverless environments
    /// - Network transfer: Larger files require more bandwidth and time
    /// 
    /// Performance Impact:
    /// - Files under 10MB: Fast processing (seconds)
    /// - Files 10-50MB: Moderate processing (tens of seconds)
    /// - Files 50-100MB: Slow processing (minutes)
    /// - Files over 100MB: Very slow processing, high timeout risk
    /// 
    /// Optimization Strategies:
    /// - Implement file compression or image optimization before processing
    /// - Split large documents into smaller sections
    /// - Use different size limits for different document types
    /// - Monitor processing times and adjust limits based on performance
    /// </summary>
    /// <example>
    /// Size limit recommendations by deployment type:
    /// - Azure Functions Consumption Plan: 25MB (faster cold starts)
    /// - Azure Functions Premium Plan: 100MB (better performance)
    /// - Azure App Service: 200MB (dedicated resources)
    /// - Azure Container Instances: 500MB (maximum control)
    /// </example>
    [Range(1, 500, ErrorMessage = "Maximum file size must be between 1 and 500 MB")]
    public int MAX_FILE_SIZE_MB { get; set; } = 50;

    /// <summary>
    /// Supported MIME content types for document processing (comma-separated).
    /// 
    /// Defines which document formats are accepted for processing. This list
    /// should align with Azure Document Intelligence capabilities and business
    /// requirements for document types.
    /// 
    /// Standard Document Formats:
    /// - PDF: application/pdf (most common, best OCR results)
    /// - Images: image/jpeg, image/png, image/tiff, image/bmp
    /// - Office Documents: application/vnd.openxmlformats-officedocument (DOCX, XLSX)
    /// 
    /// Format Selection Considerations:
    /// - PDF provides best OCR and layout analysis results
    /// - High-resolution images (300+ DPI) work better than low-resolution
    /// - Avoid highly compressed formats that may reduce OCR accuracy
    /// - Consider processing time differences between formats
    /// 
    /// Quality Requirements:
    /// - Minimum resolution: 150 DPI for reliable text extraction
    /// - Maximum resolution: 3000 DPI (higher doesn't improve results significantly)
    /// - Color depth: Grayscale sufficient for most text extraction
    /// - File corruption: Implement validation to detect corrupted files
    /// </summary>
    /// <example>
    /// Content type configurations by use case:
    /// - General documents: "application/pdf,image/jpeg,image/png"
    /// - High-quality scanning: "application/pdf,image/tiff,image/png"
    /// - Mobile capture: "image/jpeg,image/png,application/pdf"
    /// - Archive processing: "application/pdf,image/tiff,image/bmp"
    /// </example>
    [Required(ErrorMessage = "Supported content types list is required")]
    public string SUPPORTED_CONTENT_TYPES { get; set; } = "application/pdf,image/jpeg,image/jpg,image/png,image/bmp,image/tiff";

    /// <summary>
    /// Number of retry attempts for Azure API calls when transient failures occur.
    /// 
    /// Azure services occasionally experience transient failures due to network
    /// issues, service throttling, or temporary capacity constraints. Retry logic
    /// helps ensure successful processing despite these temporary issues.
    /// 
    /// Retry Strategy Considerations:
    /// - More retries increase success rate but also increase processing time
    /// - Exponential backoff prevents overwhelming services during high load
    /// - Different failure types may require different retry strategies
    /// - Monitor retry patterns to identify systemic issues
    /// 
    /// Failure Types and Retry Suitability:
    /// - Network timeouts: Retry recommended
    /// - Service throttling (429): Retry with backoff
    /// - Authentication errors (401): Do not retry
    /// - Invalid requests (400): Do not retry
    /// - Service unavailable (503): Retry with caution
    /// </summary>
    /// <example>
    /// Retry configuration by environment:
    /// - Production: 5 attempts (maximize success rate)
    /// - Staging: 3 attempts (balance testing speed and reliability)
    /// - Development: 1 attempt (fast feedback on issues)
    /// - Load testing: 2 attempts (realistic but not excessive)
    /// </example>
    [Range(0, 10, ErrorMessage = "Retry attempts must be between 0 and 10")]
    public int AZURE_API_RETRY_ATTEMPTS { get; set; } = 3;

    /// <summary>
    /// Initial delay in seconds between retry attempts for failed Azure API calls.
    /// 
    /// This delay is used as the base for exponential backoff retry strategy.
    /// Each subsequent retry will wait exponentially longer to avoid overwhelming
    /// services that may be experiencing high load or temporary issues.
    /// 
    /// Exponential Backoff Formula:
    /// Delay = AZURE_API_RETRY_DELAY * (2 ^ retry_attempt) + random_jitter
    /// 
    /// Delay Calculation Examples (base delay = 2 seconds):
    /// - 1st retry: 2 seconds
    /// - 2nd retry: 4 seconds
    /// - 3rd retry: 8 seconds
    /// - 4th retry: 16 seconds
    /// 
    /// Tuning Considerations:
    /// - Shorter delays provide faster recovery from brief issues
    /// - Longer delays reduce load on services during widespread issues
    /// - Consider user experience impact of total retry time
    /// - Balance between success rate and processing time
    /// </summary>
    /// <example>
    /// Delay recommendations by scenario:
    /// - Interactive applications: 1-2 seconds (user experience priority)
    /// - Batch processing: 5-10 seconds (throughput priority)
    /// - Background tasks: 10-30 seconds (resource efficiency priority)
    /// </example>
    [Range(1, 60, ErrorMessage = "Retry delay must be between 1 and 60 seconds")]
    public int AZURE_API_RETRY_DELAY { get; set; } = 2;

    /// <summary>
    /// Maximum timeout in seconds for Azure API operations before giving up.
    /// 
    /// Sets the maximum time to wait for Azure Document Intelligence API responses
    /// before considering the operation failed. This prevents indefinite waiting
    /// and helps manage resource utilization in serverless environments.
    /// 
    /// Timeout Considerations by Document Size:
    /// - Small documents (< 1MB): 30-60 seconds
    /// - Medium documents (1-10MB): 60-180 seconds
    /// - Large documents (10-50MB): 180-300 seconds
    /// - Very large documents (50MB+): 300-600 seconds
    /// 
    /// Environment-Specific Considerations:
    /// - Azure Functions: Must complete within function timeout (5-10 minutes)
    /// - Azure App Service: Can handle longer timeouts (up to 30 minutes)
    /// - Client applications: Consider user experience with long waits
    /// - Batch processing: Longer timeouts acceptable for throughput
    /// 
    /// Monitoring and Optimization:
    /// - Track actual processing times vs timeout values
    /// - Adjust timeouts based on 95th percentile processing times
    /// - Consider separate timeouts for different document types
    /// - Monitor timeout failures and investigate root causes
    /// </summary>
    /// <example>
    /// Timeout configuration by use case:
    /// - Real-time processing: 60 seconds (fast feedback required)
    /// - Batch processing: 300 seconds (optimize for success rate)
    /// - Large document processing: 600 seconds (handle complex documents)
    /// - Background processing: 900 seconds (maximize success, time less critical)
    /// </example>
    [Range(30, 1800, ErrorMessage = "API timeout must be between 30 and 1800 seconds")]
    public int AZURE_API_TIMEOUT { get; set; } = 300;

    /// <summary>
    /// Parse the supported content types string into a list for easy validation.
    /// 
    /// Converts the comma-separated content types configuration into a list
    /// that can be used for validation and filtering operations.
    /// </summary>
    /// <returns>List of supported MIME content types</returns>
    /// <example>
    /// <code>
    /// var settings = new DocumentProcessingSettings();
    /// var supportedTypes = settings.GetSupportedContentTypes();
    /// 
    /// if (supportedTypes.Contains("application/pdf"))
    /// {
    ///     // PDF processing is supported
    /// }
    /// </code>
    /// </example>
    public List<string> GetSupportedContentTypes()
    {
        return SUPPORTED_CONTENT_TYPES.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                     .Select(x => x.Trim().ToLowerInvariant())
                                     .Where(x => !string.IsNullOrWhiteSpace(x))
                                     .Distinct()
                                     .ToList();
    }

    /// <summary>
    /// Validate a specific content type against the supported types list.
    /// 
    /// Checks if a given MIME type is in the list of supported content types
    /// for document processing operations.
    /// </summary>
    /// <param name="contentType">MIME type to validate</param>
    /// <returns>True if the content type is supported, false otherwise</returns>
    /// <example>
    /// <code>
    /// var settings = new DocumentProcessingSettings();
    /// 
    /// if (settings.IsContentTypeSupported("application/pdf"))
    /// {
    ///     // Process the PDF document
    /// }
    /// else
    /// {
    ///     // Reject the document or convert to supported format
    /// }
    /// </code>
    /// </example>
    public bool IsContentTypeSupported(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        var supportedTypes = GetSupportedContentTypes();
        return supportedTypes.Contains(contentType.Trim().ToLowerInvariant());
    }

    /// <summary>
    /// Calculate the total maximum retry time including all attempts and delays.
    /// 
    /// Computes the maximum time that retry operations could take using
    /// exponential backoff strategy with the configured parameters.
    /// </summary>
    /// <returns>Maximum retry time in seconds</returns>
    /// <example>
    /// With 3 retry attempts and 2-second base delay:
    /// Total time = 2 + 4 + 8 = 14 seconds maximum
    /// </example>
    public int GetMaxRetryTime()
    {
        if (AZURE_API_RETRY_ATTEMPTS == 0)
            return 0;

        var totalTime = 0;
        for (int i = 1; i <= AZURE_API_RETRY_ATTEMPTS; i++)
        {
            totalTime += AZURE_API_RETRY_DELAY * (int)Math.Pow(2, i);
        }
        return totalTime;
    }

    /// <summary>
    /// Validate all document processing configuration settings.
    /// 
    /// Performs comprehensive validation of all configuration values including
    /// data annotations and business rule validation.
    /// </summary>
    /// <returns>List of validation error messages, empty if all settings are valid</returns>
    public List<string> ValidateSettings()
    {
        var errors = new List<string>();
        var context = new ValidationContext(this);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(this, context, results, true))
        {
            errors.AddRange(results.Select(r => r.ErrorMessage ?? "Unknown validation error"));
        }

        // Additional business rule validations
        var supportedTypes = GetSupportedContentTypes();
        if (!supportedTypes.Any())
        {
            errors.Add("At least one supported content type must be specified");
        }

        var maxRetryTime = GetMaxRetryTime();
        if (maxRetryTime > AZURE_API_TIMEOUT)
        {
            errors.Add($"Maximum retry time ({maxRetryTime}s) exceeds API timeout ({AZURE_API_TIMEOUT}s)");
        }

        return errors;
    }
}
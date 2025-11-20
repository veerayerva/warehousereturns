using System.ComponentModel.DataAnnotations;

namespace WarehouseReturns.PieceInfoApi.Configuration;

/// <summary>
/// Configuration settings for PieceInfo API external service integration
/// 
/// This class defines all configurable parameters for the PieceInfo API including:
/// - External API connection settings
/// - Performance and reliability parameters
/// - Security and SSL configuration
/// - Environment-specific settings
/// 
/// Configuration sources (in order of precedence):
/// 1. Environment variables
/// 2. Azure App Configuration
/// 3. local.settings.json (development)
/// 4. Default values (defined in this class)
/// 
/// Example configuration:
/// {
///   "ExternalApiBaseUrl": "https://apim-prod.nfm.com",
///   "ApiTimeoutSeconds": 30,
///   "ApiMaxRetries": 3,
///   "VerifySsl": true,
///   "WarehouseReturnsEnv": "production"
/// }
/// </summary>
public class PieceInfoApiSettings
{
    /// <summary>
    /// Base URL for external API endpoints
    /// 
    /// This URL serves as the root for all external API calls including:
    /// - Piece inventory location API
    /// - Product master API
    /// - Vendor details API
    /// 
    /// Environment-specific URLs:
    /// - Development: "https://apim-dev.nfm.com"
    /// - Staging: "https://apim-staging.nfm.com"
    /// - Production: "https://apim-prod.nfm.com"
    /// 
    /// Example: "https://apim-prod.nfm.com"
    /// </summary>
    [Required(ErrorMessage = "External API base URL is required")]
    [Url(ErrorMessage = "External API base URL must be a valid URL")]
    public string ExternalApiBaseUrl { get; set; } = "https://apim-dev.nfm.com";

    /// <summary>
    /// Azure API Management subscription key for authentication
    /// 
    /// Required for accessing external APIs through Azure APIM gateway.
    /// This key should be stored securely and not logged or exposed in responses.
    /// 
    /// Obtain from:
    /// - Azure Portal > API Management > Subscriptions
    /// - Azure Key Vault (recommended for production)
    /// 
    /// Example: "a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6"
    /// </summary>
    [Required(ErrorMessage = "API Management subscription key is required")]
    [StringLength(64, MinimumLength = 8, ErrorMessage = "Subscription key must be between 8-64 characters")]
    public string OcpApimSubscriptionKey { get; set; } = string.Empty;

    /// <summary>
    /// HTTP request timeout in seconds for external API calls
    /// 
    /// Recommended values:
    /// - Development: 30 seconds (allows for debugging)
    /// - Production: 10-15 seconds (faster failure detection)
    /// - High-load scenarios: 5-10 seconds
    /// 
    /// Range: 1-300 seconds
    /// Default: 30 seconds
    /// </summary>
    [Range(1, 300, ErrorMessage = "API timeout must be between 1 and 300 seconds")]
    public int ApiTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of retry attempts for failed API calls
    /// 
    /// Retry policy applies to:
    /// - Network timeouts
    /// - HTTP 5xx server errors
    /// - Transient connection failures
    /// 
    /// Does NOT retry for:
    /// - HTTP 4xx client errors (permanent failures)
    /// - Authentication errors
    /// - Validation errors
    /// 
    /// Recommended values:
    /// - Production: 2-3 retries
    /// - Development: 1-2 retries
    /// - High-availability: 3-5 retries
    /// 
    /// Range: 0-10 retries
    /// Default: 3 retries
    /// </summary>
    [Range(0, 10, ErrorMessage = "Max retries must be between 0 and 10")]
    public int ApiMaxRetries { get; set; } = 3;

    /// <summary>
    /// Maximum batch size for bulk operations (future use)
    /// 
    /// Limits the number of items processed in a single batch request
    /// to prevent memory issues and ensure reasonable response times.
    /// 
    /// Considerations:
    /// - Memory usage: Larger batches use more memory
    /// - Response time: Larger batches take longer to process
    /// - External API limits: Some APIs have batch size restrictions
    /// 
    /// Range: 1-100 items
    /// Default: 10 items
    /// </summary>
    [Range(1, 100, ErrorMessage = "Max batch size must be between 1 and 100")]
    public int MaxBatchSize { get; set; } = 10;

    /// <summary>
    /// Application environment identifier
    /// 
    /// Used for:
    /// - Environment-specific logging
    /// - Configuration validation
    /// - Feature flag decisions
    /// - Monitoring and alerting
    /// 
    /// Standard values:
    /// - "development": Local development environment
    /// - "testing": Automated testing environment
    /// - "staging": Pre-production staging environment
    /// - "production": Production environment
    /// 
    /// Default: "development"
    /// </summary>
    [StringLength(50, ErrorMessage = "Environment name must not exceed 50 characters")]
    public string WarehouseReturnsEnv { get; set; } = "development";

    /// <summary>
    /// Enable SSL/TLS certificate verification for external API calls
    /// 
    /// Security recommendations:
    /// - Production: Always true (strict SSL verification)
    /// - Staging: true (test with production-like security)
    /// - Development: false (allow self-signed certificates)
    /// 
    /// When disabled, the application will accept:
    /// - Self-signed certificates
    /// - Expired certificates
    /// - Invalid certificate chains
    /// 
    /// ⚠️ WARNING: Only disable in development environments!
    /// 
    /// Default: false (development-friendly)
    /// </summary>
    public bool VerifySsl { get; set; } = false;

    /// <summary>
    /// Serilog logging level configuration
    /// 
    /// Available levels (in order of verbosity):
    /// - "Verbose": Most detailed logging (development only)
    /// - "Debug": Detailed diagnostic information
    /// - "Information": General operational messages
    /// - "Warning": Warning conditions
    /// - "Error": Error conditions only
    /// - "Fatal": Critical errors only
    /// 
    /// Recommended by environment:
    /// - Development: "Debug" or "Information"
    /// - Staging: "Information"
    /// - Production: "Warning" or "Error"
    /// 
    /// Default: "Information"
    /// </summary>
    [StringLength(20, ErrorMessage = "Log level must not exceed 20 characters")]
    public string LogLevel { get; set; } = "Information";

    /// <summary>
    /// Custom SSL certificate file path for client authentication (optional)
    /// 
    /// Used when external APIs require client certificate authentication.
    /// Should point to a .pem or .crt file containing the client certificate.
    /// 
    /// Security notes:
    /// - Store in secure location with restricted access
    /// - Use Azure Key Vault in production
    /// - Ensure proper file permissions (600/400)
    /// 
    /// Example: "/app/certs/client.crt"
    /// </summary>
    [StringLength(500, ErrorMessage = "SSL certificate path must not exceed 500 characters")]
    public string? SslCertPath { get; set; }

    /// <summary>
    /// Custom SSL private key file path for client authentication (optional)
    /// 
    /// Used in conjunction with SslCertPath for client certificate authentication.
    /// Should point to a .pem or .key file containing the private key.
    /// 
    /// Security notes:
    /// - Store in secure location with highly restricted access
    /// - Use Azure Key Vault in production
    /// - Ensure proper file permissions (600/400)
    /// - Never log or expose in application output
    /// 
    /// Example: "/app/certs/client.key"
    /// </summary>
    [StringLength(500, ErrorMessage = "SSL key path must not exceed 500 characters")]
    public string? SslKeyPath { get; set; }

    /// <summary>
    /// Custom Certificate Authority (CA) bundle file path (optional)
    /// 
    /// Used to specify additional trusted certificate authorities
    /// for validating external API SSL certificates.
    /// 
    /// Useful for:
    /// - Private/internal certificate authorities
    /// - Self-signed certificate chains
    /// - Corporate proxy certificates
    /// 
    /// Should point to a .pem file containing CA certificates.
    /// 
    /// Example: "/app/certs/ca-bundle.crt"
    /// </summary>
    [StringLength(500, ErrorMessage = "SSL CA bundle path must not exceed 500 characters")]
    public string? SslCaBundle { get; set; }

    /// <summary>
    /// Validate configuration settings and ensure consistency
    /// </summary>
    /// <returns>Validation results with any errors found</returns>
    public IEnumerable<ValidationResult> Validate()
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(this);
        
        // Perform standard data annotation validation
        Validator.TryValidateObject(this, context, results, true);
        
        // Custom business rule validation
        if (VerifySsl && string.IsNullOrEmpty(SslCertPath) && !string.IsNullOrEmpty(SslKeyPath))
        {
            results.Add(new ValidationResult(
                "SSL certificate path is required when SSL key path is specified",
                new[] { nameof(SslCertPath), nameof(SslKeyPath) }));
        }
        
        if (WarehouseReturnsEnv == "production" && !VerifySsl)
        {
            results.Add(new ValidationResult(
                "SSL verification must be enabled in production environment",
                new[] { nameof(VerifySsl), nameof(WarehouseReturnsEnv) }));
        }
        
        return results;
    }
}
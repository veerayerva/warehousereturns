using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.OpenApi.Models;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using WarehouseReturns.PieceInfoApi.Models;
using WarehouseReturns.PieceInfoApi.Services;

namespace WarehouseReturns.PieceInfoApi.Functions;

/// <summary>
/// Azure Functions HTTP endpoints for PieceInfo API operations
/// 
/// Provides comprehensive piece information aggregation from multiple external APIs
/// including piece inventory location, product master data, and vendor details.
/// </summary>
public class PieceInfoFunctions
{
    private readonly IAggregationService _aggregationService;
    private readonly IHealthCheckService _healthCheckService;
    private readonly ILogger<PieceInfoFunctions> _logger;

    public PieceInfoFunctions(
        IAggregationService aggregationService,
        IHealthCheckService healthCheckService,
        ILogger<PieceInfoFunctions> logger)
    {
        _aggregationService = aggregationService ?? throw new ArgumentNullException(nameof(aggregationService));
        _healthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Retrieve comprehensive piece information by aggregating data from multiple external APIs
    /// 
    /// This endpoint serves as the primary integration point for warehouse piece information,
    /// orchestrating data collection from multiple external APIs to provide a comprehensive
    /// view of piece details including inventory location, product specifications, and vendor information.
    /// </summary>
    [Function("GetPieceInfo")]
    [OpenApiOperation(operationId: "GetPieceInfo", tags: new[] { "Pieces" }, Summary = "Get aggregated piece information", Description = "Retrieves comprehensive piece information by combining data from piece inventory, product master, and vendor APIs")]
    [OpenApiParameter(name: "piece_number", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Unique piece inventory identifier (e.g., 170080637)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(AggregatedPieceInfo), Description = "Successful response with aggregated piece information")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(object), Description = "Bad request - validation error")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.NotFound, contentType: "application/json", bodyType: typeof(object), Description = "Piece not found")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "application/json", bodyType: typeof(object), Description = "Internal server error")]
    public async Task<HttpResponseData> GetPieceInfo(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "pieces/{piece_number}")] HttpRequestData req,
        string piece_number)
    {
        var correlationId = Guid.NewGuid().ToString();
        
        var contentType = req.Headers.TryGetValues("Content-Type", out var contentTypeValues) 
            ? contentTypeValues.FirstOrDefault() ?? "unknown" 
            : "unknown";
        var userAgent = req.Headers.TryGetValues("User-Agent", out var userAgentValues) 
            ? userAgentValues.FirstOrDefault() ?? "unknown" 
            : "unknown";
            
        _logger.LogInformation(
            "[HTTP-REQUEST] Endpoint: /api/pieces/{PieceNumber}, Method: GET, " +
            "Content-Type: {ContentType}, User-Agent: {UserAgent}, Correlation-ID: {CorrelationId}",
            piece_number,
            contentType,
            userAgent,
            correlationId);

        try
        {
            _logger.LogInformation("GetPieceInfo endpoint called - Correlation ID: {CorrelationId}", correlationId);

            // Validate piece number format and business rules
            var (isValid, errorMessage) = ValidatePieceNumber(piece_number);
            if (!isValid)
            {
                _logger.LogWarning(
                    "Invalid piece number: {PieceNumber} - {ErrorMessage} - Correlation ID: {CorrelationId}",
                    piece_number, errorMessage, correlationId);
                return await CreateErrorResponseAsync(req, errorMessage!, HttpStatusCode.BadRequest, correlationId);
            }

            _logger.LogInformation("Processing piece number: {PieceNumber} - Correlation ID: {CorrelationId}", piece_number, correlationId);

            // Get aggregated piece information from external APIs
            AggregatedPieceInfo result;
            try
            {
                result = await _aggregationService.GetAggregatedPieceInfoAsync(piece_number, correlationId);
                _logger.LogInformation(
                    "Successfully retrieved aggregated data - Piece: {PieceNumber} - Correlation ID: {CorrelationId}",
                    piece_number, correlationId);
            }
            catch (Exception aggregationError)
            {
                _logger.LogError(aggregationError,
                    "Aggregation failed - Piece: {PieceNumber} - Correlation ID: {CorrelationId}",
                    piece_number, correlationId);
                
                // Check for specific error types
                var errorMsg = aggregationError.Message.ToLowerInvariant();
                if (errorMsg.Contains("not found") || errorMsg.Contains("404"))
                {
                    return await CreateErrorResponseAsync(req, $"Piece {piece_number} not found", HttpStatusCode.NotFound, correlationId);
                }
                else if (errorMsg.Contains("timeout"))
                {
                    return await CreateErrorResponseAsync(req, "Request timeout - please try again", HttpStatusCode.GatewayTimeout, correlationId);
                }
                else if (errorMsg.Contains("ssl") || errorMsg.Contains("certificate"))
                {
                    return await CreateErrorResponseAsync(req, "SSL/Certificate error - contact support", HttpStatusCode.BadGateway, correlationId);
                }
                else
                {
                    return await CreateErrorResponseAsync(req, "Failed to retrieve piece information", HttpStatusCode.InternalServerError, correlationId);
                }
            }

            // Create success response
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            AddSecurityHeaders(response);

            var responseData = JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            await response.WriteStringAsync(responseData);

            _logger.LogInformation(
                "[HTTP-RESPONSE-SUCCESS] Status: 200, Piece: {PieceNumber}, " +
                "SKU: {Sku}, Vendor: {VendorCode}, Description: {Description}, " +
                "Response-Size: {ResponseSize} chars, Correlation-ID: {CorrelationId}",
                piece_number,
                result.Sku,
                result.VendorCode,
                result.Description.Length > 50 ? result.Description[..50] + "..." : result.Description,
                responseData.Length,
                correlationId);

            _logger.LogInformation("Piece processing completed successfully - Correlation ID: {CorrelationId}", correlationId);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing piece info request - Correlation ID: {CorrelationId}", correlationId);
            return await CreateErrorResponseAsync(req, "An unexpected error occurred while processing the request", HttpStatusCode.InternalServerError, correlationId);
        }
    }

    /// <summary>
    /// Comprehensive health check endpoint for monitoring and alerting
    /// </summary>
    [Function("PieceInfoHealthCheck")]
    [OpenApiOperation(operationId: "HealthCheck", tags: new[] { "Health" }, Summary = "Health check endpoint", Description = "Provides comprehensive health information about the service and its components including external API connectivity and configuration status")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(HealthStatus), Description = "Service is healthy with detailed component status")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.ServiceUnavailable, contentType: "application/json", bodyType: typeof(HealthStatus), Description = "Service is unhealthy - check component details")]
    public async Task<HttpResponseData> PieceInfoHealthCheck(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        var correlationId = Guid.NewGuid().ToString();
        
        _logger.LogInformation("Health check endpoint called - Correlation ID: {CorrelationId}", correlationId);

        try
        {
            var healthStatus = await _healthCheckService.PerformHealthCheckAsync(correlationId);

            var statusCode = healthStatus.Status == "healthy" ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable;
            
            var response = req.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "application/json");
            AddSecurityHeaders(response);

            var responseData = JsonSerializer.Serialize(healthStatus, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            await response.WriteStringAsync(responseData);

            _logger.LogInformation(
                "Health check completed - Status: {Status} - Correlation ID: {CorrelationId}",
                healthStatus.Status, correlationId);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed - Correlation ID: {CorrelationId}", correlationId);
            return await CreateErrorResponseAsync(req, "Health check failed", HttpStatusCode.InternalServerError, correlationId);
        }
    }

    /// <summary>
    /// Render OpenAPI document
    /// </summary>
    [Function("RenderOpenApiDocument")]
    [OpenApiOperation(operationId: "RenderOpenApiDocument", tags: new[] { "Documentation" }, Summary = "Get OpenAPI specification", Description = "Retrieves the complete OpenAPI 3.0.1 specification document in JSON format for this API")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "Complete OpenAPI 3.0.1 specification document")]
    public async Task<HttpResponseData> RenderOpenApiDocument(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "swagger.json")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        
        var openApiSpec = @"{
  ""openapi"": ""3.0.1"",
  ""info"": {
    ""title"": ""PieceInfo API"",
    ""description"": ""Comprehensive API for aggregating warehouse piece information from multiple external data sources. This service combines data from piece inventory systems, product master databases, and vendor information to provide a unified view of warehouse items including location, specifications, and vendor details."",
    ""version"": ""1.0.0"",
    ""contact"": {
      ""name"": ""Warehouse Returns Team"",
      ""email"": ""warehouse-returns@company.com""
    },
    ""license"": {
      ""name"": ""Internal Use Only""
    }
  },
  ""servers"": [
    {
      ""url"": ""http://localhost:7074/api"",
      ""description"": ""Development server""
    }
  ],
  ""paths"": {
    ""/pieces/{piece_number}"": {
      ""get"": {
        ""tags"": [""Pieces""],
        ""summary"": ""Get aggregated piece information"",
        ""description"": ""Retrieves comprehensive piece information by combining data from piece inventory, product master, and vendor APIs. This endpoint aggregates data from multiple external sources to provide complete details about warehouse pieces including location, product specifications, and vendor information."",
        ""operationId"": ""GetPieceInfo"",
        ""parameters"": [
          {
            ""name"": ""piece_number"",
            ""in"": ""path"",
            ""required"": true,
            ""schema"": {
              ""type"": ""string"",
              ""pattern"": ""^[0-9]+$"",
              ""minLength"": 1,
              ""maxLength"": 20
            },
            ""description"": ""Unique piece inventory identifier - numeric string up to 20 characters"",
            ""example"": ""170080637""
          }
        ],
        ""responses"": {
          ""200"": {
            ""description"": ""Successful response with aggregated piece information"",
            ""content"": {
              ""application/json"": {
                ""schema"": {
                  ""$ref"": ""#/components/schemas/AggregatedPieceInfo""
                },
                ""example"": {
                  ""piece_inventory_key"": ""170080637"",
                  ""sku"": ""67007500"",
                  ""vendor_code"": ""VIZIA"",
                  ""warehouse_location"": ""WHKCTY"",
                  ""rack_location"": ""R03-019-03"",
                  ""serial_number"": ""SZVOU5GB1600294"",
                  ""description"": ""ALL-IN-ONE SOUNDBAR"",
                  ""vendor_name"": ""NIGHT & DAY""
                }
              }
            }
          },
          ""400"": {
            ""description"": ""Bad request - validation error"",
            ""content"": {
              ""application/json"": {
                ""schema"": {
                  ""$ref"": ""#/components/schemas/ErrorResponse""
                },
                ""example"": {
                  ""error"": ""Validation failed"",
                  ""message"": ""piece_number is required and must be a valid numeric string"",
                  ""correlation_id"": ""123e4567-e89b-12d3-a456-426614174000""
                }
              }
            }
          },
          ""404"": {
            ""description"": ""Piece not found"",
            ""content"": {
              ""application/json"": {
                ""schema"": {
                  ""$ref"": ""#/components/schemas/ErrorResponse""
                },
                ""example"": {
                  ""error"": ""Not Found"",
                  ""message"": ""No piece found with inventory key: 999999999"",
                  ""correlation_id"": ""123e4567-e89b-12d3-a456-426614174000""
                }
              }
            }
          },
          ""500"": {
            ""description"": ""Internal server error"",
            ""content"": {
              ""application/json"": {
                ""schema"": {
                  ""$ref"": ""#/components/schemas/ErrorResponse""
                },
                ""example"": {
                  ""error"": ""Internal Server Error"",
                  ""message"": ""An unexpected error occurred while processing the request"",
                  ""correlation_id"": ""123e4567-e89b-12d3-a456-426614174000""
                }
              }
            }
          }
        }
      }
    },
    ""/health"": {
      ""get"": {
        ""tags"": [""Health""],
        ""summary"": ""Health check endpoint"",
        ""description"": ""Provides comprehensive health information about the service and its components including external API connectivity, configuration status, and system metrics"",
        ""operationId"": ""HealthCheck"",
        ""responses"": {
          ""200"": {
            ""description"": ""Service is healthy with detailed component status"",
            ""content"": {
              ""application/json"": {
                ""schema"": {
                  ""$ref"": ""#/components/schemas/HealthStatus""
                },
                ""example"": {
                  ""status"": ""healthy"",
                  ""timestamp"": ""2025-11-19T21:38:42.167368Z"",
                  ""correlation_id"": ""ae56a0ab-072c-48a1-9895-90a333cc60ef"",
                  ""version"": ""1.0.0"",
                  ""service"": ""pieceinfo-api"",
                  ""environment"": ""development"",
                  ""components"": {
                    ""external_apis"": ""healthy"",
                    ""configuration"": ""healthy"",
                    ""database_connectivity"": ""healthy""
                  },
                  ""configuration"": {
                    ""api_timeout_seconds"": 30,
                    ""max_retries"": 3,
                    ""external_api_base_url"": ""https://apim-dev.nfm.com""
                  }
                }
              }
            }
          },
          ""503"": {
            ""description"": ""Service is unhealthy - check component details"",
            ""content"": {
              ""application/json"": {
                ""schema"": {
                  ""$ref"": ""#/components/schemas/HealthStatus""
                },
                ""example"": {
                  ""status"": ""unhealthy"",
                  ""timestamp"": ""2025-11-19T21:38:42.167368Z"",
                  ""correlation_id"": ""ae56a0ab-072c-48a1-9895-90a333cc60ef"",
                  ""version"": ""1.0.0"",
                  ""service"": ""pieceinfo-api"",
                  ""environment"": ""development"",
                  ""components"": {
                    ""external_apis"": ""unhealthy"",
                    ""configuration"": ""healthy"",
                    ""database_connectivity"": ""degraded""
                  }
                }
              }
            }
          }
        }
      }
    }
  },
  ""components"": {
    ""schemas"": {
      ""AggregatedPieceInfo"": {
        ""type"": ""object"",
        ""properties"": {
          ""piece_inventory_key"": {
            ""type"": ""string"",
            ""description"": ""Unique piece inventory identifier"",
            ""example"": ""170080637""
          },
          ""sku"": {
            ""type"": ""string"",
            ""description"": ""Stock keeping unit identifier"",
            ""example"": ""67007500""
          },
          ""vendor_code"": {
            ""type"": ""string"",
            ""description"": ""Vendor code identifier"",
            ""example"": ""VIZIA""
          },
          ""warehouse_location"": {
            ""type"": ""string"",
            ""description"": ""Warehouse location code"",
            ""example"": ""WHKCTY""
          },
          ""rack_location"": {
            ""type"": ""string"",
            ""description"": ""Specific rack location within warehouse"",
            ""example"": ""R03-019-03""
          },
          ""serial_number"": {
            ""type"": ""string"",
            ""description"": ""Product serial number"",
            ""example"": ""SZVOU5GB1600294""
          },
          ""description"": {
            ""type"": ""string"",
            ""description"": ""Product description"",
            ""example"": ""ALL-IN-ONE SOUNDBAR""
          },
          ""vendor_name"": {
            ""type"": ""string"",
            ""description"": ""Vendor company name"",
            ""example"": ""NIGHT & DAY""
          }
        }
      },
      ""HealthStatus"": {
        ""type"": ""object"",
        ""required"": [""status"", ""timestamp"", ""correlation_id"", ""version"", ""service""],
        ""properties"": {
          ""status"": {
            ""type"": ""string"",
            ""enum"": [""healthy"", ""unhealthy"", ""degraded""],
            ""description"": ""Overall health status"",
            ""example"": ""healthy""
          },
          ""timestamp"": {
            ""type"": ""string"",
            ""format"": ""date-time"",
            ""description"": ""Timestamp of health check"",
            ""example"": ""2025-11-19T15:48:42.167368Z""
          },
          ""correlation_id"": {
            ""type"": ""string"",
            ""format"": ""uuid"",
            ""description"": ""Unique correlation identifier"",
            ""example"": ""ae56a0ab-072c-48a1-9895-90a333cc60ef""
          },
          ""version"": {
            ""type"": ""string"",
            ""description"": ""API version"",
            ""example"": ""1.0.0""
          },
          ""service"": {
            ""type"": ""string"",
            ""description"": ""Service name"",
            ""example"": ""pieceinfo-api""
          },
          ""environment"": {
            ""type"": ""string"",
            ""description"": ""Environment name"",
            ""example"": ""development""
          },
          ""components"": {
            ""type"": ""object"",
            ""description"": ""Component health status details"",
            ""additionalProperties"": {
              ""type"": ""string"",
              ""enum"": [""healthy"", ""unhealthy"", ""degraded""]
            }
          },
          ""configuration"": {
            ""type"": ""object"",
            ""description"": ""Configuration information"",
            ""additionalProperties"": true
          }
        }
      },
      ""ErrorResponse"": {
        ""type"": ""object"",
        ""required"": [""error"", ""message""],
        ""properties"": {
          ""error"": {
            ""type"": ""string"",
            ""description"": ""Error type or category"",
            ""example"": ""Validation Error""
          },
          ""message"": {
            ""type"": ""string"",
            ""description"": ""Detailed error message"",
            ""example"": ""The provided piece_number is invalid""
          },
          ""correlation_id"": {
            ""type"": ""string"",
            ""format"": ""uuid"",
            ""description"": ""Unique correlation identifier for tracking"",
            ""example"": ""123e4567-e89b-12d3-a456-426614174000""
          },
          ""timestamp"": {
            ""type"": ""string"",
            ""format"": ""date-time"",
            ""description"": ""Timestamp when error occurred"",
            ""example"": ""2025-11-19T21:38:42.167368Z""
          },
          ""details"": {
            ""type"": ""object"",
            ""description"": ""Additional error details"",
            ""additionalProperties"": true
          }
        }
      }
    }
  }
}";
        
        await response.WriteStringAsync(openApiSpec);
        return response;
    }

    /// <summary>
    /// Render Swagger UI
    /// </summary>
    [Function("RenderSwaggerUI")]
    [OpenApiOperation(operationId: "RenderSwaggerUI", tags: new[] { "Documentation" }, Summary = "Get interactive API documentation", Description = "Provides interactive Swagger UI for testing and exploring the API endpoints")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/html", bodyType: typeof(string), Description = "Interactive Swagger UI HTML page with API documentation and testing capabilities")]
    public async Task<HttpResponseData> RenderSwaggerUI(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "swagger/ui")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/html");
        
        var swaggerHtml = @"<!DOCTYPE html>
<html>
<head>
    <title>PieceInfo API Documentation</title>
    <link rel=""stylesheet"" type=""text/css"" href=""https://unpkg.com/swagger-ui-dist@5.9.0/swagger-ui.css"" />
    <style>
        html { box-sizing: border-box; overflow: -moz-scrollbars-vertical; overflow-y: scroll; }
        *, *:before, *:after { box-sizing: inherit; }
        body { margin:0; background: #fafafa; }
        .swagger-ui .topbar { display: none; }
    </style>
</head>
<body>
    <div id=""swagger-ui""></div>
    <script src=""https://unpkg.com/swagger-ui-dist@5.9.0/swagger-ui-bundle.js""></script>
    <script src=""https://unpkg.com/swagger-ui-dist@5.9.0/swagger-ui-standalone-preset.js""></script>
    <script>
        window.onload = function() {
            const ui = SwaggerUIBundle({
                url: '/api/swagger.json',
                dom_id: '#swagger-ui',
                deepLinking: true,
                presets: [
                    SwaggerUIBundle.presets.apis,
                    SwaggerUIStandalonePreset
                ],
                plugins: [
                    SwaggerUIBundle.plugins.DownloadUrl
                ],
                layout: ""StandaloneLayout"",
                tryItOutEnabled: true,
                supportedSubmitMethods: ['get', 'post', 'put', 'delete', 'patch'],
                docExpansion: 'list',
                filter: true,
                showRequestHeaders: true
            });
        };
    </script>
</body>
</html>";
        
        await response.WriteStringAsync(swaggerHtml);
        return response;
    }

    /// <summary>
    /// Validate piece number format and business rules
    /// </summary>
    private static (bool IsValid, string? ErrorMessage) ValidatePieceNumber(string pieceNumber)
    {
        if (string.IsNullOrWhiteSpace(pieceNumber))
        {
            return (false, "piece_number is required");
        }
        
        // Remove whitespace and convert to string
        pieceNumber = pieceNumber.Trim();
        
        // Check minimum length
        if (pieceNumber.Length < 3)
        {
            return (false, "piece_number must be at least 3 characters long");
        }
        
        // Check maximum length
        if (pieceNumber.Length > 50)
        {
            return (false, "piece_number must not exceed 50 characters");
        }
        
        // Check for valid alphanumeric characters (allow some special chars)
        if (!Regex.IsMatch(pieceNumber, @"^[A-Za-z0-9\-_]*$"))
        {
            return (false, "piece_number contains invalid characters. Only alphanumeric, hyphens, and underscores are allowed");
        }
        
        return (true, null);
    }

    /// <summary>
    /// Create standardized error response
    /// </summary>
    private async Task<HttpResponseData> CreateErrorResponseAsync(
        HttpRequestData req, 
        string message, 
        HttpStatusCode statusCode, 
        string correlationId)
    {
        var errorResponse = new ErrorResponse
        {
            Error = message,
            StatusCode = (int)statusCode,
            Timestamp = DateTime.UtcNow,
            CorrelationId = correlationId
        };

        var response = req.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json");
        AddSecurityHeaders(response);

        var responseData = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        });

        await response.WriteStringAsync(responseData);
        return response;
    }

    /// <summary>
    /// Add standard security headers to response
    /// </summary>
    private static void AddSecurityHeaders(HttpResponseData response)
    {
        response.Headers.Add("X-Content-Type-Options", "nosniff");
        response.Headers.Add("X-Frame-Options", "DENY");
        response.Headers.Add("X-XSS-Protection", "1; mode=block");
        response.Headers.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
        response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
        response.Headers.Add("Content-Security-Policy", "default-src 'self'");
        response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
        response.Headers.Add("Pragma", "no-cache");
    }

    // OpenAPI documentation is now automatically generated using Azure Functions OpenAPI extension
}
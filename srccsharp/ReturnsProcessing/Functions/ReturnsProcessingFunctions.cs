using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System.Net;
using System.Text.Json;
using WarehouseReturns.ReturnsProcessing.Models;
using WarehouseReturns.ReturnsProcessing.Services;

namespace WarehouseReturns.ReturnsProcessing.Functions;

/// <summary>
/// Azure Functions for SharePoint-triggered returns processing
/// 
/// Provides endpoints for processing SharePoint list items through Document Intelligence
/// and PieceInfo API integration with comprehensive error handling and monitoring.
/// </summary>
public class ReturnsProcessingFunctions
{
    private readonly IReturnsProcessingService _processingService;
    private readonly ILogger<ReturnsProcessingFunctions> _logger;

    public ReturnsProcessingFunctions(
        IReturnsProcessingService processingService,
        ILogger<ReturnsProcessingFunctions> logger)
    {
        _processingService = processingService;
        _logger = logger;
    }

    /// <summary>
    /// Process SharePoint list item for returns processing
    /// 
    /// This function is triggered when a SharePoint list item is created or updated.
    /// It orchestrates the complete workflow:
    /// 1. Fetch SharePoint list item and image
    /// 2. Analyze image with Document Intelligence
    /// 3. Extract serial number and confidence score
    /// 4. Lookup piece information using serial
    /// 5. Calculate overall confidence score
    /// 6. Update SharePoint list item with results
    /// </summary>
    [Function("ProcessSharePointItem")]
    [OpenApiOperation(operationId: "ProcessSharePointItem", tags: new[] { "Returns Processing" }, 
        Summary = "Process SharePoint return item", 
        Description = "Processes a SharePoint list item through Document Intelligence and PieceInfo API integration. Extracts serial numbers from product images, enriches with piece information, and updates SharePoint with results.")]
    [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ProcessingRequest), Required = true, 
        Description = "Processing request containing SharePoint list item ID and optional correlation ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ProcessingResult), 
        Summary = "Processing completed successfully", Description = "Returns processing results with extracted serial, confidence score, and enrichment data")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(ErrorResponse), 
        Summary = "Invalid request", Description = "Request validation failed or missing required parameters")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "application/json", bodyType: typeof(ErrorResponse), 
        Summary = "Processing error", Description = "Unexpected error during processing workflow")]
    public async Task<HttpResponseData> ProcessSharePointItem(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "process-sharepoint-item")] 
        HttpRequestData req,
        FunctionContext executionContext)
    {
        var correlationId = Guid.NewGuid().ToString();
        var logger = executionContext.GetLogger("ProcessSharePointItem");

        try
        {
            logger.LogInformation(
                "[PROCESS-SHAREPOINT-ITEM] Function invoked - Correlation: {CorrelationId}", 
                correlationId);

            // Parse request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new ErrorResponse
                {
                    Error = "Request body cannot be empty",
                    CorrelationId = correlationId
                }));
                return errorResponse;
            }

            var processingRequest = JsonSerializer.Deserialize<ProcessingRequest>(requestBody);
            if (processingRequest == null || string.IsNullOrEmpty(processingRequest.ListItemId))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new ErrorResponse
                {
                    Error = "Invalid request format or missing ListItemId",
                    CorrelationId = correlationId
                }));
                return errorResponse;
            }

            // Use provided correlation ID if available
            if (!string.IsNullOrEmpty(processingRequest.CorrelationId))
            {
                correlationId = processingRequest.CorrelationId;
            }

            logger.LogInformation(
                "[PROCESS-SHAREPOINT-ITEM] Processing request - ItemId: {ItemId}, Correlation: {CorrelationId}",
                processingRequest.ListItemId, correlationId);

            // Process the SharePoint item
            var result = await _processingService.ProcessReturnItemAsync(processingRequest.ListItemId, correlationId);

            // Create successful response
            var successResponse = req.CreateResponse(HttpStatusCode.OK);
            successResponse.Headers.Add("Content-Type", "application/json");
            successResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            await successResponse.WriteStringAsync(JsonSerializer.Serialize(result, GetJsonOptions()));

            logger.LogInformation(
                "[PROCESS-SHAREPOINT-ITEM] Processing completed - ItemId: {ItemId}, Status: {Status}, Serial: {Serial}, Confidence: {Confidence}, Correlation: {CorrelationId}",
                processingRequest.ListItemId, result.Status, result.Serial, result.ConfidenceScore, correlationId);

            return successResponse;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, 
                "[PROCESS-SHAREPOINT-ITEM] JSON parsing error - Correlation: {CorrelationId}", 
                correlationId);

            var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new ErrorResponse
            {
                Error = "Invalid JSON format in request body",
                Details = ex.Message,
                CorrelationId = correlationId
            }));
            return errorResponse;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, 
                "[PROCESS-SHAREPOINT-ITEM] Unexpected error - Correlation: {CorrelationId}", 
                correlationId);

            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new ErrorResponse
            {
                Error = "An unexpected error occurred during processing",
                Details = ex.Message,
                CorrelationId = correlationId
            }));
            return errorResponse;
        }
    }

    /// <summary>
    /// Health check endpoint for returns processing service
    /// </summary>
    [Function("HealthCheck")]
    [OpenApiOperation(operationId: "HealthCheck", tags: new[] { "Health" }, 
        Summary = "Service health check", 
        Description = "Validates connectivity to all dependent services including SharePoint, Document Intelligence, and PieceInfo APIs")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(HealthStatus), 
        Summary = "Service is healthy", Description = "All dependencies are operational")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.ServiceUnavailable, contentType: "application/json", bodyType: typeof(HealthStatus), 
        Summary = "Service is unhealthy", Description = "One or more dependencies are not operational")]
    public async Task<HttpResponseData> HealthCheck(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        var correlationId = Guid.NewGuid().ToString();

        try
        {
            _logger.LogInformation(
                "[HEALTH-CHECK] Health check started - Correlation: {CorrelationId}", 
                correlationId);

            var isHealthy = await _processingService.ValidateConfigurationAsync();
            
            var healthStatus = new HealthStatus
            {
                Status = isHealthy ? "healthy" : "unhealthy",
                Timestamp = DateTime.UtcNow,
                CorrelationId = correlationId,
                Service = "returns-processing",
                Version = "1.0.0"
            };

            var statusCode = isHealthy ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable;
            var response = req.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(healthStatus, GetJsonOptions()));

            _logger.LogInformation(
                "[HEALTH-CHECK] Health check completed - Status: {Status}, Correlation: {CorrelationId}",
                healthStatus.Status, correlationId);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[HEALTH-CHECK] Health check failed - Correlation: {CorrelationId}",
                correlationId);

            var healthStatus = new HealthStatus
            {
                Status = "unhealthy",
                Timestamp = DateTime.UtcNow,
                CorrelationId = correlationId,
                Service = "returns-processing",
                Version = "1.0.0",
                Error = ex.Message
            };

            var response = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(healthStatus, GetJsonOptions()));
            return response;
        }
    }

    /// <summary>
    /// Render OpenAPI documentation
    /// </summary>
    [Function("RenderOpenApiDocument")]
    [OpenApiOperation(operationId: "RenderOpenApiDocument", tags: new[] { "Documentation" }, 
        Summary = "Get OpenAPI specification", 
        Description = "Retrieves the complete OpenAPI specification document for this service")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), 
        Description = "Complete OpenAPI specification document")]
    public async Task<HttpResponseData> RenderOpenApiDocument(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "swagger.json")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json");
        
        var openApiSpec = GetOpenApiSpecification();
        await response.WriteStringAsync(openApiSpec);
        return response;
    }

    /// <summary>
    /// Handle CORS preflight requests for POST endpoints
    /// </summary>
    [Function("HandleCorsOptions")]
    public HttpResponseData HandleCorsOptions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "options", Route = "{*route}")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS, PUT, DELETE");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, Accept");
        response.Headers.Add("Content-Length", "0");
        return response;
    }

    /// <summary>
    /// Render Swagger UI for API documentation
    /// </summary>
    [Function("RenderSwaggerUI")]
    public async Task<HttpResponseData> RenderSwaggerUI(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "swagger")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/html");
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        
        var swaggerHtml = GetSwaggerUIHtml();
        await response.WriteStringAsync(swaggerHtml);
        return response;
    }

    /// <summary>
    /// Get JSON serialization options
    /// </summary>
    private static JsonSerializerOptions GetJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Get Swagger UI HTML
    /// </summary>
    private string GetSwaggerUIHtml()
    {
        return @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <title>Returns Processing API - Swagger UI</title>
    <link rel=""stylesheet"" type=""text/css"" href=""https://unpkg.com/swagger-ui-dist@3.52.5/swagger-ui.css"" />
    <style>
        html { box-sizing: border-box; overflow: -moz-scrollbars-vertical; overflow-y: scroll; }
        *, *:before, *:after { box-sizing: inherit; }
        body { margin:0; background: #fafafa; }
    </style>
</head>
<body>
    <div id=""swagger-ui""></div>
    <script src=""https://unpkg.com/swagger-ui-dist@3.52.5/swagger-ui-bundle.js""></script>
    <script src=""https://unpkg.com/swagger-ui-dist@3.52.5/swagger-ui-standalone-preset.js""></script>
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
            layout: ""StandaloneLayout""
        });
    }
    </script>
</body>
</html>";
    }

    /// <summary>
    /// Get OpenAPI specification
    /// </summary>
    private string GetOpenApiSpecification()
    {
        return @"{
  ""openapi"": ""3.0.1"",
  ""info"": {
    ""title"": ""Returns Processing API"",
    ""description"": ""SharePoint-triggered returns processing service with Document Intelligence and PieceInfo API integration. Extracts serial numbers from product images, enriches with piece information, and updates SharePoint with processing results."",
    ""version"": ""1.0.0"",
    ""contact"": {
      ""name"": ""Warehouse Returns Team""
    }
  },
  ""servers"": [
    {
      ""url"": ""http://localhost:7071/api"",
      ""description"": ""Development server""
    }
  ],
  ""paths"": {
    ""/process-sharepoint-item"": {
      ""post"": {
        ""tags"": [""Returns Processing""],
        ""summary"": ""Process SharePoint return item"",
        ""description"": ""Processes a SharePoint list item through Document Intelligence and PieceInfo API integration."",
        ""requestBody"": {
          ""required"": true,
          ""content"": {
            ""application/json"": {
              ""schema"": {
                ""$ref"": ""#/components/schemas/ProcessingRequest""
              }
            }
          }
        },
        ""responses"": {
          ""200"": {
            ""description"": ""Processing completed successfully"",
            ""content"": {
              ""application/json"": {
                ""schema"": {
                  ""$ref"": ""#/components/schemas/ProcessingResult""
                }
              }
            }
          },
          ""400"": {
            ""description"": ""Invalid request""
          },
          ""500"": {
            ""description"": ""Processing error""
          }
        }
      }
    },
    ""/health"": {
      ""get"": {
        ""tags"": [""Health""],
        ""summary"": ""Service health check"",
        ""responses"": {
          ""200"": {
            ""description"": ""Service is healthy""
          },
          ""503"": {
            ""description"": ""Service is unhealthy""
          }
        }
      }
    }
  },
  ""components"": {
    ""schemas"": {
      ""ProcessingRequest"": {
        ""type"": ""object"",
        ""properties"": {
          ""listItemId"": {
            ""type"": ""string"",
            ""description"": ""SharePoint list item ID""
          },
          ""correlationId"": {
            ""type"": ""string"",
            ""description"": ""Optional correlation ID for tracking""
          }
        },
        ""required"": [""listItemId""]
      },
      ""ProcessingResult"": {
        ""type"": ""object"",
        ""properties"": {
          ""listItemId"": {
            ""type"": ""string""
          },
          ""serial"": {
            ""type"": ""string""
          },
          ""confidenceScore"": {
            ""type"": ""number""
          },
          ""sku"": {
            ""type"": ""string""
          },
          ""family"": {
            ""type"": ""string""
          },
          ""status"": {
            ""type"": ""string""
          },
          ""correlationId"": {
            ""type"": ""string""
          }
        }
      }
    }
  }
}";
    }
}

/// <summary>
/// Processing request model
/// </summary>
public class ProcessingRequest
{
    public string ListItemId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Error response model
/// </summary>
public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}

/// <summary>
/// Health status model
/// </summary>
public class HealthStatus
{
    public string Status { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? Error { get; set; }
}
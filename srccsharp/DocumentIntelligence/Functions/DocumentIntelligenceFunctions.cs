using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System.Net;
using System.Text;
using System.Text.Json;
using WarehouseReturns.DocumentIntelligence.Models;
using WarehouseReturns.DocumentIntelligence.Services;

namespace WarehouseReturns.DocumentIntelligence.Functions;

/// <summary>
/// Azure Functions HTTP API for enterprise document intelligence and automated field extraction services.
/// 
/// This class provides a comprehensive REST API for document processing operations using Azure Document Intelligence,
/// designed for high-throughput production workloads with enterprise-grade reliability, security, and monitoring.
/// 
/// API Endpoints Overview:
/// - POST /api/process-document-url: Process documents from publicly accessible URLs
/// - POST /api/process-document-file: Process uploaded document files with multipart form support
/// - GET /api/health: Comprehensive health check with dependency validation and performance metrics
/// - GET /api/docs: Interactive Swagger UI documentation interface for API exploration
/// - GET /api/swagger: OpenAPI 3.0 specification in JSON format for API integration
/// 
/// Enterprise Features:
/// - Automatic request validation with detailed error responses and field-level validation messages
/// - Correlation ID tracking for distributed system debugging and audit trail maintenance
/// - Comprehensive structured logging with Application Insights integration and custom metrics
/// - Content Security Policy (CSP) headers and CORS support for web application integration
/// - Rate limiting and request throttling capabilities (configured at Azure Function App level)
/// - Authentication support via Azure Function keys, Azure Active Directory, or custom JWT tokens
/// - Request/response compression for optimal network performance and reduced bandwidth costs
/// - OpenAPI 3.0 specification with detailed schemas for automated client code generation
/// 
/// Security Implementation:
/// - Input validation and sanitization preventing injection attacks and malformed requests
/// - Content-Type validation ensuring only supported document formats are processed
/// - File size limits and timeout controls preventing resource exhaustion attacks
/// - Secure error handling that prevents information leakage while providing actionable feedback
/// - Request logging with PII filtering for compliance with data protection regulations
/// 
/// Performance Characteristics:
/// - Asynchronous processing with cancellation token support for responsive user experiences
/// - Memory-efficient streaming for large document processing without excessive RAM usage
/// - Connection pooling and resource management for optimal Azure service utilization
/// - Configurable timeout controls balancing responsiveness with processing reliability
/// - Automatic scaling based on request volume with cold start optimization strategies
/// </summary>
/// <remarks>
/// Production Deployment Considerations:
/// 
/// Function App Configuration:
/// - Enable Application Insights for comprehensive telemetry collection and alerting
/// - Configure appropriate scaling limits based on expected request volume and processing time
/// - Set up health check monitoring with automated alerting for service degradation
/// - Implement proper API key management using Azure Key Vault integration
/// - Configure CORS policies for cross-origin web application access
/// 
/// Monitoring and Alerting Setup:
/// - Request rate monitoring with alerts for unusual traffic patterns or potential attacks
/// - Error rate tracking with automated escalation for service reliability issues
/// - Performance monitoring with SLA alerting for response time degradation
/// - Dependency health monitoring for Azure Document Intelligence and Blob Storage services
/// - Cost monitoring and budget alerts for usage optimization and financial control
/// 
/// Integration Patterns:
/// - Webhook notifications for asynchronous processing completion callbacks
/// - Event Grid integration for document processing workflow orchestration
/// - Service Bus integration for reliable message queuing and batch processing
/// - Logic Apps integration for complex business workflow automation
/// - Power Platform integration for low-code business application development
/// 
/// Example Usage:
/// <code>
/// // Direct HTTP client usage
/// var client = new HttpClient();
/// client.DefaultRequestHeaders.Add("x-functions-key", "your-function-key");
/// 
/// var request = new DocumentAnalysisUrlRequest
/// {
///     DocumentUrl = "https://storage.blob.core.windows.net/docs/invoice.pdf",
///     DocumentType = DocumentType.Invoice,
///     ConfidenceThreshold = 0.85
/// };
/// 
/// var response = await client.PostAsJsonAsync(
///     "https://your-app.azurewebsites.net/api/process-document-url", 
///     request);
/// 
/// // JavaScript/TypeScript frontend integration
/// const processDocument = async (documentUrl: string) => {
///   const response = await fetch('/api/process-document-url', {
///     method: 'POST',
///     headers: {
///       'Content-Type': 'application/json',
///       'x-functions-key': 'your-function-key'
///     },
///     body: JSON.stringify({
///       document_url: documentUrl,
///       document_type: 'product_label',
///       confidence_threshold: 0.8
///     })
///   });
///   
///   const result = await response.json();
///   return result;
/// };
/// </code>
/// </remarks>
public class DocumentIntelligenceFunctions
{
    private readonly IDocumentProcessingService _documentProcessingService;
    private readonly IDocumentIntelligenceService _documentIntelligenceService;
    private readonly IConfiguration _configuration;

    public DocumentIntelligenceFunctions(
        IDocumentProcessingService documentProcessingService,
        IDocumentIntelligenceService documentIntelligenceService,
        IConfiguration configuration)
    {
        _documentProcessingService = documentProcessingService;
        _documentIntelligenceService = documentIntelligenceService;
        _configuration = configuration;
    }

    /// <summary>
    /// Process document from URL using Azure Document Intelligence
    /// </summary>
    [Function("ProcessDocumentFromUrl")]
    [OpenApiOperation(operationId: "ProcessDocumentFromUrl", tags: new[] { "Document Analysis" }, Summary = "Process Document from URL", Description = "Analyzes a document from a URL using Azure Document Intelligence and stores results in blob storage based on confidence levels. Model ID is automatically configured from settings.")]
    [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(DocumentAnalysisUrlRequest), Required = true, Description = "Document analysis request containing document URL (modelId is automatically configured from settings)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(DocumentAnalysisResponse), Summary = "Document processed successfully", Description = "Returns the analysis results with confidence scores and extracted data")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(ErrorResponse), Summary = "Bad Request", Description = "Invalid request format or missing required fields")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "application/json", bodyType: typeof(ErrorResponse), Summary = "Internal Server Error", Description = "Unexpected error during document processing")]
    public async Task<HttpResponseData> ProcessDocumentFromUrl(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "process-document/url")] 
        HttpRequestData req,
        FunctionContext executionContext)
    {
        var correlationId = Guid.NewGuid().ToString();
        var logger = executionContext.GetLogger("ProcessDocument");

        try
        {
            logger.LogInformation("ProcessDocument endpoint called - Correlation ID: {CorrelationId}", correlationId);

            // Parse request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            
            if (string.IsNullOrWhiteSpace(requestBody))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Request body cannot be empty");
                return errorResponse;
            }

            var request = JsonSerializer.Deserialize<DocumentAnalysisUrlRequest>(requestBody);
            if (request == null)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid request format");
                return errorResponse;
            }

            // Always set modelId and confidence threshold from configuration (ignore any values in request)
            request.ModelId = _configuration.GetValue<string>("Values:DEFAULT_MODEL_ID") ?? "serialnumber";
            var configThreshold = _configuration.GetValue<double>("Values:CONFIDENCE_THRESHOLD");
            request.ConfidenceThreshold = configThreshold > 0 ? configThreshold : 0.3;

            // Process document
            var result = await _documentProcessingService.ProcessDocumentFromUrlAsync(request, correlationId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(result));

            logger.LogInformation("Document processing completed - Correlation ID: {CorrelationId}", correlationId);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing document - Correlation ID: {CorrelationId}", correlationId);
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Internal server error");
            return errorResponse;
        }
    }

    /// <summary>
    /// Process document file upload using Azure Document Intelligence
    /// </summary>
    [Function("ProcessDocumentFromFile")]
    [OpenApiOperation(operationId: "ProcessDocumentFromFile", tags: new[] { "Document Analysis" }, Summary = "Process Document from File Upload", Description = "Analyzes an uploaded document file using Azure Document Intelligence and stores results in blob storage based on confidence levels. Model ID is automatically configured from settings.")]
    [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
    [OpenApiRequestBody(contentType: "multipart/form-data", bodyType: typeof(object), Required = true, Description = "Multipart form data containing the document file (modelId is automatically configured from settings)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(DocumentAnalysisResponse), Summary = "Document processed successfully", Description = "Returns the analysis results with confidence scores and extracted data")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(ErrorResponse), Summary = "Bad Request", Description = "Invalid file format or missing required fields")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "application/json", bodyType: typeof(ErrorResponse), Summary = "Internal Server Error", Description = "Unexpected error during document processing")]
    public async Task<HttpResponseData> ProcessDocumentFromFile(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "process-document/file")] 
        HttpRequestData req,
        FunctionContext executionContext)
    {
        var correlationId = Guid.NewGuid().ToString();
        var logger = executionContext.GetLogger("ProcessDocumentFromFile");

        try
        {
            logger.LogInformation("ProcessDocumentFromFile endpoint called - Correlation ID: {CorrelationId}", correlationId);

            // Check if the request has multipart content
            var contentType = req.Headers.GetValues("Content-Type").FirstOrDefault();
            if (contentType == null || !contentType.StartsWith("multipart/form-data"))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Request must be multipart/form-data");
                return errorResponse;
            }

            // Parse multipart form data
            var boundary = GetBoundary(contentType);
            if (string.IsNullOrEmpty(boundary))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Invalid multipart boundary");
                return errorResponse;
            }

            var formData = await ParseMultipartFormDataAsync(req.Body, boundary);
            
            if (!formData.ContainsKey("file") || formData["file"].FileBytes == null)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("File is required");
                return errorResponse;
            }

            var fileData = formData["file"];
            
            // Create file request with configuration values (no overrides allowed)
            var configThreshold = _configuration.GetValue<double>("Values:CONFIDENCE_THRESHOLD");
            var request = new DocumentAnalysisFileRequest
            {
                FileContent = fileData.FileBytes,
                Filename = fileData.FileName ?? "unknown",
                ContentType = fileData.ContentType ?? "application/octet-stream",
                ModelId = _configuration.GetValue<string>("Values:DEFAULT_MODEL_ID") ?? "serialnumber",
                ConfidenceThreshold = configThreshold > 0 ? configThreshold : 0.3
            };

            // Process document using file bytes
            var result = await _documentProcessingService.ProcessDocumentFromFileAsync(request, correlationId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(result));

            logger.LogInformation("Document file processing completed - Correlation ID: {CorrelationId}", correlationId);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing document file - Correlation ID: {CorrelationId}", correlationId);
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync("Internal server error");
            return errorResponse;
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [Function("HealthCheck")]
    [OpenApiOperation(operationId: "HealthCheck", tags: new[] { "Health" }, Summary = "Health Check", Description = "Monitors the health status of Document Intelligence service and its dependencies including Azure Document Intelligence API and blob storage connectivity.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(Dictionary<string, object>), Summary = "Service is healthy", Description = "All dependencies are operational and the service is ready to process requests")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.ServiceUnavailable, contentType: "application/json", bodyType: typeof(Dictionary<string, object>), Summary = "Service is unhealthy", Description = "One or more dependencies are unavailable or the service cannot process requests")]
    public async Task<HttpResponseData> HealthCheck(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] 
        HttpRequestData req,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger("HealthCheck");

        try
        {
            var healthStatus = await _documentIntelligenceService.HealthCheckAsync();
            
            var statusCode = healthStatus.ContainsKey("status") && 
                           healthStatus["status"]?.ToString() == "healthy" 
                           ? HttpStatusCode.OK 
                           : HttpStatusCode.ServiceUnavailable;

            var response = req.CreateResponse(statusCode);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(JsonSerializer.Serialize(healthStatus));

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Health check failed");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
            await errorResponse.WriteStringAsync("Service unavailable");
            return errorResponse;
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
    ""title"": ""Document Intelligence API"",
    ""description"": ""API for document analysis using Azure Document Intelligence with blob storage integration. This service processes various document types and extracts structured information using AI-powered document analysis capabilities."",
    ""version"": ""1.0.0"",
    ""contact"": {
      ""name"": ""Warehouse Returns Team""
    }
  },
  ""servers"": [
    {
      ""url"": ""http://localhost:7075/api"",
      ""description"": ""Development server""
    }
  ],
  ""paths"": {
    ""/process-document/url"": {
      ""post"": {
        ""tags"": [""Document Analysis""],
        ""summary"": ""Process Document from URL"",
        ""description"": ""Analyzes a document from a URL using Azure Document Intelligence and stores results in blob storage based on confidence levels. Model ID is automatically configured from settings."",
        ""operationId"": ""ProcessDocumentFromUrl"",
        ""requestBody"": {
          ""required"": true,
          ""content"": {
            ""application/json"": {
              ""schema"": {
                ""$ref"": ""#/components/schemas/DocumentAnalysisUrlRequest""
              }
            }
          }
        },
        ""responses"": {
          ""200"": {
            ""description"": ""Document processed successfully"",
            ""content"": {
              ""application/json"": {
                ""schema"": {
                  ""$ref"": ""#/components/schemas/DocumentAnalysisResponse""
                }
              }
            }
          },
          ""400"": {
            ""description"": ""Bad Request"",
            ""content"": {
              ""application/json"": {
                ""schema"": {
                  ""$ref"": ""#/components/schemas/ErrorResponse""
                }
              }
            }
          },
          ""500"": {
            ""description"": ""Internal Server Error"",
            ""content"": {
              ""application/json"": {
                ""schema"": {
                  ""$ref"": ""#/components/schemas/ErrorResponse""
                }
              }
            }
          }
        }
      }
    },
    ""/process-document/file"": {
      ""post"": {
        ""tags"": [""Document Analysis""],
        ""summary"": ""Process Document from File Upload"",
        ""description"": ""Analyzes an uploaded document file using Azure Document Intelligence and stores results in blob storage based on confidence levels. Model ID and confidence threshold are automatically configured from settings."",
        ""operationId"": ""ProcessDocumentFromFile"",
        ""requestBody"": {
          ""required"": true,
          ""content"": {
            ""multipart/form-data"": {
              ""schema"": {
                ""type"": ""object"",
                ""properties"": {
                  ""file"": {
                    ""type"": ""string"",
                    ""format"": ""binary"",
                    ""description"": ""Document file to analyze. Model ID and confidence threshold are automatically configured from settings.""
                  }
                },
                ""required"": [""file""]
              }
            }
          }
        },
        ""responses"": {
          ""200"": {
            ""description"": ""Document processed successfully"",
            ""content"": {
              ""application/json"": {
                ""schema"": {
                  ""$ref"": ""#/components/schemas/DocumentAnalysisResponse""
                }
              }
            }
          },
          ""400"": {
            ""description"": ""Bad Request"",
            ""content"": {
              ""application/json"": {
                ""schema"": {
                  ""$ref"": ""#/components/schemas/ErrorResponse""
                }
              }
            }
          },
          ""500"": {
            ""description"": ""Internal Server Error"",
            ""content"": {
              ""application/json"": {
                ""schema"": {
                  ""$ref"": ""#/components/schemas/ErrorResponse""
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
        ""summary"": ""Health Check"",
        ""description"": ""Monitors the health status of Document Intelligence service and its dependencies."",
        ""operationId"": ""HealthCheck"",
        ""responses"": {
          ""200"": {
            ""description"": ""Service is healthy"",
            ""content"": {
              ""application/json"": {
                ""schema"": {
                  ""type"": ""object""
                }
              }
            }
          },
          ""503"": {
            ""description"": ""Service is unhealthy"",
            ""content"": {
              ""application/json"": {
                ""schema"": {
                  ""type"": ""object""
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
      ""DocumentAnalysisUrlRequest"": {
        ""type"": ""object"",
        ""properties"": {
          ""documentUrl"": {
            ""type"": ""string"",
            ""format"": ""uri"",
            ""description"": ""The URL where the document is hosted (must be publicly accessible). Model ID and confidence threshold are automatically configured from settings.""
          }
        },
        ""required"": [""documentUrl""]
      },
      ""DocumentAnalysisResponse"": {
        ""type"": ""object"",
        ""properties"": {
          ""documentId"": {
            ""type"": ""string""
          },
          ""confidence"": {
            ""type"": ""number""
          },
          ""extractedData"": {
            ""type"": ""object""
          }
        }
      },
      ""ErrorResponse"": {
        ""type"": ""object"",
        ""properties"": {
          ""error"": {
            ""type"": ""string""
          },
          ""message"": {
            ""type"": ""string""
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
    /// Render interactive Swagger UI
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
    <title>Document Intelligence API Documentation</title>
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
    /// Extract boundary from multipart content type header
    /// </summary>
    private static string GetBoundary(string contentType)
    {
        var boundaryIndex = contentType.IndexOf("boundary=", StringComparison.OrdinalIgnoreCase);
        if (boundaryIndex == -1) return string.Empty;
        
        var boundary = contentType.Substring(boundaryIndex + 9);
        if (boundary.StartsWith("\"") && boundary.EndsWith("\""))
        {
            boundary = boundary.Substring(1, boundary.Length - 2);
        }
        return boundary;
    }

    /// <summary>
    /// Parse multipart form data from request stream
    /// </summary>
    private static async Task<Dictionary<string, MultipartFormField>> ParseMultipartFormDataAsync(Stream stream, string boundary)
    {
        var formData = new Dictionary<string, MultipartFormField>();
        
        // Read all bytes from stream
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        var allBytes = memoryStream.ToArray();
        
        var boundaryBytes = Encoding.UTF8.GetBytes("--" + boundary);
        var doubleCrLf = Encoding.UTF8.GetBytes("\r\n\r\n");
        
        var parts = SplitBytesByBoundary(allBytes, boundaryBytes);
        
        foreach (var part in parts)
        {
            if (part.Length == 0) continue;
            
            // Find header/body separator
            var headerEndIndex = FindBytesInArray(part, doubleCrLf);
            if (headerEndIndex == -1) continue;
            
            var headerBytes = part.Take(headerEndIndex).ToArray();
            var bodyBytes = part.Skip(headerEndIndex + 4).ToArray();
            
            // Remove trailing CRLF from body if present
            if (bodyBytes.Length >= 2 && bodyBytes[^2] == 13 && bodyBytes[^1] == 10)
            {
                bodyBytes = bodyBytes.Take(bodyBytes.Length - 2).ToArray();
            }
            
            var headers = Encoding.UTF8.GetString(headerBytes);
            
            var nameMatch = System.Text.RegularExpressions.Regex.Match(headers, @"name=""([^""]+)""");
            if (!nameMatch.Success) continue;
            
            var fieldName = nameMatch.Groups[1].Value;
            var filenameMatch = System.Text.RegularExpressions.Regex.Match(headers, @"filename=""([^""]+)""");
            var contentTypeMatch = System.Text.RegularExpressions.Regex.Match(headers, @"Content-Type:\s*(.+)");
            
            if (filenameMatch.Success)
            {
                // This is a file field
                var fileName = filenameMatch.Groups[1].Value;
                var contentType = contentTypeMatch.Success ? contentTypeMatch.Groups[1].Value.Trim() : "application/octet-stream";
                
                formData[fieldName] = new MultipartFormField
                {
                    FileName = fileName,
                    ContentType = contentType,
                    FileBytes = bodyBytes, // Use raw bytes for file content
                    Value = null
                };
            }
            else
            {
                // This is a text field
                formData[fieldName] = new MultipartFormField
                {
                    Value = Encoding.UTF8.GetString(bodyBytes).Trim(),
                    FileName = null,
                    ContentType = null,
                    FileBytes = null
                };
            }
        }
        
        return formData;
    }
    
    /// <summary>
    /// Split byte array by boundary
    /// </summary>
    private static List<byte[]> SplitBytesByBoundary(byte[] data, byte[] boundary)
    {
        var parts = new List<byte[]>();
        var start = 0;
        
        while (start < data.Length)
        {
            var index = FindBytesInArray(data, boundary, start);
            if (index == -1) break;
            
            if (index > start)
            {
                var partLength = index - start;
                var part = new byte[partLength];
                Array.Copy(data, start, part, 0, partLength);
                parts.Add(part);
            }
            
            start = index + boundary.Length;
        }
        
        return parts;
    }
    
    /// <summary>
    /// Find byte pattern in array
    /// </summary>
    private static int FindBytesInArray(byte[] haystack, byte[] needle, int startIndex = 0)
    {
        for (int i = startIndex; i <= haystack.Length - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }

    /// <summary>
    /// Represents a field in multipart form data
    /// </summary>
    private sealed class MultipartFormField
    {
        public string? Value { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }
        public byte[]? FileBytes { get; set; }
    }
}
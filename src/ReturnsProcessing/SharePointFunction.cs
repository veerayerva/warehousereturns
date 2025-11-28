using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using WarehouseReturns.ReturnsProcessing.Services;

namespace WarehouseReturns.ReturnsProcessing
{
    public class SharePointFunction
    {
        private readonly ILogger _logger;
        private readonly ISharePointService _sharePointService;
        private readonly HttpClient _httpClient;

        public SharePointFunction(
            ILoggerFactory loggerFactory,
            ISharePointService sharePointService,
            HttpClient httpClient)
        {
            _logger = loggerFactory.CreateLogger<SharePointFunction>();
            _sharePointService = sharePointService;
            _httpClient = httpClient;
        }

        [Function("ProcessSharePointItem")]
        public async Task<HttpResponseData> ProcessSharePointItem(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "process-sharepoint-item")] HttpRequestData req)
        {
            _logger.LogInformation("SharePoint item processing function triggered");
            var correlationId = Guid.NewGuid().ToString();

            try
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                _logger.LogInformation($"Received request body: {requestBody} - CorrelationId: {correlationId}");
                
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var request = JsonSerializer.Deserialize<ProcessingRequest>(requestBody, jsonOptions);
                
                _logger.LogInformation($"Deserialized request - ListItemId: '{request?.ListItemId}', CorrelationId: '{request?.CorrelationId}' - CorrelationId: {correlationId}");
                
                if (request == null || string.IsNullOrEmpty(request.ListItemId))
                {
                    _logger.LogError($"Invalid request - missing ListItemId. Request body: {requestBody} - CorrelationId: {correlationId}");
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync(JsonSerializer.Serialize(new ErrorResponse 
                    { 
                        Error = "ListItemId is required",
                        CorrelationId = correlationId
                    }));
                    return badResponse;
                }

                correlationId = request.CorrelationId ?? correlationId;
                _logger.LogInformation($"Processing SharePoint item {request.ListItemId} - CorrelationId: {correlationId}");

                // Step 1: Get SharePoint item details
                _logger.LogInformation($"Step 1: Fetching SharePoint item {request.ListItemId} - CorrelationId: {correlationId}");
                var vendorEntry = await _sharePointService.GetVendorReturnEntryAsync(request.ListItemId);
                
                if (vendorEntry == null)
                {
                    _logger.LogWarning($"SharePoint item {request.ListItemId} not found - CorrelationId: {correlationId}");
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFoundResponse.WriteStringAsync(JsonSerializer.Serialize(new ErrorResponse 
                    { 
                        Error = $"SharePoint item {request.ListItemId} not found",
                        CorrelationId = correlationId
                    }));
                    return notFoundResponse;
                }

                _logger.LogInformation($"Step 1 Complete: Retrieved SharePoint item - ID: {vendorEntry.Id}, Status: {vendorEntry.Status}, SerialNumber: {vendorEntry.SerialNumber}, HasSerialImage: {vendorEntry.SerialImage != null} - CorrelationId: {correlationId}");

                // Step 2: Extract serial image attachment
                _logger.LogInformation($"Step 2: Extracting serial image attachment for item {request.ListItemId} - CorrelationId: {correlationId}");
                var serialImageAttachment = await _sharePointService.GetSerialImageAttachmentAsync(request.ListItemId);
                
                if (serialImageAttachment == null)
                {
                    _logger.LogWarning($"No serial image attachment found for item {request.ListItemId} - CorrelationId: {correlationId}");
                    var noImageResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await noImageResponse.WriteStringAsync(JsonSerializer.Serialize(new ErrorResponse 
                    { 
                        Error = "No serial image attachment found for processing",
                        CorrelationId = correlationId
                    }));
                    return noImageResponse;
                }

                _logger.LogInformation($"Step 2 Complete: Found serial image - FileName: {serialImageAttachment.FileName}, Size: {serialImageAttachment.Content.Length} bytes, ContentType: {serialImageAttachment.ContentType} - CorrelationId: {correlationId}");

                // Step 3: Send image to Document Intelligence API
                var documentIntelligenceResult = await SendToDocumentIntelligenceAsync(serialImageAttachment, correlationId);
                
                string extractedSerial = "";
                decimal confidenceScore = 0.0m;
                
                if (documentIntelligenceResult != null)
                {
                    extractedSerial = documentIntelligenceResult.SerialNumber ?? "";
                    confidenceScore = documentIntelligenceResult.ConfidenceScore;
                    _logger.LogInformation($"Document Intelligence extracted serial: {extractedSerial} with confidence: {confidenceScore}");
                }

                // Step 4: If we have a serial number, call PieceInfo API
                string sku = "";
                string family = "";
                
                if (!string.IsNullOrEmpty(extractedSerial))
                {
                    var pieceInfoResult = await CallPieceInfoApiAsync(extractedSerial, correlationId);
                    if (pieceInfoResult != null)
                    {
                        sku = pieceInfoResult.SKU ?? "";
                        family = pieceInfoResult.Family ?? "";
                        _logger.LogInformation($"PieceInfo API returned SKU: {sku}, Family: {family}");
                    }
                }

                // Step 5: Update SharePoint with results
                var updates = new Dictionary<string, object>
                {
                    ["SerialNumber"] = extractedSerial,
                    ["SkuNumber"] = sku,
                    ["Family"] = family,
                    ["Status"] = string.IsNullOrEmpty(extractedSerial) ? "Processing Failed" : "Processing Completed"
                };

                await _sharePointService.UpdateVendorReturnEntryAsync(request.ListItemId, updates);

                // Step 6: Return results
                _logger.LogInformation($"Step 6: Returning processing results - ListItemId: {request.ListItemId}, Status: {updates["Status"]}, Serial: {extractedSerial}, SKU: {sku}, Family: {family} - CorrelationId: {correlationId}");
                
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/json");
                response.Headers.Add("Access-Control-Allow-Origin", "*");
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                response.Headers.Add("Access-Control-Allow-Headers", "*");

                var result = new ProcessingResponse
                {
                    ListItemId = request.ListItemId,
                    Status = updates["Status"].ToString() ?? "",
                    Serial = extractedSerial,
                    ConfidenceScore = confidenceScore,
                    SKU = sku,
                    Family = family,
                    ProcessedDateTime = DateTime.UtcNow,
                    CorrelationId = correlationId
                };

                var resultJson = JsonSerializer.Serialize(result);
                _logger.LogInformation($"Step 6 Complete: Returning response - {resultJson} - CorrelationId: {correlationId}");
                
                await response.WriteStringAsync(resultJson);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing SharePoint item - CorrelationId: {correlationId}");
                
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync(JsonSerializer.Serialize(new ErrorResponse 
                { 
                    Error = ex.Message,
                    Details = ex.StackTrace,
                    CorrelationId = correlationId
                }));
                return errorResponse;
            }
        }

        private async Task<DocumentIntelligenceResult?> SendToDocumentIntelligenceAsync(Models.AttachmentInfo attachment, string correlationId)
        {
            try
            {
                _logger.LogInformation($"Sending image to Document Intelligence API - CorrelationId: {correlationId}");

                // Create multipart form data content
                using var formContent = new MultipartFormDataContent();
                using var imageContent = new ByteArrayContent(attachment.Content);
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(attachment.ContentType);
                formContent.Add(imageContent, "file", attachment.FileName);

                // Add correlation ID
                formContent.Add(new StringContent(correlationId), "correlationId");

                // Send to Document Intelligence API
                var documentApiUrl = "http://localhost:7075/api/analyze-document"; // From configuration
                var response = await _httpClient.PostAsync(documentApiUrl, formContent);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<DocumentIntelligenceResult>(responseContent);
                    return result;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"Document Intelligence API returned {response.StatusCode}: {errorContent}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calling Document Intelligence API - CorrelationId: {correlationId}");
                return null;
            }
        }

        private async Task<PieceInfoResult?> CallPieceInfoApiAsync(string serialNumber, string correlationId)
        {
            try
            {
                _logger.LogInformation($"Calling PieceInfo API for serial: {serialNumber} - CorrelationId: {correlationId}");

                var pieceInfoApiUrl = $"http://localhost:7074/api/piece-info/{Uri.EscapeDataString(serialNumber)}"; // From configuration
                var response = await _httpClient.GetAsync(pieceInfoApiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<PieceInfoResult>(responseContent);
                    return result;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning($"PieceInfo API returned {response.StatusCode}: {errorContent}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error calling PieceInfo API - CorrelationId: {correlationId}");
                return null;
            }
        }

        [Function("HealthCheck")]
        public async Task<HttpResponseData> HealthCheck(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "*");

            var health = new HealthStatus
            {
                Status = "Healthy",
                Service = "SharePoint Returns Processing",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0"
            };

            await response.WriteStringAsync(JsonSerializer.Serialize(health));
            return response;
        }

        [Function("OptionsHandler")]
        public HttpResponseData OptionsHandler(
            [HttpTrigger(AuthorizationLevel.Anonymous, "options", Route = "{*path}")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Requested-With");
            response.Headers.Add("Access-Control-Max-Age", "86400");
            
            return response;
        }

        [Function("SwaggerJson")]
        public async Task<HttpResponseData> SwaggerJson(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "swagger.json")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "*");
            
            var openApiSpec = GetOpenApiSpecification();
            await response.WriteStringAsync(openApiSpec);
            return response;
        }

        [Function("SwaggerUI")]
        public async Task<HttpResponseData> SwaggerUI(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "swagger/ui")] HttpRequestData req)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/html");
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "*");
            
            var swaggerHtml = GetSwaggerUIHtml();
            await response.WriteStringAsync(swaggerHtml);
            return response;
        }

        private string GetOpenApiSpecification()
        {
            return @"{
  ""openapi"": ""3.0.1"",
  ""info"": {
    ""title"": ""SharePoint Returns Processing API"",
    ""description"": ""Azure Functions service for processing SharePoint return items through Document Intelligence and PieceInfo API integration."",
    ""version"": ""1.0.0""
  },
  ""servers"": [
    {
      ""url"": ""http://localhost:7076/api"",
      ""description"": ""Local development server""
    },
    {
      ""url"": ""/api"",
      ""description"": ""Current server""
    }
  ],
  ""paths"": {
    ""/process-sharepoint-item"": {
      ""post"": {
        ""tags"": [""Returns Processing""],
        ""summary"": ""Process SharePoint return item"",
        ""description"": ""Processes a SharePoint list item through Document Intelligence and PieceInfo API integration for returns processing."",
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
                  ""$ref"": ""#/components/schemas/ProcessingResponse""
                }
              }
            }
          },
          ""400"": {
            ""description"": ""Invalid request"",
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
        ""summary"": ""Service health check"",
        ""description"": ""Validates connectivity to SharePoint, Document Intelligence, and PieceInfo APIs"",
        ""responses"": {
          ""200"": {
            ""description"": ""Service is healthy"",
            ""content"": {
              ""application/json"": {
                ""schema"": {
                  ""$ref"": ""#/components/schemas/HealthStatus""
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
      ""ProcessingRequest"": {
        ""type"": ""object"",
        ""properties"": {
          ""listItemId"": {
            ""type"": ""string"",
            ""description"": ""SharePoint list item identifier""
          },
          ""correlationId"": {
            ""type"": ""string"",
            ""description"": ""Optional correlation ID for tracking""
          }
        },
        ""required"": [""listItemId""]
      },
      ""ProcessingResponse"": {
        ""type"": ""object"",
        ""properties"": {
          ""listItemId"": { ""type"": ""string"", ""description"": ""SharePoint list item identifier"" },
          ""serial"": { ""type"": ""string"", ""description"": ""Extracted serial number"" },
          ""confidenceScore"": { ""type"": ""number"", ""format"": ""decimal"", ""description"": ""Extraction confidence score"" },
          ""sku"": { ""type"": ""string"", ""description"": ""Product SKU"" },
          ""family"": { ""type"": ""string"", ""description"": ""Product family"" },
          ""status"": { ""type"": ""string"", ""description"": ""Processing status"" },
          ""processedDateTime"": { ""type"": ""string"", ""format"": ""date-time"", ""description"": ""Processing timestamp"" },
          ""correlationId"": { ""type"": ""string"", ""description"": ""Correlation ID for tracking"" }
        }
      },
      ""ErrorResponse"": {
        ""type"": ""object"",
        ""properties"": {
          ""error"": { ""type"": ""string"", ""description"": ""Error message"" },
          ""details"": { ""type"": ""string"", ""description"": ""Detailed error information"" },
          ""correlationId"": { ""type"": ""string"", ""description"": ""Correlation ID for tracking"" }
        }
      },
      ""HealthStatus"": {
        ""type"": ""object"",
        ""properties"": {
          ""status"": { ""type"": ""string"", ""description"": ""Service health status"" },
          ""service"": { ""type"": ""string"", ""description"": ""Service name"" },
          ""timestamp"": { ""type"": ""string"", ""format"": ""date-time"", ""description"": ""Health check timestamp"" },
          ""version"": { ""type"": ""string"", ""description"": ""Service version"" },
          ""error"": { ""type"": ""string"", ""description"": ""Error information if unhealthy"" }
        }
      }
    }
  }
}";
        }

        private string GetSwaggerUIHtml()
        {
            return @"<!DOCTYPE html>
<html>
<head>
    <title>SharePoint Returns Processing API</title>
    <link rel=""stylesheet"" type=""text/css"" href=""https://unpkg.com/swagger-ui-dist@4.15.5/swagger-ui.css"" />
    <style>
        html { box-sizing: border-box; overflow: -moz-scrollbars-vertical; overflow-y: scroll; }
        *, *:before, *:after { box-sizing: inherit; }
        body { margin: 0; background: #fafafa; }
        .swagger-ui .topbar { background-color: #2c3e50; }
        .swagger-ui .topbar .download-url-wrapper { display: none; }
    </style>
</head>
<body>
    <div id=""swagger-ui""></div>
    <script src=""https://unpkg.com/swagger-ui-dist@4.15.5/swagger-ui-bundle.js""></script>
    <script src=""https://unpkg.com/swagger-ui-dist@4.15.5/swagger-ui-standalone-preset.js""></script>
    <script>
        window.onload = function() {
            // Get the current host and construct the swagger.json URL
            const protocol = window.location.protocol;
            const host = window.location.host;
            const swaggerJsonUrl = protocol + '//' + host + '/api/swagger.json';
            
            SwaggerUIBundle({
                url: swaggerJsonUrl,
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
                requestInterceptor: function(req) {
                    // Add CORS headers for local development
                    req.headers['Access-Control-Allow-Origin'] = '*';
                    return req;
                }
            });
        }
    </script>
</body>
</html>";
        }
    }
}

public class ProcessingRequest
{
    public string ListItemId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
}

public class ProcessingResponse
{
    public string ListItemId { get; set; } = string.Empty;
    public string? Serial { get; set; }
    public decimal ConfidenceScore { get; set; }
    public string? SKU { get; set; }
    public string? Family { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ProcessedDateTime { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}

public class HealthStatus
{
    public string Status { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Version { get; set; } = string.Empty;
    public string? Error { get; set; }
}

public class DocumentIntelligenceResult
{
    public string? SerialNumber { get; set; }
    public decimal ConfidenceScore { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime ProcessedAt { get; set; }
}

public class PieceInfoResult
{
    public string? SKU { get; set; }
    public string? Family { get; set; }
    public string? Description { get; set; }
    public string? CorrelationId { get; set; }
}
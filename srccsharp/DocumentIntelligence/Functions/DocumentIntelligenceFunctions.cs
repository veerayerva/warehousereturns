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
/// HTTP-triggered functions for document analysis using Azure Document Intelligence.
/// Processes documents from URLs or file uploads and extracts structured data.
/// </summary>
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
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(DocumentAnalysisResponse), Description = "Returns the analysis results with confidence scores and extracted data")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(ErrorResponse), Description = "Invalid request format or missing required fields")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "application/json", bodyType: typeof(ErrorResponse), Description = "Unexpected error during document processing")]
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
                errorResponse.Headers.Add("Content-Type", "application/json");
                errorResponse.Headers.Add("Access-Control-Allow-Origin", "*");
                await errorResponse.WriteStringAsync("Request body cannot be empty");
                return errorResponse;
            }

            var request = JsonSerializer.Deserialize<DocumentAnalysisUrlRequest>(requestBody);
            if (request == null)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.Headers.Add("Content-Type", "application/json");
                errorResponse.Headers.Add("Access-Control-Allow-Origin", "*");
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
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            await response.WriteStringAsync(JsonSerializer.Serialize(result));

            logger.LogInformation("Document processing completed - Correlation ID: {CorrelationId}", correlationId);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing document - Correlation ID: {CorrelationId}", correlationId);
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json");
            errorResponse.Headers.Add("Access-Control-Allow-Origin", "*");
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
    [OpenApiRequestBody(contentType: "multipart/form-data", bodyType: typeof(FileUploadRequest), Required = true, Description = "Multipart form data containing the document file (modelId is automatically configured from settings)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(DocumentAnalysisResponse), Description = "Returns the analysis results with confidence scores and extracted data")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(ErrorResponse), Description = "Invalid file format or missing required fields")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "application/json", bodyType: typeof(ErrorResponse), Description = "Unexpected error during document processing")]
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
                errorResponse.Headers.Add("Content-Type", "application/json");
                errorResponse.Headers.Add("Access-Control-Allow-Origin", "*");
                await errorResponse.WriteStringAsync("Request must be multipart/form-data");
                return errorResponse;
            }

            // Parse multipart form data
            var boundary = GetBoundary(contentType);
            if (string.IsNullOrEmpty(boundary))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.Headers.Add("Content-Type", "application/json");
                errorResponse.Headers.Add("Access-Control-Allow-Origin", "*");
                await errorResponse.WriteStringAsync("Invalid multipart boundary");
                return errorResponse;
            }

            var formData = await ParseMultipartFormDataAsync(req.Body, boundary);
            
            if (!formData.ContainsKey("file") || formData["file"].FileBytes == null)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.Headers.Add("Content-Type", "application/json");
                errorResponse.Headers.Add("Access-Control-Allow-Origin", "*");
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
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            await response.WriteStringAsync(JsonSerializer.Serialize(result));

            logger.LogInformation("Document file processing completed - Correlation ID: {CorrelationId}", correlationId);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing document file - Correlation ID: {CorrelationId}", correlationId);
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            errorResponse.Headers.Add("Content-Type", "application/json");
            errorResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            await errorResponse.WriteStringAsync("Internal server error");
            return errorResponse;
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [Function("HealthCheck")]
    [OpenApiOperation(operationId: "HealthCheck", tags: new[] { "Health" }, Summary = "Health Check", Description = "Monitors the health status of Document Intelligence service and its dependencies including Azure Document Intelligence API and blob storage connectivity.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(HealthCheckResponse), Summary = "Service is healthy", Description = "All dependencies are operational and the service is ready to process requests")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.ServiceUnavailable, contentType: "application/json", bodyType: typeof(HealthCheckResponse), Summary = "Service is unhealthy", Description = "One or more dependencies are unavailable or the service cannot process requests")]
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
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            await response.WriteStringAsync(JsonSerializer.Serialize(healthStatus));

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Health check failed");
            
            var errorResponse = req.CreateResponse(HttpStatusCode.ServiceUnavailable);
            errorResponse.Headers.Add("Content-Type", "application/json");
            errorResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            await errorResponse.WriteStringAsync("Service unavailable");
            return errorResponse;
        }
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

/// <summary>
/// Request model for file upload (OpenAPI documentation only)
/// </summary>
public class FileUploadRequest
{
    /// <summary>
    /// The document file to analyze
    /// </summary>
    public byte[] File { get; set; } = Array.Empty<byte>();
}
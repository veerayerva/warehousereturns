using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System.Net;
using System.Text.Json;
using VendorReturnsService.Services;

namespace VendorReturnsService.Functions;

/// <summary>
/// HTTP-triggered function to process a single vendor return item.
/// </summary>
public class ProcessReturnItemFunction
{
    private readonly IVendorReturnProcessor _processor;
    private readonly ILogger<ProcessReturnItemFunction> _logger;

    public ProcessReturnItemFunction(
        IVendorReturnProcessor processor,
        ILogger<ProcessReturnItemFunction> logger)
    {
        _processor = processor;
        _logger = logger;
    }

    [Function("ProcessReturnItem")]
    [OpenApiOperation(operationId: "ProcessReturnItem", tags: new[] { "Vendor Returns" },
        Summary = "Process single vendor return item",
        Description = "Processes a single vendor return item through the complete workflow: image retrieval, Document Intelligence analysis, PieceInfo API enrichment, and SharePoint updates.")]
    [OpenApiSecurity("function_key", SecuritySchemeType.ApiKey, Name = "code", In = OpenApiSecurityLocationType.Query)]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ProcessReturnItemRequest), Required = true,
        Description = "Request containing the SharePoint list item ID and optional correlation ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object),
        Summary = "Processing completed", Description = "Returns detailed processing results with extracted data and status")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.BadRequest, contentType: "application/json", bodyType: typeof(object),
        Summary = "Invalid request", Description = "Request validation failed or missing required parameters")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.InternalServerError, contentType: "application/json", bodyType: typeof(object),
        Summary = "Processing error", Description = "Unexpected error during processing workflow")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "process-return-item")] HttpRequestData req)
    {
        var correlationId = Guid.NewGuid().ToString();
        
        try
        {
            _logger.LogInformation("[{CorrelationId}] ProcessReturnItem function triggered", correlationId);

            // Parse request body
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var requestData = JsonSerializer.Deserialize<ProcessReturnItemRequest>(requestBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (requestData == null || string.IsNullOrEmpty(requestData.ListItemId))
            {
                _logger.LogWarning("[{CorrelationId}] Invalid request: ListItemId is required", correlationId);
                
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteAsJsonAsync(new
                {
                    success = false,
                    error = "ListItemId is required",
                    correlationId
                });
                return badRequestResponse;
            }

            // Override correlationId if provided
            if (!string.IsNullOrEmpty(requestData.CorrelationId))
            {
                correlationId = requestData.CorrelationId;
            }

            // Process the item
            var result = await _processor.ProcessReturnItemAsync(requestData.ListItemId, correlationId);

            var statusCode = result.Success ? HttpStatusCode.OK : HttpStatusCode.InternalServerError;
            var response = req.CreateResponse(statusCode);
            
            await response.WriteAsJsonAsync(new
            {
                success = result.Success,
                listItemId = result.ListItemId,
                pieceNumber = result.PieceNumber,
                serialNumber = result.SerialNumber,
                processStatus = result.ProcessStatus,
                errorMessage = result.ErrorMessage,
                processingDetails = result.ProcessingDetails,
                correlationId
            });

            _logger.LogInformation(
                "[{CorrelationId}] ProcessReturnItem completed: Success={Success}, ListItemId={ListItemId}",
                correlationId, result.Success, result.ListItemId);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Error in ProcessReturnItem function", correlationId);
            
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteAsJsonAsync(new
            {
                success = false,
                error = ex.Message,
                correlationId
            });
            return errorResponse;
        }
    }
}

/// <summary>
/// Request model for ProcessReturnItem function.
/// </summary>
public class ProcessReturnItemRequest
{
    public string ListItemId { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
}

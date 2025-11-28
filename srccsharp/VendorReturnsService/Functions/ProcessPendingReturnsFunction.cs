using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using VendorReturnsService.Configuration;
using VendorReturnsService.Services;

namespace VendorReturnsService.Functions;

/// <summary>
/// Timer-triggered function to process pending vendor returns in batches.
/// </summary>
public class ProcessPendingReturnsFunction
{
    private readonly ProcessingSettings _settings;
    private readonly ISharePointService _sharePointService;
    private readonly IVendorReturnProcessor _processor;
    private readonly ILogger<ProcessPendingReturnsFunction> _logger;

    public ProcessPendingReturnsFunction(
        ProcessingSettings settings,
        ISharePointService sharePointService,
        IVendorReturnProcessor processor,
        ILogger<ProcessPendingReturnsFunction> logger)
    {
        _settings = settings;
        _sharePointService = sharePointService;
        _processor = processor;
        _logger = logger;
    }

    [Function("ProcessPendingReturns")]
    [OpenApiOperation(operationId: "ProcessPendingReturns", tags: new[] { "Vendor Returns" },
        Summary = "Timer trigger for batch processing",
        Description = "Automatically processes pending vendor returns (Status=R1, ProcessStatus!=Failed) in parallel batches on a configured schedule.")]
    public async Task Run([TimerTrigger("%Processing:Schedule%")] TimerInfo timerInfo)
    {
        var correlationId = Guid.NewGuid().ToString();

        try
        {
            _logger.LogInformation(
                "[{CorrelationId}] ProcessPendingReturns timer function triggered at {DateTime}",
                correlationId, DateTime.UtcNow);

            // Retrieve items with Status=R1 and ProcessStatus != 'Failed'
            var pendingItems = await _sharePointService.GetItemsByStatusAsync(
                "R1",
                "Failed",
                correlationId);

            if (pendingItems.Count == 0)
            {
                _logger.LogInformation("[{CorrelationId}] No pending items found", correlationId);
                return;
            }

            _logger.LogInformation(
                "[{CorrelationId}] Found {Count} pending items to process",
                correlationId, pendingItems.Count);

            // Process items in parallel with configured parallelism
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _settings.MaxParallelItems
            };

            var successCount = 0;
            var failureCount = 0;
            var lockObj = new object();

            await Parallel.ForEachAsync(pendingItems, parallelOptions, async (item, cancellationToken) =>
            {
                var itemCorrelationId = $"{correlationId}-{item.Id}";

                try
                {
                    _logger.LogInformation(
                        "[{ItemCorrelationId}] Processing item {ListItemId}",
                        itemCorrelationId, item.Id);

                    var result = await _processor.ProcessReturnItemAsync(item.Id, itemCorrelationId);

                    lock (lockObj)
                    {
                        if (result.Success)
                        {
                            successCount++;
                            _logger.LogInformation(
                                "[{ItemCorrelationId}] Successfully processed item {ListItemId}: PieceNumber={PieceNumber}",
                                itemCorrelationId, item.Id, result.PieceNumber);
                        }
                        else
                        {
                            failureCount++;
                            _logger.LogError(
                                "[{ItemCorrelationId}] Failed to process item {ListItemId}: {Error}",
                                itemCorrelationId, item.Id, result.ErrorMessage);
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (lockObj)
                    {
                        failureCount++;
                    }

                    _logger.LogError(ex,
                        "[{ItemCorrelationId}] Exception processing item {ListItemId}",
                        itemCorrelationId, item.Id);
                }
            });

            _logger.LogInformation(
                "[{CorrelationId}] Batch processing completed: Total={Total}, Success={Success}, Failed={Failed}",
                correlationId, pendingItems.Count, successCount, failureCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[{CorrelationId}] Error in ProcessPendingReturns timer function",
                correlationId);
        }
    }
}

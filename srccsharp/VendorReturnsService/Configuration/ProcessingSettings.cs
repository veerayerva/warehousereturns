namespace VendorReturnsService.Configuration;

/// <summary>
/// Timer trigger and batch processing settings.
/// </summary>
public class ProcessingSettings
{
    public string Schedule { get; set; } = "0 */15 * * * *"; // Every 15 minutes
    public int MaxParallelItems { get; set; } = 3;
}

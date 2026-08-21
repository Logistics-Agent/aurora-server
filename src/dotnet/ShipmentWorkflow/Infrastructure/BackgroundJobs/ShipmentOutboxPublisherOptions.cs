namespace ShipmentWorkflow.Infrastructure.BackgroundJobs;

public sealed class ShipmentOutboxPublisherOptions
{
    public const string SectionName = "ShipmentOutbox";

    public int BatchSize { get; set; } = 50;
    public int MaxRetries { get; set; } = 5;
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(2);
}

namespace GpsTracking.Infrastructure.BackgroundJobs;

public sealed class GpsOutboxPublisherOptions
{
    public const string SectionName = "GpsOutbox";

    public int BatchSize { get; set; } = 50;
    public int MaxRetries { get; set; } = 5;
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(2);

    public void Validate()
    {
        if (BatchSize is < 1 or > 1_000)
            throw new InvalidOperationException("GpsOutbox:BatchSize must be between 1 and 1000.");
        if (MaxRetries is < 1 or > 100)
            throw new InvalidOperationException("GpsOutbox:MaxRetries must be between 1 and 100.");
        if (PollingInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("GpsOutbox:PollingInterval must be positive.");
    }
}

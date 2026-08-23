namespace DocumentOcr.Infrastructure.BackgroundJobs;

public sealed class DocumentOcrOutboxPublisherOptions
{
    public const string SectionName = "DocumentOcrOutbox";

    public int BatchSize { get; set; } = 50;
    public int MaxRetries { get; set; } = 5;
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(2);

    public void Validate()
    {
        if (BatchSize is < 1 or > 1_000)
            throw new InvalidOperationException("DocumentOcrOutbox:BatchSize must be between 1 and 1000.");
        if (MaxRetries is < 1 or > 100)
            throw new InvalidOperationException("DocumentOcrOutbox:MaxRetries must be between 1 and 100.");
        if (PollingInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("DocumentOcrOutbox:PollingInterval must be positive.");
    }
}

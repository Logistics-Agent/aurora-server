namespace DocumentOcr.Application.Jobs;

public sealed class DocumentOcrWorkerOptions
{
    public const string SectionName = "DocumentOcrWorker";

    public int BatchSize { get; init; } = 10;
    public int MaxAttempts { get; init; } = 3;
    public TimeSpan PollingInterval { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan BaseRetryDelay { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan MaxRetryJitter { get; init; } = TimeSpan.FromSeconds(5);

    public void Validate()
    {
        if (BatchSize is < 1 or > 100)
            throw new InvalidOperationException("DocumentOcrWorker:BatchSize must be between 1 and 100.");
        if (MaxAttempts is < 1 or > 20)
            throw new InvalidOperationException("DocumentOcrWorker:MaxAttempts must be between 1 and 20.");
        if (PollingInterval <= TimeSpan.Zero || LeaseDuration <= TimeSpan.Zero || HeartbeatInterval <= TimeSpan.Zero)
            throw new InvalidOperationException("Document OCR worker intervals must be positive.");
        if (HeartbeatInterval >= LeaseDuration)
            throw new InvalidOperationException("Document OCR heartbeat must be shorter than the lease.");
        if (BaseRetryDelay <= TimeSpan.Zero || MaxRetryDelay < BaseRetryDelay || MaxRetryJitter < TimeSpan.Zero)
            throw new InvalidOperationException("Document OCR retry delays are invalid.");
    }
}

public static class DocumentOcrRetryPolicy
{
    public static TimeSpan GetDelay(Guid jobId, int attemptCount, DocumentOcrWorkerOptions options)
    {
        if (jobId == Guid.Empty)
            throw new ArgumentException("JobId is required.", nameof(jobId));
        if (attemptCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(attemptCount));
        options.Validate();

        var exponent = Math.Min(attemptCount - 1, 20);
        var exponentialTicks = options.BaseRetryDelay.Ticks * Math.Pow(2, exponent);
        var boundedTicks = Math.Min(exponentialTicks, options.MaxRetryDelay.Ticks);
        var hash = BitConverter.ToUInt32(jobId.ToByteArray(), 0);
        var jitterTicks = options.MaxRetryJitter.Ticks == 0
            ? 0
            : (long)(hash % (ulong)(options.MaxRetryJitter.Ticks + 1));
        var baseTicks = (long)boundedTicks;
        var boundedJitterTicks = Math.Min(jitterTicks, options.MaxRetryDelay.Ticks - baseTicks);
        return TimeSpan.FromTicks(baseTicks + boundedJitterTicks);
    }
}

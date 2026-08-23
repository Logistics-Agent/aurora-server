namespace GpsTracking.Application.Monitoring;

public sealed class MonitoringOptions
{
    public const string SectionName = "GpsMonitoring";

    public decimal StationarySpeedKph { get; init; } = 1;
    public TimeSpan AbnormalStopDuration { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan SignalLossThreshold { get; init; } = TimeSpan.FromMinutes(5);
    public TimeSpan SignalLossScanInterval { get; init; } = TimeSpan.FromMinutes(1);
    public int SignalLossBatchSize { get; init; } = 100;

    public void Validate()
    {
        if (StationarySpeedKph < 0)
            throw new InvalidOperationException("GpsMonitoring:StationarySpeedKph cannot be negative.");
        if (AbnormalStopDuration <= TimeSpan.Zero
            || SignalLossThreshold <= TimeSpan.Zero
            || SignalLossScanInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("GPS monitoring durations must be positive.");
        }
        if (SignalLossBatchSize is < 1 or > 10_000)
            throw new InvalidOperationException("GpsMonitoring:SignalLossBatchSize must be between 1 and 10000.");
    }
}

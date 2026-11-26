using GpsSimulator.Services;

namespace GpsSimulator.Tests;

public class SimulationMetricsTests
{
    public void Metrics_CalculatesAccuratePercentiles()
    {
        var metrics = new SimulationMetrics();
        metrics.Start(10);

        // Add 100 values: 1ms to 100ms
        for (int i = 1; i <= 100; i++)
        {
            metrics.RecordSuccess(i);
        }

        var (p50, p95, p99) = metrics.GetPercentiles();

        if (Math.Abs(p50 - 50.0) > 0.01) throw new Exception($"Expected p50 50, got {p50}");
        if (Math.Abs(p95 - 95.0) > 0.01) throw new Exception($"Expected p95 95, got {p95}");
        if (Math.Abs(p99 - 99.0) > 0.01) throw new Exception($"Expected p99 99, got {p99}");
    }

    public void Metrics_TracksFailuresAndTimeouts()
    {
        var metrics = new SimulationMetrics();
        metrics.Start(5);

        metrics.RecordSuccess(12.5);
        metrics.RecordFailure(isTimeout: false);
        metrics.RecordFailure(isTimeout: true);
        metrics.RecordRetry();

        metrics.Stop();
        metrics.PrintSummary();
    }
}

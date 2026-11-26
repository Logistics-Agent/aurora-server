using System.Collections.Concurrent;
using System.Diagnostics;

namespace GpsSimulator.Services;

public class SimulationMetrics
{
    private readonly Stopwatch _stopwatch = new();
    private readonly ConcurrentBag<double> _latenciesMs = new();
    private long _totalEvents;
    private long _successfulEvents;
    private long _failedEvents;
    private long _timeoutCount;
    private long _retryCount;
    private int _totalVehicles;

    public void Start(int totalVehicles)
    {
        _totalVehicles = totalVehicles;
        _stopwatch.Restart();
    }

    public void Stop()
    {
        _stopwatch.Stop();
    }

    public void RecordSuccess(double latencyMs)
    {
        Interlocked.Increment(ref _totalEvents);
        Interlocked.Increment(ref _successfulEvents);
        _latenciesMs.Add(latencyMs);
    }

    public void RecordFailure(bool isTimeout = false)
    {
        Interlocked.Increment(ref _totalEvents);
        Interlocked.Increment(ref _failedEvents);
        if (isTimeout)
        {
            Interlocked.Increment(ref _timeoutCount);
        }
    }

    public void RecordRetry()
    {
        Interlocked.Increment(ref _retryCount);
    }

    public (double P50, double P95, double P99) GetPercentiles()
    {
        var array = _latenciesMs.ToArray();
        if (array.Length == 0)
        {
            return (0, 0, 0);
        }

        Array.Sort(array);
        double p50 = GetPercentile(array, 0.50);
        double p95 = GetPercentile(array, 0.95);
        double p99 = GetPercentile(array, 0.99);
        return (p50, p95, p99);
    }

    private static double GetPercentile(double[] sortedArray, double percentile)
    {
        if (sortedArray.Length == 0) return 0;
        int index = (int)Math.Ceiling(percentile * sortedArray.Length) - 1;
        index = Math.Clamp(index, 0, sortedArray.Length - 1);
        return sortedArray[index];
    }

    public void PrintProgress(int activeVehicles)
    {
        long sent = Interlocked.Read(ref _totalEvents);
        long succ = Interlocked.Read(ref _successfulEvents);
        long fail = Interlocked.Read(ref _failedEvents);
        double elapsedSec = Math.Max(0.1, _stopwatch.Elapsed.TotalSeconds);
        double rate = sent / elapsedSec;

        Console.WriteLine(
            $"[Progress {elapsedSec:F0}s] Active: {activeVehicles}/{_totalVehicles} | Sent: {sent} | Succ: {succ} | Fail: {fail} | Rate: {rate:F1} evt/s");
    }

    public void PrintSummary()
    {
        double elapsedSec = Math.Max(0.001, _stopwatch.Elapsed.TotalSeconds);
        long total = Interlocked.Read(ref _totalEvents);
        long succ = Interlocked.Read(ref _successfulEvents);
        long fail = Interlocked.Read(ref _failedEvents);
        long timeouts = Interlocked.Read(ref _timeoutCount);
        long retries = Interlocked.Read(ref _retryCount);
        double successRate = total > 0 ? (double)succ / total * 100.0 : 0.0;
        double throughput = succ / elapsedSec;
        var (p50, p95, p99) = GetPercentiles();

        Console.WriteLine();
        Console.WriteLine("======================= SIMULATION METRICS REPORT =======================");
        Console.WriteLine($"Total Vehicles        : {_totalVehicles}");
        Console.WriteLine($"Duration              : {elapsedSec:F2}s");
        Console.WriteLine($"Total Events Sent     : {total}");
        Console.WriteLine($"Successful Events     : {succ}");
        Console.WriteLine($"Failed Events         : {fail}");
        Console.WriteLine($"RPC Timeout Count     : {timeouts}");
        Console.WriteLine($"Retry Count           : {retries}");
        Console.WriteLine($"Success Rate          : {successRate:F2}%");
        Console.WriteLine($"Throughput            : {throughput:F2} events/sec");
        Console.WriteLine($"Client Latency p50    : {p50:F2} ms");
        Console.WriteLine($"Client Latency p95    : {p95:F2} ms");
        Console.WriteLine($"Client Latency p99    : {p99:F2} ms");
        Console.WriteLine("=========================================================================");
        Console.WriteLine();
    }
}


using System.Diagnostics;
using GpsSimulator.Models;
using GpsTracking.Grpc;
using Grpc.Core;

namespace GpsSimulator.Services;

public class SimulationRunner
{
    private readonly GpsTrackingService.GpsTrackingServiceClient _gpsClient;
    private readonly TelemetryGenerator _telemetryGenerator;
    private readonly SimulationMetrics _metrics;
    private readonly Metadata _headers;

    public SimulationRunner(
        GpsTrackingService.GpsTrackingServiceClient gpsClient,
        TelemetryGenerator telemetryGenerator,
        SimulationMetrics metrics,
        Metadata headers)
    {
        _gpsClient = gpsClient;
        _telemetryGenerator = telemetryGenerator;
        _metrics = metrics;
        _headers = headers;
    }

    public async Task<int> RunAsync(
        List<VehicleSimulation> vehicles,
        SimulationConfig config,
        CancellationToken cancellationToken)
    {
        _metrics.Start(vehicles.Count);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (config.DurationSeconds.HasValue)
        {
            linkedCts.CancelAfter(TimeSpan.FromSeconds(config.DurationSeconds.Value));
        }

        var ct = linkedCts.Token;
        int maxConcurrency = Math.Max(1, Math.Min(config.MaxConcurrency, vehicles.Count));
        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        // Start progress reporting timer for multi-vehicle runs
        using var progressTimer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        int activeVehiclesCount = vehicles.Count;

        var progressTask = Task.Run(async () =>
        {
            try
            {
                while (await progressTimer.WaitForNextTickAsync(ct))
                {
                    _metrics.PrintProgress(Volatile.Read(ref activeVehiclesCount));
                }
            }
            catch (OperationCanceledException) { }
        }, CancellationToken.None);

        bool isSingleMode = config.Mode == SimulationMode.Single && vehicles.Count == 1;

        var vehicleTasks = vehicles.Select(async vehicle =>
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await RunSingleVehicleAsync(vehicle, config, isSingleMode, ct).ConfigureAwait(false);
            }
            finally
            {
                Interlocked.Decrement(ref activeVehiclesCount);
                semaphore.Release();
            }
        }).ToList();

        try
        {
            await Task.WhenAll(vehicleTasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
        {
            // Expected on duration timeout or user cancellation
        }

        _metrics.Stop();

        try
        {
            await progressTask.ConfigureAwait(false);
        }
        catch { }

        _metrics.PrintSummary();

        if (cancellationToken.IsCancellationRequested)
        {
            Console.WriteLine("Simulation cancelled by user.");
            return 130;
        }

        if (config.DurationSeconds.HasValue && linkedCts.IsCancellationRequested)
        {
            Console.WriteLine($"Simulation reached target duration of {config.DurationSeconds.Value}s.");
            return 0;
        }

        return 0;
    }

    private async Task RunSingleVehicleAsync(
        VehicleSimulation vehicle,
        SimulationConfig config,
        bool isSingleMode,
        CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(config.IntervalSeconds);
        using var timer = new PeriodicTimer(interval);

        int totalRoutePoints = vehicle.Trajectory.Points.Count;
        int stepNumber = 0;

        // In single mode with no duration specified: finish once route is completed
        bool loopForever = config.DurationSeconds.HasValue || (config.Mode != SimulationMode.Single);

        while (!ct.IsCancellationRequested)
        {
            stepNumber++;
            var request = _telemetryGenerator.GenerateNext(vehicle);

            if (request != null)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    await _gpsClient.IngestPositionAsync(
                        request,
                        _headers,
                        deadline: DateTime.UtcNow.AddSeconds(5),
                        cancellationToken: ct).ConfigureAwait(false);

                    sw.Stop();
                    _metrics.RecordSuccess(sw.Elapsed.TotalMilliseconds);

                    if (isSingleMode)
                    {
                        Console.WriteLine(
                            $"[{stepNumber}/{totalRoutePoints}] {request.Latitude:F4},{request.Longitude:F4} | {request.SpeedKph:F0} km/h (lat: {sw.Elapsed.TotalMilliseconds:F1}ms)");
                    }
                }
                catch (RpcException ex) when (ct.IsCancellationRequested && ex.StatusCode == StatusCode.Cancelled)
                {
                    break;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.DeadlineExceeded)
                {
                    sw.Stop();
                    _metrics.RecordFailure(isTimeout: true);
                    if (isSingleMode)
                    {
                        Console.WriteLine($"[WARN] IngestPosition timeout: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    _metrics.RecordFailure();
                    if (isSingleMode)
                    {
                        Console.WriteLine($"[WARN] IngestPosition failed: {ex.Message}");
                    }
                }
            }
            else
            {
                // Simulated signal loss step
                if (isSingleMode)
                {
                    Console.WriteLine($"[{stepNumber}/{totalRoutePoints}] [SIGNAL LOSS] Telemetry dropped.");
                }
            }

            // Check completion for non-looping single mode
            if (!loopForever && stepNumber >= totalRoutePoints)
            {
                break;
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}


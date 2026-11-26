using GpsSimulator.Models;
using GpsSimulator.Services;
using GpsTracking.Grpc;
using Grpc.Core;
using Grpc.Net.Client;
using RoutePlanningAgent.Grpc;
using ShipmentWorkflow.Grpc;

namespace GpsSimulator;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        SimulationConfig config;
        try
        {
            config = SimulationConfig.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(SimulationConfig.Usage);
            Console.Error.WriteLine();
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }

        using var cts = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\n[Shutdown] Cancellation received. Stopping vehicles gracefully...");
            cts.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        Console.WriteLine("=========================================================");
        Console.WriteLine("                  Aurora GPS Simulator                   ");
        Console.WriteLine("=========================================================");
        Console.WriteLine($"Mode          : {config.Mode}");
        if (config.Mode == SimulationMode.Single)
        {
            Console.WriteLine($"Shipment      : {config.SingleShipmentId}");
        }
        else if (config.VehiclesLimit.HasValue)
        {
            Console.WriteLine($"Vehicles Limit: {config.VehiclesLimit.Value}");
        }
        Console.WriteLine($"Behavior      : {config.Behavior}");
        Console.WriteLine($"Interval      : {config.IntervalSeconds}s | Base Speed: {config.BaseSpeedKph:0.#} km/h");
        if (config.DurationSeconds.HasValue)
        {
            Console.WriteLine($"Duration      : {config.DurationSeconds.Value}s");
        }
        if (config.RandomSeed.HasValue)
        {
            Console.WriteLine($"Random Seed   : {config.RandomSeed.Value}");
        }
        Console.WriteLine($"GPS URL       : {config.GpsGrpcUrl}");
        Console.WriteLine($"Shipment URL  : {config.ShipmentGrpcUrl}");
        Console.WriteLine($"Route URL     : {config.RouteGrpcUrl}");
        Console.WriteLine($"Tenant ID     : {config.TenantId}");
        Console.WriteLine($"Max Concurrency: {config.MaxConcurrency}");
        Console.WriteLine("=========================================================");
        Console.WriteLine();

        var headers = new Metadata
        {
            { "x-tenant-id", config.TenantId },
            { "x-user-id", config.UserId }
        };

        using var gpsChannel = CreateChannel(config.GpsGrpcUrl);
        var gpsClient = new GpsTrackingService.GpsTrackingServiceClient(gpsChannel);

        if (config.Mode == SimulationMode.Scenario)
        {
            var scenarioRunner = new FunctionalScenarioRunner(gpsClient, config);
            return await scenarioRunner.RunScenariosAsync(config.ScenarioName ?? "all", cts.Token);
        }

        using var shipmentChannel = CreateChannel(config.ShipmentGrpcUrl);
        var shipmentClient = new ShipmentWorkflowService.ShipmentWorkflowServiceClient(shipmentChannel);

        using var routeChannel = CreateChannel(config.RouteGrpcUrl);
        var routeClient = new RoutePlanningService.RoutePlanningServiceClient(routeChannel);

        // 1. Discover shipment candidates
        Console.WriteLine("Discovering shipment candidates...");
        var discoveryService = new ShipmentDiscoveryService(shipmentClient, headers);
        List<ShipmentCandidate> candidates;
        try
        {
            candidates = await discoveryService.DiscoverAsync(config, cts.Token);
        }
        catch (KeyNotFoundException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 2;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            Console.WriteLine("Simulation cancelled during discovery.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error during shipment discovery: {ex.Message}");
            return 3;
        }

        if (candidates.Count == 0)
        {
            Console.WriteLine("[WARN] No eligible shipments found. (Make sure shipments have vehicles assigned or use --auto-assign).");
            return 0;
        }

        Console.WriteLine($"Found {candidates.Count} candidate shipment(s).");

        // 2. Trajectory provider
        var trajectoryProvider = new CompositeTrajectoryProvider(routeClient, headers);
        var rnd = config.RandomSeed.HasValue ? new Random(config.RandomSeed.Value) : new Random();

        // 3. Build VehicleSimulation objects
        Console.WriteLine("Building vehicle simulation models and trajectories...");
        var vehicles = new List<VehicleSimulation>();
        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            var trajectory = await trajectoryProvider.GetAsync(candidate, cts.Token);

            var behavior = config.Behavior switch
            {
                SimulationBehavior.Mixed => (rnd.Next(100)) switch
                {
                    < 70 => SimulationBehavior.Normal,
                    < 85 => SimulationBehavior.Deviation,
                    _ => SimulationBehavior.SignalLoss
                },
                _ => config.Behavior
            };

            var vehicle = new VehicleSimulation
            {
                ShipmentId = candidate.ShipmentId,
                VehicleId = candidate.VehicleId ?? $"sim-veh-{i + 1:D4}",
                DeviceId = $"sim-dev-{candidate.VehicleId ?? $"veh-{i + 1:D4}"}",
                RouteId = candidate.RouteId,
                Trajectory = trajectory,
                CurrentPosition = trajectory.Points[0],
                CurrentSpeed = config.BaseSpeedKph,
                CurrentWaypointIndex = 0,
                ExternalReadingSequence = 0,
                Behavior = behavior
            };

            vehicles.Add(vehicle);
        }

        Console.WriteLine($"Initialized {vehicles.Count} active vehicle simulation(s).");
        Console.WriteLine();

        // 4. Start concurrent simulation
        var telemetryGenerator = new TelemetryGenerator(config.BaseSpeedKph, config.RandomSeed);
        var metrics = new SimulationMetrics();
        var runner = new SimulationRunner(gpsClient, telemetryGenerator, metrics, headers);

        int exitCode = await runner.RunAsync(vehicles, config, cts.Token);
        return exitCode;
    }

    private static GrpcChannel CreateChannel(string url)
    {
        return GrpcChannel.ForAddress(url, new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true
            }
        });
    }
}

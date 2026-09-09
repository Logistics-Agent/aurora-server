using Google.Protobuf.WellKnownTypes;
using GpsTracking.Grpc;
using Grpc.Core;
using Grpc.Net.Client;
using ShipmentWorkflow.Grpc;

namespace GpsSimulator;

public class Program
{
    private const string Usage =
        "Usage: gps-simulator --shipment <uuid> [--interval <sec>] [--speed <kmh>] [--grpc <url>] [--tenant <id>]";

    public static async Task<int> Main(string[] args)
    {
        string? shipmentId = null;
        int intervalSeconds = 3;
        double baseSpeed = 50.0;
        string gpsGrpcUrl = Environment.GetEnvironmentVariable("GPS_GRPC_URL") ?? "http://localhost:6002";
        string shipmentGrpcUrl = Environment.GetEnvironmentVariable("SHIPMENT_GRPC_URL") ?? "http://localhost:6000";
        string tenantId = Environment.GetEnvironmentVariable("TENANT_ID") ?? "01920000-0000-7000-8000-000000000001";
        string userId = Environment.GetEnvironmentVariable("USER_ID") ?? "01910000-0000-7000-8000-000000000001";

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is "--help" or "-h")
            {
                Console.WriteLine(Usage);
                return 0;
            }

            if (i + 1 >= args.Length)
            {
                return PrintUsageError($"Missing value for '{args[i]}'.");
            }

            string option = args[i];
            string value = args[++i];
            switch (option)
            {
                case "--shipment":
                    shipmentId = value;
                    break;
                case "--interval" when int.TryParse(value, out var parsedInterval):
                    intervalSeconds = Math.Max(1, parsedInterval);
                    break;
                case "--speed" when double.TryParse(value, out var parsedSpeed):
                    baseSpeed = Math.Max(10.0, parsedSpeed);
                    break;
                case "--grpc":
                    gpsGrpcUrl = value;
                    break;
                case "--tenant":
                    tenantId = value;
                    break;
                default:
                    return PrintUsageError($"Invalid argument or value near '{option}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(shipmentId) || !Guid.TryParse(shipmentId, out _))
        {
            return PrintUsageError("--shipment is required and must be a valid UUID.");
        }

        using var cts = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        Console.CancelKeyPress += cancelHandler;

        Console.WriteLine("Aurora GPS Simulator");
        Console.WriteLine($"Shipment : {shipmentId}");
        Console.WriteLine($"Interval : {intervalSeconds}s  Speed: {baseSpeed:0.#} km/h");
        Console.WriteLine($"GPS URL  : {gpsGrpcUrl}");
        Console.WriteLine($"Shipment URL: {shipmentGrpcUrl}");
        Console.WriteLine($"Tenant   : {tenantId}");
        Console.WriteLine();

        var headers = new Metadata
        {
            { "x-tenant-id", tenantId },
            { "x-user-id", userId }
        };

        Console.WriteLine("Validating shipment...");
        try
        {
            using var shipmentChannel = CreateChannel(shipmentGrpcUrl);
            var shipmentClient = new ShipmentWorkflowService.ShipmentWorkflowServiceClient(shipmentChannel);
            var shipment = await shipmentClient.GetShipmentAsync(
                new GetShipmentRequest { Id = shipmentId },
                headers,
                deadline: DateTime.UtcNow.AddSeconds(10),
                cancellationToken: cts.Token);

            Console.WriteLine($"Shipment found: {shipment.CustomerName} ({shipment.Status})");
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            Console.Error.WriteLine($"Error: Shipment '{shipmentId}' not found.");
            return 2;
        }
        catch (RpcException ex) when (cts.IsCancellationRequested && ex.StatusCode == StatusCode.Cancelled)
        {
            Console.WriteLine("Simulation cancelled.");
            return 130;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            Console.WriteLine("Simulation cancelled.");
            return 130;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"Error: Cannot connect to Shipment Workflow Service at {shipmentGrpcUrl}: {ex.Message}");
            return 3;
        }

        var waypoints = new (double Lat, double Lng)[]
        {
            (9.9333, -84.0833), // San Jose Hub
            (9.8653, -83.9189), // Cartago
            (9.3789, -83.7042), // San Isidro
            (8.9667, -83.5167), // Palmar Norte
            (8.5333, -82.8333), // Paso Canoas Border
            (8.4333, -82.4333), // David Hub
            (8.2333, -81.7500), // Tole
            (8.1000, -80.9667), // Santiago
            (8.4000, -80.4167), // Penonome
            (8.9824, -79.5199), // Panama City Logistics Center
        };

        var routePoints = InterpolateRoute(waypoints, 20);
        Console.WriteLine("Route: San Jose CR -> Panama City PA (hardcoded ROAD corridor)");
        Console.WriteLine($"Route points: {routePoints.Count}");
        Console.WriteLine();

        using var gpsChannel = CreateChannel(gpsGrpcUrl);
        var gpsClient = new GpsTrackingService.GpsTrackingServiceClient(gpsChannel);
        var random = new Random();
        int successCount = 0;
        int failCount = 0;

        // vehicle_id = shipmentId (UUID string).
        // GPS Tracking links it to a shipment when a VehicleShipmentAssignment exists.
        // The demo UI queries type=vehicle so movement is visible with or without that assignment.
        for (int i = 0; i < routePoints.Count && !cts.IsCancellationRequested; i++)
        {
            var point = routePoints[i];
            var nextPoint = i < routePoints.Count - 1 ? routePoints[i + 1] : point;
            double heading = CalculateHeading(point.Lat, point.Lng, nextPoint.Lat, nextPoint.Lng);
            double speed = Math.Round(baseSpeed + random.NextDouble() * 6 - 3, 1);

            var request = new IngestPositionRequest
            {
                ExternalReadingId = $"sim-{Guid.NewGuid():N}",
                DeviceId = $"sim-dev-{shipmentId}",
                VehicleId = shipmentId,
                Latitude = point.Lat,
                Longitude = point.Lng,
                SpeedKph = speed,
                HeadingDegrees = heading,
                AccuracyMeters = 3.5,
                RecordedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
            };

            try
            {
                await gpsClient.IngestPositionAsync(
                    request,
                    headers,
                    deadline: DateTime.UtcNow.AddSeconds(5),
                    cancellationToken: cts.Token);
                successCount++;
            }
            catch (RpcException ex) when (cts.IsCancellationRequested && ex.StatusCode == StatusCode.Cancelled)
            {
                break;
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                failCount++;
                Console.WriteLine($"[WARN] IngestPosition failed: {ex.Message}");
            }

            Console.WriteLine(
                $"[{i + 1}/{routePoints.Count}] {point.Lat:F4},{point.Lng:F4} | {speed:F0} km/h");

            if (i == routePoints.Count - 1)
            {
                continue;
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Console.WriteLine();
        if (cts.IsCancellationRequested)
        {
            Console.WriteLine($"Simulation cancelled. {successCount} sent, {failCount} failed.");
            return 130;
        }

        Console.WriteLine($"Simulation completed. {successCount} sent, {failCount} failed.");
        return failCount == 0 ? 0 : 4;
    }

    private static int PrintUsageError(string message)
    {
        Console.Error.WriteLine(Usage);
        Console.Error.WriteLine($"Error: {message}");
        return 1;
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

    private static List<(double Lat, double Lng)> InterpolateRoute(
        (double Lat, double Lng)[] waypoints,
        int stepsBetween)
    {
        var result = new List<(double Lat, double Lng)>();
        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            var start = waypoints[i];
            var end = waypoints[i + 1];
            for (int step = 0; step < stepsBetween; step++)
            {
                double progress = (double)step / stepsBetween;
                double latitude = start.Lat + progress * (end.Lat - start.Lat);
                double longitude = start.Lng + progress * (end.Lng - start.Lng);
                result.Add((latitude, longitude));
            }
        }

        result.Add(waypoints[^1]);
        return result;
    }

    private static double CalculateHeading(double lat1, double lon1, double lat2, double lon2)
    {
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double y = Math.Sin(dLon) * Math.Cos(lat2 * Math.PI / 180.0);
        double x = Math.Cos(lat1 * Math.PI / 180.0) * Math.Sin(lat2 * Math.PI / 180.0) -
                   Math.Sin(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) * Math.Cos(dLon);
        double radians = Math.Atan2(y, x);
        double degrees = (radians * 180.0 / Math.PI + 360.0) % 360.0;
        return Math.Round(degrees, 1);
    }
}

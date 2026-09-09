using Google.Protobuf.WellKnownTypes;
using GpsTracking.Grpc;
using Grpc.Core;
using Grpc.Net.Client;

namespace GpsSimulator;

public class Program
{
    public static async Task Main(string[] args)
    {
        string shipmentId = "SHP-2026-00128";
        int intervalSeconds = 3;
        double baseSpeed = 50.0;
        string grpcUrl = Environment.GetEnvironmentVariable("GPS_GRPC_URL") ?? "http://localhost:5004";
        string tenantId = Environment.GetEnvironmentVariable("TENANT_ID") ?? "00000000-0000-0000-0000-000000000001";

        // Parse CLI arguments
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--shipment" && i + 1 < args.Length)
            {
                shipmentId = args[++i];
            }
            else if (args[i] == "--interval" && i + 1 < args.Length && int.TryParse(args[++i], out var parsedInterval))
            {
                intervalSeconds = Math.Max(1, parsedInterval);
            }
            else if (args[i] == "--speed" && i + 1 < args.Length && double.TryParse(args[++i], out var parsedSpeed))
            {
                baseSpeed = Math.Max(10.0, parsedSpeed);
            }
            else if (args[i] == "--grpc" && i + 1 < args.Length)
            {
                grpcUrl = args[++i];
            }
            else if (args[i] == "--tenant" && i + 1 < args.Length)
            {
                tenantId = args[++i];
            }
        }

        Console.WriteLine("Aurora GPS Simulator");
        Console.WriteLine($"Shipment: {shipmentId}");

        // Central America Corridor Waypoints (San Jose CR -> Paso Canoas Border -> David -> Panama City)
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

        // Generate dense interpolated points along the corridor
        var routePoints = InterpolateRoute(waypoints, 20);
        Console.WriteLine($"Route points: {routePoints.Count}");
        Console.WriteLine();

        using var channel = GrpcChannel.ForAddress(grpcUrl, new GrpcChannelOptions
        {
            HttpHandler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true
            }
        });
        var client = new GpsTrackingService.GpsTrackingServiceClient(channel);

        var random = new Random();
        for (int i = 0; i < routePoints.Count; i++)
        {
            var pt = routePoints[i];
            var nextPt = i < routePoints.Count - 1 ? routePoints[i + 1] : routePoints[i];
            double heading = CalculateHeading(pt.Lat, pt.Lng, nextPt.Lat, nextPt.Lng);
            double speed = Math.Round(baseSpeed + (random.NextDouble() * 6 - 3), 1);

            var request = new IngestPositionRequest
            {
                ExternalReadingId = $"sim-{Guid.NewGuid():N}",
                DeviceId = $"sim-dev-{shipmentId}",
                VehicleId = shipmentId,
                Latitude = pt.Lat,
                Longitude = pt.Lng,
                SpeedKph = speed,
                HeadingDegrees = heading,
                AccuracyMeters = 3.5,
                RecordedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
            };

            var headers = new Metadata
            {
                { "x-tenant-id", tenantId },
                { "x-user-id", "00000000-0000-0000-0000-000000000002" }
            };

            try
            {
                await client.IngestPositionAsync(request, headers);
            }
            catch (Exception ex)
            {
                // In demo offline mode, simulator prints output cleanly
            }

            Console.WriteLine($"[{i + 1}/{routePoints.Count}] {pt.Lat:F4},{pt.Lng:F4} | {speed:F0} km/h");
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds));
        }

        Console.WriteLine();
        Console.WriteLine("ROAD leg destination reached. Simulation completed.");
    }

    private static List<(double Lat, double Lng)> InterpolateRoute((double Lat, double Lng)[] waypoints, int stepsBetween)
    {
        var result = new List<(double Lat, double Lng)>();
        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            var p1 = waypoints[i];
            var p2 = waypoints[i + 1];
            for (int s = 0; s < stepsBetween; s++)
            {
                double t = (double)s / stepsBetween;
                double lat = p1.Lat + t * (p2.Lat - p1.Lat);
                double lng = p1.Lng + t * (p2.Lng - p1.Lng);
                result.Add((lat, lng));
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

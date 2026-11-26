using GpsSimulator.Models;
using Grpc.Core;
using RoutePlanningAgent.Grpc;

namespace GpsSimulator.Services;

public interface ITrajectoryProvider
{
    Task<RouteTrajectory> GetAsync(
        ShipmentCandidate shipment,
        CancellationToken cancellationToken);
}

public class CompositeTrajectoryProvider : ITrajectoryProvider
{
    private static readonly (double Lat, double Lng)[] DefaultCorridorWaypoints =
    [
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
    ];

    private readonly RoutePlanningService.RoutePlanningServiceClient? _routeClient;
    private readonly Metadata _headers;

    public CompositeTrajectoryProvider(
        RoutePlanningService.RoutePlanningServiceClient? routeClient,
        Metadata headers)
    {
        _routeClient = routeClient;
        _headers = headers;
    }

    public async Task<RouteTrajectory> GetAsync(
        ShipmentCandidate shipment,
        CancellationToken cancellationToken)
    {
        // 1. Try Route Planning service if RouteId is provided
        if (!string.IsNullOrWhiteSpace(shipment.RouteId) && _routeClient != null)
        {
            try
            {
                var response = await _routeClient.GetRouteAsync(
                    new GetRouteRequest { Id = shipment.RouteId },
                    _headers,
                    deadline: DateTime.UtcNow.AddSeconds(5),
                    cancellationToken: cancellationToken);

                if (response?.Stops != null && response.Stops.Count >= 2)
                {
                    var stops = response.Stops
                        .OrderBy(s => s.Sequence)
                        .Select(s => (s.Latitude, s.Longitude))
                        .ToList();

                    string desc = $"Route Planning '{response.Name ?? shipment.RouteId}' ({stops.Count} stops)";
                    return RouteTrajectory.FromWaypoints(stops, desc);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Could not load route '{shipment.RouteId}' from Route Planning Service: {ex.Message}. Falling back.");
            }
        }

        // 2. Try Shipment locations if available
        if (shipment.Locations != null && shipment.Locations.Count >= 2)
        {
            var waypoints = shipment.Locations
                .OrderBy(l => l.Sequence)
                .Select(l => (l.Latitude, l.Longitude))
                .ToList();

            string desc = $"Shipment Locations ({waypoints.Count} waypoints)";
            return RouteTrajectory.FromWaypoints(waypoints, desc);
        }

        // 3. Fallback to hardcoded corridor
        return RouteTrajectory.FromWaypoints(
            DefaultCorridorWaypoints,
            "Default Road Corridor: San Jose CR -> Panama City PA");
    }
}


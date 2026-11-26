namespace GpsSimulator.Models;

public record WaypointLocation(double Latitude, double Longitude, int Sequence, string? Name = null);

public class ShipmentCandidate
{
    public required string ShipmentId { get; init; }
    public string? VehicleId { get; set; }
    public string? RouteId { get; init; }
    public string CustomerName { get; init; } = "Unknown";
    public string Status { get; init; } = "UNKNOWN";
    public List<WaypointLocation> Locations { get; init; } = new();
}


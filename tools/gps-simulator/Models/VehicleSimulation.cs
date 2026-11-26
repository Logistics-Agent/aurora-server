namespace GpsSimulator.Models;

public class VehicleSimulation
{
    public required string ShipmentId { get; init; }
    public required string VehicleId { get; init; }
    public required string DeviceId { get; init; }
    public string? RouteId { get; init; }
    public required RouteTrajectory Trajectory { get; init; }
    public (double Lat, double Lng) CurrentPosition { get; set; }
    public double CurrentSpeed { get; set; }
    public double Heading { get; set; }
    public int CurrentWaypointIndex { get; set; }
    public long ExternalReadingSequence { get; set; }
    public SimulationBehavior Behavior { get; set; } = SimulationBehavior.Normal;

    // Behavioral state helpers
    public int SignalLossRemainingSteps { get; set; }
    public double DeviationLatOffset { get; set; }
    public double DeviationLngOffset { get; set; }
}


using GpsSimulator.Models;
using GpsSimulator.Services;

namespace GpsSimulator.Tests;

public class TelemetryGeneratorTests
{
    public void GenerateNext_IncrementsSequenceAndAdvancesWaypoints()
    {
        var waypoints = new (double Lat, double Lng)[]
        {
            (10.0, 20.0),
            (10.5, 20.5),
            (11.0, 21.0)
        };
        var trajectory = RouteTrajectory.FromWaypoints(waypoints, "Test", stepsBetween: 5);

        var vehicle = new VehicleSimulation
        {
            ShipmentId = "ship-1",
            VehicleId = "veh-1",
            DeviceId = "sim-dev-veh-1",
            Trajectory = trajectory,
            CurrentPosition = trajectory.Points[0],
            CurrentSpeed = 50.0,
            Behavior = SimulationBehavior.Normal
        };

        var generator = new TelemetryGenerator(baseSpeed: 50.0, seed: 12345);

        var req1 = generator.GenerateNext(vehicle);
        if (req1 == null) throw new Exception("Expected request not null");
        if (req1.ExternalReadingId != "sim-veh-1-00000001") throw new Exception($"Unexpected reading id: {req1.ExternalReadingId}");
        if (req1.VehicleId != "veh-1") throw new Exception("Unexpected vehicle id");
        if (req1.DeviceId != "sim-dev-veh-1") throw new Exception("Unexpected device id");
        if (vehicle.ExternalReadingSequence != 1) throw new Exception("Sequence not incremented to 1");

        var req2 = generator.GenerateNext(vehicle);
        if (req2 == null) throw new Exception("Expected request not null");
        if (req2.ExternalReadingId != "sim-veh-1-00000002") throw new Exception($"Unexpected reading id: {req2.ExternalReadingId}");
        if (vehicle.ExternalReadingSequence != 2) throw new Exception("Sequence not incremented to 2");
    }

    public void GenerateNext_DeviationBehavior_AppliesOffset()
    {
        var waypoints = new (double Lat, double Lng)[]
        {
            (10.0, 20.0),
            (11.0, 21.0)
        };
        var trajectory = RouteTrajectory.FromWaypoints(waypoints, "Test", stepsBetween: 10);

        var vehicle = new VehicleSimulation
        {
            ShipmentId = "ship-dev",
            VehicleId = "veh-dev",
            DeviceId = "sim-dev-veh-dev",
            Trajectory = trajectory,
            Behavior = SimulationBehavior.Deviation
        };

        var generator = new TelemetryGenerator(baseSpeed: 60.0, seed: 42);

        var req = generator.GenerateNext(vehicle);
        if (req == null) throw new Exception("Expected request not null");
        if (vehicle.DeviationLatOffset == 0.0 && vehicle.DeviationLngOffset == 0.0)
        {
            throw new Exception("Expected deviation offsets to be non-zero");
        }
        if (Math.Abs(req.AccuracyMeters - 8.0) > 0.01)
        {
            throw new Exception("Expected deviation accuracy to be 8.0");
        }
    }
}

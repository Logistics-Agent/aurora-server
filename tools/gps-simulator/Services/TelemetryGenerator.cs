using Google.Protobuf.WellKnownTypes;
using GpsSimulator.Models;
using GpsTracking.Grpc;

namespace GpsSimulator.Services;

public class TelemetryGenerator
{
    private readonly Random _random;
    private readonly double _baseSpeed;

    public TelemetryGenerator(double baseSpeed, int? seed = null)
    {
        _baseSpeed = baseSpeed;
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public IngestPositionRequest? GenerateNext(VehicleSimulation vehicle)
    {
        var points = vehicle.Trajectory.Points;
        if (points.Count == 0)
        {
            return null;
        }

        // Handle Behavior: SignalLoss
        if (vehicle.Behavior == SimulationBehavior.SignalLoss)
        {
            if (vehicle.SignalLossRemainingSteps > 0)
            {
                vehicle.SignalLossRemainingSteps--;
                return null; // Skip sending during signal loss window
            }

            // 15% probability to enter signal loss for 2-5 intervals
            if (_random.NextDouble() < 0.15)
            {
                vehicle.SignalLossRemainingSteps = _random.Next(2, 6);
                return null;
            }
        }

        int currentIndex = vehicle.CurrentWaypointIndex;
        var currentPoint = points[currentIndex];

        // Advance index for next time (loop around to support long-duration simulation)
        int nextIndex = (currentIndex + 1) % points.Count;
        vehicle.CurrentWaypointIndex = nextIndex;

        var nextPoint = points[nextIndex];
        double heading = RouteTrajectory.CalculateHeading(
            currentPoint.Lat, currentPoint.Lng,
            nextPoint.Lat, nextPoint.Lng);

        // Speed variation (+/- 4 km/h)
        double speed = Math.Round(_baseSpeed + (_random.NextDouble() * 8.0 - 4.0), 1);
        if (speed < 0) speed = 0;

        double lat = currentPoint.Lat;
        double lng = currentPoint.Lng;
        double accuracy = 3.5;

        // Handle Behavior: Deviation
        if (vehicle.Behavior == SimulationBehavior.Deviation)
        {
            // Gradually drift or deviate from route
            vehicle.DeviationLatOffset += (_random.NextDouble() - 0.5) * 0.002;
            vehicle.DeviationLngOffset += (_random.NextDouble() - 0.5) * 0.002;
            lat += vehicle.DeviationLatOffset;
            lng += vehicle.DeviationLngOffset;
            accuracy = 8.0;
        }

        vehicle.CurrentPosition = (lat, lng);
        vehicle.CurrentSpeed = speed;
        vehicle.Heading = heading;
        vehicle.ExternalReadingSequence++;

        return new IngestPositionRequest
        {
            ExternalReadingId = $"sim-{vehicle.VehicleId}-{vehicle.ExternalReadingSequence:D8}",
            DeviceId = vehicle.DeviceId,
            VehicleId = vehicle.VehicleId,
            Latitude = lat,
            Longitude = lng,
            SpeedKph = speed,
            HeadingDegrees = heading,
            AccuracyMeters = accuracy,
            RecordedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
        };
    }
}


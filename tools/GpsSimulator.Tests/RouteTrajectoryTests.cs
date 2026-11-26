using GpsSimulator.Models;

namespace GpsSimulator.Tests;

public class RouteTrajectoryTests
{
    public void FromWaypoints_InterpolatesCorrectNumberOfPoints()
    {
        var waypoints = new (double Lat, double Lng)[]
        {
            (10.0, 20.0),
            (11.0, 21.0),
            (12.0, 22.0)
        };

        var trajectory = RouteTrajectory.FromWaypoints(waypoints, "Test Route", stepsBetween: 10);

        // 2 segments * 10 steps + 1 end point = 21 points
        if (trajectory.Points.Count != 21) throw new Exception($"Expected 21 points, got {trajectory.Points.Count}");
        if (Math.Abs(trajectory.Points[0].Lat - 10.0) > 0.0001) throw new Exception("Start Lat mismatch");
        if (Math.Abs(trajectory.Points[0].Lng - 20.0) > 0.0001) throw new Exception("Start Lng mismatch");
        if (Math.Abs(trajectory.Points[^1].Lat - 12.0) > 0.0001) throw new Exception("End Lat mismatch");
        if (Math.Abs(trajectory.Points[^1].Lng - 22.0) > 0.0001) throw new Exception("End Lng mismatch");
    }

    public void CalculateHeading_HeadingCalculationsAreAccurate()
    {
        // Due North
        double north = RouteTrajectory.CalculateHeading(0.0, 0.0, 1.0, 0.0);
        if (Math.Abs(north - 0.0) > 0.5) throw new Exception($"Expected North 0, got {north}");

        // Due East
        double east = RouteTrajectory.CalculateHeading(0.0, 0.0, 0.0, 1.0);
        if (Math.Abs(east - 90.0) > 0.5) throw new Exception($"Expected East 90, got {east}");

        // Due South
        double south = RouteTrajectory.CalculateHeading(1.0, 0.0, 0.0, 0.0);
        if (Math.Abs(south - 180.0) > 0.5) throw new Exception($"Expected South 180, got {south}");

        // Due West
        double west = RouteTrajectory.CalculateHeading(0.0, 1.0, 0.0, 0.0);
        if (Math.Abs(west - 270.0) > 0.5) throw new Exception($"Expected West 270, got {west}");
    }

    public void HaversineDistance_ReturnsRealisticDistance()
    {
        // Distance from San Jose CR (9.9333, -84.0833) to Panama City (8.9824, -79.5199) is ~510-540 km
        double distance = RouteTrajectory.HaversineDistanceKm(9.9333, -84.0833, 8.9824, -79.5199);
        if (distance < 500.0 || distance > 550.0) throw new Exception($"Distance {distance} not in range [500, 550]");
    }
}

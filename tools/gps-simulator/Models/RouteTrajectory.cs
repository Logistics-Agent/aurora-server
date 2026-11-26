namespace GpsSimulator.Models;

public class RouteTrajectory
{
    public IReadOnlyList<(double Lat, double Lng)> Waypoints { get; }
    public IReadOnlyList<(double Lat, double Lng)> Points { get; }
    public string Description { get; }

    public RouteTrajectory(
        IReadOnlyList<(double Lat, double Lng)> waypoints,
        IReadOnlyList<(double Lat, double Lng)> points,
        string description)
    {
        Waypoints = waypoints;
        Points = points;
        Description = description;
    }

    public static RouteTrajectory FromWaypoints(
        IReadOnlyList<(double Lat, double Lng)> waypoints,
        string description,
        int stepsBetween = 20)
    {
        if (waypoints == null || waypoints.Count == 0)
        {
            throw new ArgumentException("Waypoints cannot be empty.", nameof(waypoints));
        }

        if (waypoints.Count == 1)
        {
            return new RouteTrajectory(waypoints, new List<(double Lat, double Lng)> { waypoints[0] }, description);
        }

        var result = new List<(double Lat, double Lng)>();
        for (int i = 0; i < waypoints.Count - 1; i++)
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
        return new RouteTrajectory(waypoints, result, description);
    }

    public static double CalculateHeading(double lat1, double lon1, double lat2, double lon2)
    {
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double y = Math.Sin(dLon) * Math.Cos(lat2 * Math.PI / 180.0);
        double x = Math.Cos(lat1 * Math.PI / 180.0) * Math.Sin(lat2 * Math.PI / 180.0) -
                   Math.Sin(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) * Math.Cos(dLon);
        double radians = Math.Atan2(y, x);
        double degrees = (radians * 180.0 / Math.PI + 360.0) % 360.0;
        return Math.Round(degrees, 1);
    }

    public static double HaversineDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371.0; // Earth radius in km
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return r * c;
    }
}


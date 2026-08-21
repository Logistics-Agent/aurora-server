namespace GpsTracking.Application.Monitoring;

public static class GeofenceDistanceCalculator
{
    private const double EarthRadiusMeters = 6_371_000;

    public static decimal DistanceMeters(
        decimal latitude1,
        decimal longitude1,
        decimal latitude2,
        decimal longitude2)
    {
        var latitudeDelta = ToRadians((double)(latitude2 - latitude1));
        var longitudeDelta = ToRadians((double)(longitude2 - longitude1));
        var firstLatitude = ToRadians((double)latitude1);
        var secondLatitude = ToRadians((double)latitude2);
        var haversine = Math.Pow(Math.Sin(latitudeDelta / 2), 2)
            + Math.Cos(firstLatitude) * Math.Cos(secondLatitude)
            * Math.Pow(Math.Sin(longitudeDelta / 2), 2);
        var distance = EarthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1 - haversine));
        return decimal.Round(Convert.ToDecimal(distance), 2);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}

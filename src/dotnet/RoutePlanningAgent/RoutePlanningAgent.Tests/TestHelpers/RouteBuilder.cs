using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Domain.Enums;
using Route = RoutePlanningAgent.Domain.Route;

namespace RoutePlanningAgent.Tests.TestHelpers;

public static class RouteBuilder
{
    public static Route Build(
        Guid? tenantId = null,
        RouteStatus status = RouteStatus.Draft,
        decimal maxWeightKg = 1000m,
        int stopCount = 3)
    {
        var route = new Route
        {
            Name = "Test Route",
            Description = "Route dùng cho unit test",
            Type = RouteType.Fixed,
            Status = status,
            RiskLevel = Shared.Enums.RouteRiskLevel.Low,
            MaxWeightKg = maxWeightKg,
            MaxVolumeM3 = 10m,
            EstimatedDistanceKm = 0m,
            EstimatedDurationMinutes = 0,
            TenantId = tenantId ?? TestDb.TenantId
        };

        for (var i = 1; i <= stopCount; i++)
        {
            route.Stops.Add(new RouteStop
            {
                Sequence = i,
                StopType = i == 1 ? StopType.Warehouse : StopType.Delivery,
                LocationName = $"Stop {i}",
                Address = $"Địa chỉ {i}",
                Latitude = 10.7 + i * 0.01,
                Longitude = 106.6 + i * 0.01,
                EstimatedArrivalMinutes = 0,
                ServiceDurationMinutes = 10,
                Route = route
            });
        }

        return route;
    }
}

using System;
using RoutePlanningAgent.Domain.Enums;
using Shared.Entity;

namespace RoutePlanningAgent.Domain;

public class RouteStop : BaseEntity
{
    public Guid RouteId { get; set; }
    public int Sequence { get; set; }
    public StopType StopType { get; set; }
    public string LocationName { get; set; } = default!;
    public string Address { get; set; } = default!;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int EstimatedArrivalMinutes { get; set; }
    public int ServiceDurationMinutes { get; set; }
    public Route Route { get; set; } = default!;
}

namespace RoutePlanningAgent.Application.DTOs.Routes;

public record RouteStopInputDto
{
    public int Sequence { get; init; }
    public string StopType { get; init; } = string.Empty;
    public string LocationName { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public int EstimatedArrivalMinutes { get; init; }
    public int ServiceDurationMinutes { get; init; }
}

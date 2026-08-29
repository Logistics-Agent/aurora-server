using System;
using System.Collections.Generic;

namespace RoutePlanningAgent.Application.DTOs.Routes;

public record RouteDto
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string RouteType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string RiskLevel { get; init; } = string.Empty;
    public string GovernanceDecision { get; init; } = string.Empty;
    public decimal EstimatedDistanceKm { get; init; }
    public int EstimatedDurationMinutes { get; init; }
    public decimal MaxWeightKg { get; init; }
    public decimal MaxVolumeM3 { get; init; }
    public bool IsAiGenerated { get; init; }
    public DateTime? OptimizedAt { get; init; }
    public int Version { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public List<RouteStopDto> Stops { get; init; } = [];
}

public record RouteStopDto
{
    public Guid Id { get; init; }
    public int Sequence { get; init; }
    public string StopType { get; init; } = string.Empty;
    public string LocationName { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public int EstimatedArrivalMinutes { get; init; }
    public int ServiceDurationMinutes { get; init; }
}

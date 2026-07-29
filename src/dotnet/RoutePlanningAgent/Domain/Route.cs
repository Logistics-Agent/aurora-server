using System;
using System.Collections.Generic;
using Shared.Entity;
using Shared.Enums;
using RoutePlanningAgent.Domain.Enums;

namespace RoutePlanningAgent.Domain;

public class Route : TenantAuditableEntity
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public RouteType Type { get; set; }
    public RouteStatus Status { get; set; }
    public RouteRiskLevel RiskLevel { get; set; }
    public decimal EstimatedDistanceKm { get; set; }
    public int EstimatedDurationMinutes { get; set; }
    public decimal MaxWeightKg { get; set; }
    public decimal MaxVolumeM3 { get; set; }
    public bool IsAiGenerated { get; set; }
    public DateTime? OptimizedAt { get; set; }
    public int Version { get; set; } = 1;
    public bool IsDeleted { get; set; }
    public ICollection<RouteStop> Stops { get; set; } = new List<RouteStop>();
    public ICollection<RouteOptimizationHistory> OptimizationHistories { get; set; } = new List<RouteOptimizationHistory>();
}

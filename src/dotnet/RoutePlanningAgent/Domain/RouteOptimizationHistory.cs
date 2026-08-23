using System;
using Shared.Entity;

namespace RoutePlanningAgent.Domain;

public class RouteOptimizationHistory : BaseEntity
{
    public Guid RouteId { get; set; }
    public string Provider { get; set; } = default!;
    public string Model { get; set; } = default!;
    public string PromptVersion { get; set; } = default!;
    public decimal TotalDistanceKm { get; set; }
    public int TotalDurationMinutes { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

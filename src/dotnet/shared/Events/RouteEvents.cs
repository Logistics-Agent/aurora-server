using MassTransit;
using System;

namespace Shared.Events;

[EntityName("route_created_event")]
public record RouteCreatedEvent
{
    public Guid RouteId { get; init; }
    public Guid TenantId { get; init; }
    public string RouteName { get; init; } = string.Empty;
    public string RouteType { get; init; } = string.Empty;
    public string RiskLevel { get; init; } = string.Empty;
    public Guid CreatedByUserId { get; init; }
}

[EntityName("route_optimized_event")]
public record RouteOptimizedEvent
{
    public Guid RouteId { get; init; }
    public Guid TenantId { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public decimal TotalDistanceKm { get; init; }
    public int TotalDurationMinutes { get; init; }
}

[EntityName("route_approval_requested_event")]
public record RouteApprovalRequestedEvent
{
    public Guid ApprovalRequestId { get; init; }
    public Guid RouteId { get; init; }
    public Guid TenantId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string AiSummary { get; init; } = string.Empty;
}

[EntityName("tenant_ai_config_changed_event")]
public record TenantAiConfigChangedEvent
{
    public Guid TenantId { get; init; }
    public string Feature { get; init; } = string.Empty;
}

[EntityName("tenant_rule_config_changed_event")]
public record TenantRuleConfigChangedEvent
{
    public Guid TenantId { get; init; }
    public string RuleName { get; init; } = string.Empty; // Empty = invalidate all rules
}

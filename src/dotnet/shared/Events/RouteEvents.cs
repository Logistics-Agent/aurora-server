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

[EntityName("route_updated_event")]
public record RouteUpdatedEvent
{
    public Guid RouteId { get; init; }
    public Guid TenantId { get; init; }
    public string RouteName { get; init; } = string.Empty;
    public int Version { get; init; }
    public Guid UpdatedByUserId { get; init; }
}

[EntityName("route_deleted_event")]
public record RouteDeletedEvent
{
    public Guid RouteId { get; init; }
    public Guid TenantId { get; init; }
    public Guid DeletedByUserId { get; init; }
}

[EntityName("route_status_changed_event")]
public record RouteStatusChangedEvent
{
    public Guid RouteId { get; init; }
    public Guid TenantId { get; init; }
    public string OldStatus { get; init; } = string.Empty;
    public string NewStatus { get; init; } = string.Empty;
    public Guid ChangedByUserId { get; init; }
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

[EntityName("route_risk_evaluated_event")]
public record RouteRiskEvaluatedEvent
{
    public Guid RouteId { get; init; }
    public int RouteVersion { get; init; } = 1;
    public Guid TenantId { get; init; }
    public string RiskLevel { get; init; } = string.Empty;
    public string GovernanceDecision { get; init; } = string.Empty;
    public string PolicyId { get; init; } = string.Empty;
    public int PolicyVersion { get; init; } = 1;
    public string PolicySource { get; init; } = string.Empty;
    public string[] MatchedRuleCodes { get; init; } = [];
    public string Source { get; init; } = string.Empty;
    public Guid EvaluatedByUserId { get; init; }
}

[EntityName("route_approved_event")]
public record RouteApprovedEvent
{
    public Guid ApprovalRequestId { get; init; }
    public Guid RouteId { get; init; }
    public Guid TenantId { get; init; }
    public Guid ReviewerUserId { get; init; }
    public string? Comment { get; init; }
}

[EntityName("route_rejected_event")]
public record RouteRejectedEvent
{
    public Guid ApprovalRequestId { get; init; }
    public Guid RouteId { get; init; }
    public Guid TenantId { get; init; }
    public Guid ReviewerUserId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? Comment { get; init; }
}

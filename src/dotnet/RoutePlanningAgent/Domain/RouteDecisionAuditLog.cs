using System;
using Shared.Entity;
using Shared.Enums;

namespace RoutePlanningAgent.Domain;

public class RouteDecisionAuditLog : TenantAuditableEntity
{
    public Guid RouteId { get; set; }
    public AutomationPolicy PolicyApplied { get; set; }
    public string RiskLevel { get; set; } = default!;
    public string RuleResultsJson { get; set; } = default!;
    public bool ComplianceCheckPerformed { get; set; }
    public string? ComplianceDocumentRefs { get; set; }
    public string? ComplianceSummary { get; set; }
    public string? LlmProvider { get; set; }
    public string? LlmModel { get; set; }
    public string? LlmSummary { get; set; }
    public string AutomationDecision { get; set; } = default!;
    public Guid? ApprovalRequestId { get; set; }
    public Guid RequestedByUserId { get; set; }
}

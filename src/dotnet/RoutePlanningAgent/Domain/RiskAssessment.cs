using System;
using Shared.Entity;
using Shared.Enums;

namespace RoutePlanningAgent.Domain;

public class RiskAssessment : TenantAuditableEntity
{
    public Guid RouteId { get; set; }
    public int RouteVersion { get; set; } = 1;
    public RouteRiskLevel RiskLevel { get; set; }
    public double? RiskScore { get; set; }
    public double? ConfidenceScore { get; set; }
    public string Source { get; set; } = default!; // DeterministicRules | AiRecommendation | Compliance | Composite
    public string PolicyId { get; set; } = "platform-default-route-governance";
    public int PolicyVersion { get; set; } = 1;
    public RiskPolicySource PolicySource { get; set; } = RiskPolicySource.PlatformDefault;
    public string MatchedRuleCodes { get; set; } = "[]"; // JSON array of matched rule codes
    public string ReasonCodes { get; set; } = default!; // JSON array or comma-separated codes
    public string ReasonDetails { get; set; } = default!;
    public GovernanceDecision GovernanceDecision { get; set; }
    public string PolicyApplied { get; set; } = default!;
    public Guid AssessedByUserId { get; set; }
    public DateTimeOffset AssessedAt { get; set; } = DateTimeOffset.UtcNow;

    public Route Route { get; set; } = default!;
}

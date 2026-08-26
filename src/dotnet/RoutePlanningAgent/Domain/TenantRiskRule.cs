using System;
using Shared.Entity;
using Shared.Enums;

namespace RoutePlanningAgent.Domain;

public class TenantRiskRule : TenantAuditableEntity
{
    public Guid PolicyId { get; set; }
    public string RuleCode { get; set; } = default!;
    public string RuleName { get; set; } = default!;
    public string ThresholdsJson { get; set; } = "{}";
    public RouteRiskLevel RiskEffect { get; set; } = RouteRiskLevel.High;
    public bool IsEnabled { get; set; } = true;
    public string? SourceReference { get; set; }
    public bool IsDeleted { get; set; }

    public TenantRiskPolicy Policy { get; set; } = default!;
}

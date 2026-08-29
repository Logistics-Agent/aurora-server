using Shared.Enums;

namespace Shared.Rules;

public record RuleResult
{
    public string RuleName { get; init; } = string.Empty;
    public string RuleCode { get; init; } = string.Empty;
    public RouteRiskLevel RiskLevel { get; init; }
    public bool Passed { get; init; }
    public string? Message { get; init; }
    public bool RequiresApproval { get; init; }
    public bool RequiresComplianceCheck { get; init; }
}

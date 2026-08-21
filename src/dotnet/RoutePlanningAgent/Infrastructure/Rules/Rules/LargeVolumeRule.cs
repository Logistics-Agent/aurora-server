using System;
using System.Threading;
using System.Threading.Tasks;
using Shared.Enums;
using Shared.Rules;

namespace RoutePlanningAgent.Infrastructure.Rules.Rules;

public class LargeVolumeRule : IRule<RouteRuleContext>
{
    public string Name => "LargeVolumeRule";

    private const decimal GlobalHighRiskM3 = 50m;
    private const decimal GlobalApprovalM3 = 40m;

    public bool CanApply(RouteRuleContext context) => context.Route.MaxVolumeM3 > 0;

    public async Task<RuleResult> EvaluateAsync(RouteRuleContext context, CancellationToken ct = default)
    {
        var thresholds = await context.RuleConfigService.GetThresholdsAsync(
            context.TenantId, Name, ct);

        if (!thresholds.IsEnabled)
            return new RuleResult { RuleName = Name, Passed = true, RiskLevel = RouteRiskLevel.Low };

        var highRiskM3 = thresholds.Get("maxVolumeM3", GlobalHighRiskM3);
        var approvalM3 = thresholds.Get("requiresApprovalThreshold", GlobalApprovalM3);

        if (context.Route.MaxVolumeM3 > highRiskM3)
        {
            return new RuleResult
            {
                RuleName = Name,
                Passed = false,
                RiskLevel = RouteRiskLevel.High,
                Message = $"MaxVolumeM3 {context.Route.MaxVolumeM3} vượt ngưỡng {highRiskM3}m³",
                RequiresApproval = context.Route.MaxVolumeM3 > approvalM3,
                RequiresComplianceCheck = true
            };
        }

        return new RuleResult { RuleName = Name, Passed = true, RiskLevel = RouteRiskLevel.Low };
    }
}

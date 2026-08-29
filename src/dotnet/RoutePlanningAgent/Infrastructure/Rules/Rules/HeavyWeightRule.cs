using System;
using System.Threading;
using System.Threading.Tasks;
using Shared.Enums;
using Shared.Rules;

namespace RoutePlanningAgent.Infrastructure.Rules.Rules;

public class HeavyWeightRule : IRule<RouteRuleContext>
{
    public string Name => "HeavyWeightRule";
    public const string Code = "ROUTE_WEIGHT_CAPACITY";

    private const decimal GlobalHighRiskKg = 5000m;
    private const decimal GlobalApprovalKg = 3000m;

    public bool CanApply(RouteRuleContext context) => context.Route.MaxWeightKg > 0;

    public async Task<RuleResult> EvaluateAsync(RouteRuleContext context, CancellationToken ct = default)
    {
        var thresholds = await context.RuleConfigService.GetThresholdsAsync(
            context.TenantId, Name, ct);

        if (!thresholds.IsEnabled)
            return new RuleResult { RuleName = Name, RuleCode = Code, Passed = true, RiskLevel = RouteRiskLevel.Low };

        var highRiskKg = thresholds.Get("maxWeightKg", GlobalHighRiskKg);
        var approvalKg = thresholds.Get("requiresApprovalThreshold", GlobalApprovalKg);

        if (context.Route.MaxWeightKg > highRiskKg)
        {
            return new RuleResult
            {
                RuleName = Name,
                RuleCode = Code,
                Passed = false,
                RiskLevel = RouteRiskLevel.High,
                Message = $"MaxWeightKg {context.Route.MaxWeightKg} vượt ngưỡng {highRiskKg}kg",
                RequiresApproval = context.Route.MaxWeightKg > approvalKg,
                RequiresComplianceCheck = true
            };
        }

        return new RuleResult { RuleName = Name, RuleCode = Code, Passed = true, RiskLevel = RouteRiskLevel.Low };
    }
}

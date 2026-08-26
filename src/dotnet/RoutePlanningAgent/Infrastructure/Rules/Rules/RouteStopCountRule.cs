using System;
using System.Threading;
using System.Threading.Tasks;
using Shared.Enums;
using Shared.Rules;

namespace RoutePlanningAgent.Infrastructure.Rules.Rules;

public class RouteStopCountRule : IRule<RouteRuleContext>
{
    public string Name => "RouteStopCountRule";
    public const string Code = "ROUTE_STOP_COUNT_BOUNDS";

    private const decimal GlobalHighRiskThreshold = 20m;
    private const decimal GlobalMediumRiskThreshold = 15m;

    public bool CanApply(RouteRuleContext context) => true;

    public async Task<RuleResult> EvaluateAsync(RouteRuleContext context, CancellationToken ct = default)
    {
        var thresholds = await context.RuleConfigService.GetThresholdsAsync(
            context.TenantId, Name, ct);

        if (!thresholds.IsEnabled)
            return new RuleResult { RuleName = Name, RuleCode = Code, Passed = true, RiskLevel = RouteRiskLevel.Low };

        var highThreshold = thresholds.Get("highRiskThreshold", GlobalHighRiskThreshold);
        var mediumThreshold = thresholds.Get("mediumRiskThreshold", GlobalMediumRiskThreshold);

        var stopCount = context.Route.Stops.Count;

        if (stopCount > highThreshold)
        {
            return new RuleResult
            {
                RuleName = Name,
                RuleCode = Code,
                Passed = false,
                RiskLevel = RouteRiskLevel.High,
                Message = $"Số lượng stops ({stopCount}) vượt ngưỡng High Risk ({highThreshold})"
            };
        }

        if (stopCount > mediumThreshold)
        {
            return new RuleResult
            {
                RuleName = Name,
                RuleCode = Code,
                Passed = false,
                RiskLevel = RouteRiskLevel.Medium,
                Message = $"Số lượng stops ({stopCount}) vượt ngưỡng Medium Risk ({mediumThreshold})"
            };
        }

        return new RuleResult { RuleName = Name, RuleCode = Code, Passed = true, RiskLevel = RouteRiskLevel.Low };
    }
}

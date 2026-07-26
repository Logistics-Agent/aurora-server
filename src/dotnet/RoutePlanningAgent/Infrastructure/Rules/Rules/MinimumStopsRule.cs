using System;
using System.Threading;
using System.Threading.Tasks;
using Shared.Enums;
using Shared.Rules;

namespace RoutePlanningAgent.Infrastructure.Rules.Rules;

public class MinimumStopsRule : IRule<RouteRuleContext>
{
    public string Name => "MinimumStopsRule";

    private const decimal GlobalMinStops = 2m;

    public bool CanApply(RouteRuleContext context) => true;

    public async Task<RuleResult> EvaluateAsync(RouteRuleContext context, CancellationToken ct = default)
    {
        var thresholds = await context.RuleConfigService.GetThresholdsAsync(
            context.TenantId, Name, ct);

        if (!thresholds.IsEnabled)
            return new RuleResult { RuleName = Name, Passed = true, RiskLevel = RouteRiskLevel.Low };

        var minStops = thresholds.Get("minStops", GlobalMinStops);

        var stopCount = context.Route.Stops.Count;

        if (stopCount < minStops)
        {
            return new RuleResult
            {
                RuleName = Name,
                Passed = false,
                RiskLevel = RouteRiskLevel.Low,
                Message = $"Số lượng stops ({stopCount}) ít hơn mức tối thiểu yêu cầu ({minStops})"
            };
        }

        return new RuleResult { RuleName = Name, Passed = true, RiskLevel = RouteRiskLevel.Low };
    }
}

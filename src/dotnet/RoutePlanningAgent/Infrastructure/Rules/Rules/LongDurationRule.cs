using System;
using System.Threading;
using System.Threading.Tasks;
using Shared.Enums;
using Shared.Rules;

namespace RoutePlanningAgent.Infrastructure.Rules.Rules;

public class LongDurationRule : IRule<RouteRuleContext>
{
    public string Name => "LongDurationRule";
    public const string Code = "ROUTE_DURATION_LIMIT";

    private const decimal GlobalMaxDurationMinutes = 480m;

    public bool CanApply(RouteRuleContext context) => context.Route.EstimatedDurationMinutes > 0;

    public async Task<RuleResult> EvaluateAsync(RouteRuleContext context, CancellationToken ct = default)
    {
        var thresholds = await context.RuleConfigService.GetThresholdsAsync(
            context.TenantId, Name, ct);

        if (!thresholds.IsEnabled)
            return new RuleResult { RuleName = Name, RuleCode = Code, Passed = true, RiskLevel = RouteRiskLevel.Low };

        var maxDuration = thresholds.Get("maxDurationMinutes", GlobalMaxDurationMinutes);

        if (context.Route.EstimatedDurationMinutes > maxDuration)
        {
            return new RuleResult
            {
                RuleName = Name,
                RuleCode = Code,
                Passed = false,
                RiskLevel = RouteRiskLevel.High,
                Message = $"EstimatedDurationMinutes {context.Route.EstimatedDurationMinutes} vượt ngưỡng {maxDuration} phút"
            };
        }

        return new RuleResult { RuleName = Name, RuleCode = Code, Passed = true, RiskLevel = RouteRiskLevel.Low };
    }
}

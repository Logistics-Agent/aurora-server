using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RoutePlanningAgent.Domain.Enums;
using Shared.Enums;
using Shared.Rules;

namespace RoutePlanningAgent.Infrastructure.Rules.Rules;

public class MultiHubRule : IRule<RouteRuleContext>
{
    public string Name => "MultiHubRule";
    public const string Code = "ROUTE_MULTI_HUB_COMPLEXITY";

    private const decimal GlobalMaxHubCount = 2m;

    public bool CanApply(RouteRuleContext context) => true;

    public async Task<RuleResult> EvaluateAsync(RouteRuleContext context, CancellationToken ct = default)
    {
        var thresholds = await context.RuleConfigService.GetThresholdsAsync(
            context.TenantId, Name, ct);

        if (!thresholds.IsEnabled)
            return new RuleResult { RuleName = Name, RuleCode = Code, Passed = true, RiskLevel = RouteRiskLevel.Low };

        var maxHubCount = thresholds.Get("maxHubCount", GlobalMaxHubCount);

        var hubCount = context.Route.Stops.Count(s =>
            s.StopType == StopType.Hub ||
            s.StopType == StopType.Port ||
            s.StopType == StopType.Customs);

        if (hubCount > maxHubCount)
        {
            return new RuleResult
            {
                RuleName = Name,
                RuleCode = Code,
                Passed = false,
                RiskLevel = RouteRiskLevel.High,
                Message = $"Số lượng Hub/Port/Customs stops ({hubCount}) vượt quá mức tối đa ({maxHubCount})",
                RequiresApproval = true,
                RequiresComplianceCheck = true
            };
        }

        return new RuleResult { RuleName = Name, RuleCode = Code, Passed = true, RiskLevel = RouteRiskLevel.Low };
    }
}

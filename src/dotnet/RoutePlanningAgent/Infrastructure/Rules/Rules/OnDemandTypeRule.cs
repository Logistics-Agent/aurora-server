using System;
using System.Threading;
using System.Threading.Tasks;
using RoutePlanningAgent.Domain.Enums;
using Shared.Enums;
using Shared.Rules;

namespace RoutePlanningAgent.Infrastructure.Rules.Rules;

public class OnDemandTypeRule : IRule<RouteRuleContext>
{
    public string Name => "OnDemandTypeRule";
    public const string Code = "ROUTE_ON_DEMAND_OPERATION";

    public bool CanApply(RouteRuleContext context) => true;

    public async Task<RuleResult> EvaluateAsync(RouteRuleContext context, CancellationToken ct = default)
    {
        var thresholds = await context.RuleConfigService.GetThresholdsAsync(
            context.TenantId, Name, ct);

        if (!thresholds.IsEnabled)
            return new RuleResult { RuleName = Name, RuleCode = Code, Passed = true, RiskLevel = RouteRiskLevel.Low };

        if (context.Route.Type == RouteType.OnDemand)
        {
            return new RuleResult
            {
                RuleName = Name,
                RuleCode = Code,
                Passed = false,
                RiskLevel = RouteRiskLevel.Medium,
                Message = "Tuyến vận chuyển thuộc loại OnDemand (Yêu cầu tức thì)"
            };
        }

        return new RuleResult { RuleName = Name, RuleCode = Code, Passed = true, RiskLevel = RouteRiskLevel.Low };
    }
}

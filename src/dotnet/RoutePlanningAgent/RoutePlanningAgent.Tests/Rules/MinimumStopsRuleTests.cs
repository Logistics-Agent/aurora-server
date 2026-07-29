using NSubstitute;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Infrastructure.Rules;
using RoutePlanningAgent.Infrastructure.Rules.Rules;
using RoutePlanningAgent.Tests.TestHelpers;
using Xunit;

namespace RoutePlanningAgent.Tests.Rules;

public class MinimumStopsRuleTests
{
    private static ITenantRuleConfigService ConfigWith(TenantRuleThresholds thresholds)
    {
        var svc = Substitute.For<ITenantRuleConfigService>();
        svc.GetThresholdsAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(thresholds);
        return svc;
    }

    [Fact]
    public async Task ItHonMinStops_Failed()
    {
        var rule = new MinimumStopsRule();
        var route = RouteBuilder.Build(stopCount: 1);
        var ctx = new RouteRuleContext(route, TestDb.TenantId, ConfigWith(new TenantRuleThresholds()));

        var result = await rule.EvaluateAsync(ctx);

        Assert.False(result.Passed);
    }

    [Fact]
    public async Task DuStops_Passed()
    {
        var rule = new MinimumStopsRule();
        var route = RouteBuilder.Build(stopCount: 3);
        var ctx = new RouteRuleContext(route, TestDb.TenantId, ConfigWith(new TenantRuleThresholds()));

        var result = await rule.EvaluateAsync(ctx);

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task TenantOverride_MinStopsCaoHon_BatLoi()
    {
        var rule = new MinimumStopsRule();
        var route = RouteBuilder.Build(stopCount: 3);
        var ctx = new RouteRuleContext(route, TestDb.TenantId,
            ConfigWith(new TenantRuleThresholds
            {
                Values = new Dictionary<string, decimal> { ["minStops"] = 5m }
            }));

        var result = await rule.EvaluateAsync(ctx);

        Assert.False(result.Passed);
    }
}

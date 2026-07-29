using NSubstitute;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Infrastructure.Rules;
using RoutePlanningAgent.Infrastructure.Rules.Rules;
using RoutePlanningAgent.Tests.TestHelpers;
using Shared.Enums;
using Xunit;

namespace RoutePlanningAgent.Tests.Rules;

public class HeavyWeightRuleTests
{
    private static ITenantRuleConfigService ConfigWith(TenantRuleThresholds thresholds)
    {
        var svc = Substitute.For<ITenantRuleConfigService>();
        svc.GetThresholdsAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(thresholds);
        return svc;
    }

    [Fact]
    public void CanApply_False_KhiKhongCoWeight()
    {
        var rule = new HeavyWeightRule();
        var route = RouteBuilder.Build(maxWeightKg: 0);
        Assert.False(rule.CanApply(new RouteRuleContext(route, TestDb.TenantId, ConfigWith(new TenantRuleThresholds()))));
    }

    [Fact]
    public async Task VuotNguongGlobal_High_RequiresComplianceVaApproval()
    {
        var rule = new HeavyWeightRule();
        var route = RouteBuilder.Build(maxWeightKg: 6000m); // > 5000 (high) và > 3000 (approval)
        var ctx = new RouteRuleContext(route, TestDb.TenantId, ConfigWith(new TenantRuleThresholds()));

        var result = await rule.EvaluateAsync(ctx);

        Assert.False(result.Passed);
        Assert.Equal(RouteRiskLevel.High, result.RiskLevel);
        Assert.True(result.RequiresApproval);
        Assert.True(result.RequiresComplianceCheck);
    }

    [Fact]
    public async Task DuoiNguong_Passed()
    {
        var rule = new HeavyWeightRule();
        var route = RouteBuilder.Build(maxWeightKg: 1000m);
        var ctx = new RouteRuleContext(route, TestDb.TenantId, ConfigWith(new TenantRuleThresholds()));

        var result = await rule.EvaluateAsync(ctx);

        Assert.True(result.Passed);
        Assert.Equal(RouteRiskLevel.Low, result.RiskLevel);
    }

    [Fact]
    public async Task RuleDisabled_LuonPassed()
    {
        var rule = new HeavyWeightRule();
        var route = RouteBuilder.Build(maxWeightKg: 99999m);
        var ctx = new RouteRuleContext(route, TestDb.TenantId,
            ConfigWith(new TenantRuleThresholds { IsEnabled = false }));

        var result = await rule.EvaluateAsync(ctx);

        Assert.True(result.Passed);
    }

    [Fact]
    public async Task TenantOverride_NguongThapHon_BatLoi()
    {
        var rule = new HeavyWeightRule();
        var route = RouteBuilder.Build(maxWeightKg: 1500m); // dưới global 5000 nhưng trên override 1000
        var ctx = new RouteRuleContext(route, TestDb.TenantId,
            ConfigWith(new TenantRuleThresholds
            {
                Values = new Dictionary<string, decimal> { ["maxWeightKg"] = 1000m }
            }));

        var result = await rule.EvaluateAsync(ctx);

        Assert.False(result.Passed);
        Assert.Equal(RouteRiskLevel.High, result.RiskLevel);
    }
}

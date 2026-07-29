using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RoutePlanningAgent.Application.Commands.Routes;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Infrastructure.Persistences;
using RoutePlanningAgent.Infrastructure.Services;
using RoutePlanningAgent.Tests.TestHelpers;
using Shared.Enums;
using Shared.Rules;
using Xunit;

namespace RoutePlanningAgent.Tests.Commands;

public class RequestRouteRecommendationHandlerTests
{
    private static RequestRouteRecommendationHandler CreateHandler(
        RoutePlanningDbContext context,
        IRouteRuleEngine ruleEngine,
        ITenantAiConfigService configService,
        IRouteAiService? aiService = null)
    {
        return new RequestRouteRecommendationHandler(
            context,
            ruleEngine,
            aiService ?? Substitute.For<IRouteAiService>(),
            Substitute.For<IComplianceRagService>(),
            configService,
            Substitute.For<ITenantRuleConfigService>(),
            new ApprovalService(context),
            new OutboxWriter(context),
            new FakeCurrentUser(TestDb.TenantId, TestDb.UserId),
            NullLogger<RequestRouteRecommendationHandler>.Instance);
    }

    [Fact]
    public async Task ManualPolicy_KhongPhanTich_GhiAuditLog()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var route = RouteBuilder.Build();
        context.Routes.Add(route);
        await context.SaveChangesAsync();

        var configService = Substitute.For<ITenantAiConfigService>();
        configService.GetConfigAsync(TestDb.TenantId, "RoutePlanning", Arg.Any<CancellationToken>())
            .Returns(new TenantAiConfig { TenantId = TestDb.TenantId, Feature = "RoutePlanning", Policy = AutomationPolicy.Manual });

        var ruleEngine = Substitute.For<IRouteRuleEngine>();

        var handler = CreateHandler(context, ruleEngine, configService);
        var dto = await handler.Handle(new RequestRouteRecommendationCommand(route.Id), CancellationToken.None);

        Assert.Equal("ManualRequired", dto.AutomationDecision);
        // Rule engine KHÔNG được gọi ở Manual mode
        await ruleEngine.DidNotReceiveWithAnyArgs().EvaluateAllAsync(default!, default);

        var audit = await context.DecisionAuditLogs.SingleAsync();
        Assert.Equal("ManualRequired", audit.AutomationDecision);
    }

    [Fact]
    public async Task RulesOnly_AuditRiskLevelLaGiaTriTinhDuoc_KhongPhaiLiteral()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var route = RouteBuilder.Build();
        context.Routes.Add(route);
        await context.SaveChangesAsync();

        // Không có config → default RulesOnly
        var configService = Substitute.For<ITenantAiConfigService>();
        configService.GetConfigAsync(TestDb.TenantId, "RoutePlanning", Arg.Any<CancellationToken>())
            .Returns((TenantAiConfig?)null);

        var ruleEngine = Substitute.For<IRouteRuleEngine>();
        ruleEngine.EvaluateAllAsync(Arg.Any<Infrastructure.Rules.RouteRuleContext>(), Arg.Any<CancellationToken>())
            .Returns(new List<RuleResult>
            {
                new() { RuleName = "HeavyWeightRule", Passed = false, RiskLevel = RouteRiskLevel.High, Message = "Quá tải" },
                new() { RuleName = "MinimumStopsRule", Passed = true, RiskLevel = RouteRiskLevel.Low }
            });

        var aiService = Substitute.For<IRouteAiService>();

        var handler = CreateHandler(context, ruleEngine, configService, aiService);
        var dto = await handler.Handle(new RequestRouteRecommendationCommand(route.Id), CancellationToken.None);

        Assert.Equal("ExecutedByRules", dto.AutomationDecision);
        Assert.Equal("High", dto.RiskLevel);

        // RulesOnly → LLM KHÔNG được gọi
        await aiService.DidNotReceiveWithAnyArgs()
            .GetRecommendationAsync(default!, default!, default, default!, default);

        // Audit RiskLevel = maxRisk tính được (bug cũ ghi literal "Evaluated")
        var audit = await context.DecisionAuditLogs.SingleAsync();
        Assert.Equal("High", audit.RiskLevel);

        // route.RiskLevel được persist
        var savedRoute = await context.Routes.SingleAsync();
        Assert.Equal(RouteRiskLevel.High, savedRoute.RiskLevel);
    }

    [Fact]
    public async Task RulesLlmApproval_TaoApproval_OutboxVaHistoryCungTransaction()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var route = RouteBuilder.Build();
        context.Routes.Add(route);
        await context.SaveChangesAsync();

        var configService = Substitute.For<ITenantAiConfigService>();
        configService.GetConfigAsync(TestDb.TenantId, "RoutePlanning", Arg.Any<CancellationToken>())
            .Returns(new TenantAiConfig
            {
                TenantId = TestDb.TenantId,
                Feature = "RoutePlanning",
                Policy = AutomationPolicy.RulesLlmApproval,
                AiProvider = "Gemini"
            });

        var ruleEngine = Substitute.For<IRouteRuleEngine>();
        ruleEngine.EvaluateAllAsync(Arg.Any<Infrastructure.Rules.RouteRuleContext>(), Arg.Any<CancellationToken>())
            .Returns(new List<RuleResult>
            {
                new() { RuleName = "MultiHubRule", Passed = false, RiskLevel = RouteRiskLevel.Medium, RequiresApproval = true, Message = "Nhiều hub" }
            });

        var aiService = Substitute.For<IRouteAiService>();
        aiService.GetRecommendationAsync(
                Arg.Any<RouteDto>(), Arg.Any<IReadOnlyList<RuleResult>>(),
                Arg.Any<ComplianceCheckResultDto?>(), "Gemini", Arg.Any<CancellationToken>())
            .Returns(new RouteAiResult
            {
                Recommendation = new RouteRecommendationDto
                {
                    RouteId = route.Id,
                    Summary = "Tóm tắt AI",
                    RecommendationSource = "AI"
                },
                Provider = "Gemini",
                Model = "gemini-2.5-flash",
                InputTokens = 150,
                OutputTokens = 60,
                Success = true
            });

        var handler = CreateHandler(context, ruleEngine, configService, aiService);
        var dto = await handler.Handle(new RequestRouteRecommendationCommand(route.Id), CancellationToken.None);

        Assert.Equal("PendingApproval", dto.AutomationDecision);
        Assert.NotNull(dto.ApprovalRequestId);

        // Tất cả trong CÙNG transaction: approval + history + audit + 2 outbox events
        Assert.Equal(1, await context.ApprovalRequests.CountAsync());
        Assert.Equal(1, await context.DecisionAuditLogs.CountAsync());

        var history = await context.OptimizationHistories.SingleAsync();
        Assert.Equal(150, history.InputTokens); // token THẬT từ LLM — không fabricate 120/80
        Assert.Equal(60, history.OutputTokens);

        var outboxTypes = await context.OutboxMessages.Select(m => m.EventType).ToListAsync();
        Assert.Contains("RouteApprovalRequestedEvent", outboxTypes);
        Assert.Contains("AiUsageEvent", outboxTypes);
    }
}

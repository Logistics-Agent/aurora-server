using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RoutePlanningAgent.Application.Commands.Routes;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.Persistences;
using RoutePlanningAgent.Infrastructure.Services;
using RoutePlanningAgent.Tests.TestHelpers;
using Shared.Enums;
using Shared.Exceptions;
using Shared.Rules;
using Xunit;

namespace RoutePlanningAgent.Tests.Commands;

public class RequestRouteRecommendationHandlerTests
{
    private static RequestRouteRecommendationHandler CreateHandler(
        RoutePlanningDbContext context,
        IRouteRuleEngine ruleEngine,
        IRouteAiService? aiService = null)
    {
        var ruleConfigService = Substitute.For<ITenantRuleConfigService>();
        var policyProvider = new RouteRiskPolicyProvider(context, ruleConfigService);

        return new RequestRouteRecommendationHandler(
            context,
            ruleEngine,
            aiService ?? Substitute.For<IRouteAiService>(),
            Substitute.For<IComplianceRagService>(),
            ruleConfigService,
            policyProvider,
            new ApprovalService(context),
            new RouteGovernanceService(context),
            new OutboxWriter(context),
            new FakeCurrentUser(TestDb.TenantId, TestDb.UserId),
            NullLogger<RequestRouteRecommendationHandler>.Instance);
    }

    private static void SeedDefaultRiskPolicy(RoutePlanningDbContext context)
    {
        context.TenantRiskPolicyConfigs.Add(new TenantRiskPolicyConfig
        {
            TenantId = TestDb.TenantId,
            PolicyMode = RiskPolicyMode.UsePlatformDefault,
            ActivePolicyId = RouteRiskPolicyProvider.PlatformDefaultPolicyId,
            ActivePolicyVersion = 1
        });
    }

    [Fact]
    public async Task UnconfiguredTenant_ThrowsRiskPolicyNotConfiguredException()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var route = RouteBuilder.Build();
        context.Routes.Add(route);
        await context.SaveChangesAsync();

        var ruleEngine = Substitute.For<IRouteRuleEngine>();
        var handler = CreateHandler(context, ruleEngine);

        // UNCONFIGURED TENANT -> Ném RiskPolicyNotConfiguredException và chặn toàn bộ
        await Assert.ThrowsAsync<RiskPolicyNotConfiguredException>(
            () => handler.Handle(new RequestRouteRecommendationCommand(route.Id), CancellationToken.None));
    }

    [Fact]
    public async Task AiGovernanceRecommendation_HighRisk_CreatesApprovalRequest()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        SeedDefaultRiskPolicy(context);
        var route = RouteBuilder.Build();
        context.Routes.Add(route);
        await context.SaveChangesAsync();

        var ruleEngine = Substitute.For<IRouteRuleEngine>();
        ruleEngine.EvaluateAllAsync(Arg.Any<Infrastructure.Rules.RouteRuleContext>(), Arg.Any<CancellationToken>())
            .Returns(new List<RuleResult>
            {
                new() { RuleName = "HeavyWeightRule", RuleCode = "ROUTE_WEIGHT_CAPACITY", Passed = false, RiskLevel = RouteRiskLevel.High, RequiresApproval = true, Message = "Quá tải 5 tấn" },
                new() { RuleName = "MinimumStopsRule", RuleCode = "ROUTE_MINIMUM_STOPS", Passed = true, RiskLevel = RouteRiskLevel.Low }
            });

        var aiService = Substitute.For<IRouteAiService>();
        aiService.GetRecommendationAsync(
                Arg.Any<RouteDto>(), Arg.Any<IReadOnlyList<RuleResult>>(),
                Arg.Any<ComplianceCheckResultDto?>(), Arg.Any<CancellationToken>())
            .Returns(new RouteAiResult
            {
                Recommendation = new RouteRecommendationDto
                {
                    RouteId = route.Id,
                    RiskLevel = "High",
                    Summary = "Tóm tắt từ AiGovernance",
                    RecommendationSource = "AI"
                },
                Provider = "Gemini",
                Model = "gemini-2.5-flash",
                InputTokens = 120,
                OutputTokens = 50,
                Success = true
            });

        var handler = CreateHandler(context, ruleEngine, aiService);
        var dto = await handler.Handle(new RequestRouteRecommendationCommand(route.Id), CancellationToken.None);

        Assert.Equal("PendingApproval", dto.AutomationDecision);
        Assert.Equal("High", dto.RiskLevel);
        Assert.NotNull(dto.ApprovalRequestId);

        // Audit RiskLevel = High
        var audit = await context.DecisionAuditLogs.SingleAsync();
        Assert.Equal("High", audit.RiskLevel);

        // Route RiskLevel và GovernanceDecision được cập nhật
        var savedRoute = await context.Routes.SingleAsync();
        Assert.Equal(RouteRiskLevel.High, savedRoute.RiskLevel);
        Assert.Equal(GovernanceDecision.ManagerApprovalRequired, savedRoute.GovernanceDecision);
    }

    [Fact]
    public async Task AiGovernanceRecommendation_LowRisk_StaffAllowed_TuDongChuyenSangReady()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        SeedDefaultRiskPolicy(context);
        var route = RouteBuilder.Build(status: RouteStatus.Draft);
        context.Routes.Add(route);
        await context.SaveChangesAsync();

        var ruleEngine = Substitute.For<IRouteRuleEngine>();
        ruleEngine.EvaluateAllAsync(Arg.Any<Infrastructure.Rules.RouteRuleContext>(), Arg.Any<CancellationToken>())
            .Returns(new List<RuleResult>
            {
                new() { RuleName = "MinimumStopsRule", RuleCode = "ROUTE_MINIMUM_STOPS", Passed = true, RiskLevel = RouteRiskLevel.Low }
            });

        var aiService = Substitute.For<IRouteAiService>();
        aiService.GetRecommendationAsync(
                Arg.Any<RouteDto>(), Arg.Any<IReadOnlyList<RuleResult>>(),
                Arg.Any<ComplianceCheckResultDto?>(), Arg.Any<CancellationToken>())
            .Returns(new RouteAiResult
            {
                Recommendation = new RouteRecommendationDto
                {
                    RouteId = route.Id,
                    RiskLevel = "Low",
                    Summary = "Tuyến đường tối ưu an toàn",
                    RecommendationSource = "AI"
                },
                Provider = "Gemini",
                Model = "gemini-2.5-flash",
                InputTokens = 100,
                OutputTokens = 40,
                Success = true
            });

        var handler = CreateHandler(context, ruleEngine, aiService);
        var dto = await handler.Handle(new RequestRouteRecommendationCommand(route.Id), CancellationToken.None);

        Assert.Equal("ExecutedByAi", dto.AutomationDecision);
        Assert.Equal("Low", dto.RiskLevel);
        Assert.Null(dto.ApprovalRequestId);

        // Route tự động chuyển sang Ready mà KHÔNG cần Manager duyệt
        var savedRoute = await context.Routes.SingleAsync();
        Assert.Equal(RouteStatus.Ready, savedRoute.Status);
        Assert.Equal(GovernanceDecision.NoApprovalRequired, savedRoute.GovernanceDecision);
    }

    [Fact]
    public async Task AiGovernanceRecommendation_GhiOutboxVaOptimizationHistoryCungTransaction()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        SeedDefaultRiskPolicy(context);
        var route = RouteBuilder.Build();
        context.Routes.Add(route);
        await context.SaveChangesAsync();

        var ruleEngine = Substitute.For<IRouteRuleEngine>();
        ruleEngine.EvaluateAllAsync(Arg.Any<Infrastructure.Rules.RouteRuleContext>(), Arg.Any<CancellationToken>())
            .Returns(new List<RuleResult>
            {
                new() { RuleName = "MultiHubRule", RuleCode = "ROUTE_MULTI_HUB_COMPLEXITY", Passed = false, RiskLevel = RouteRiskLevel.High, RequiresApproval = true, Message = "Nhiều hub phức tạp" }
            });

        var aiService = Substitute.For<IRouteAiService>();
        aiService.GetRecommendationAsync(
                Arg.Any<RouteDto>(), Arg.Any<IReadOnlyList<RuleResult>>(),
                Arg.Any<ComplianceCheckResultDto?>(), Arg.Any<CancellationToken>())
            .Returns(new RouteAiResult
            {
                Recommendation = new RouteRecommendationDto
                {
                    RouteId = route.Id,
                    Summary = "Phân tích AI từ AiGovernance",
                    RecommendationSource = "AI"
                },
                Provider = "Gemini",
                Model = "gemini-2.5-flash",
                InputTokens = 150,
                OutputTokens = 60,
                Success = true
            });

        var handler = CreateHandler(context, ruleEngine, aiService);
        var dto = await handler.Handle(new RequestRouteRecommendationCommand(route.Id), CancellationToken.None);

        Assert.Equal("PendingApproval", dto.AutomationDecision);
        Assert.NotNull(dto.ApprovalRequestId);

        // Tất cả trong CÙNG transaction: approval + history + audit + outbox events
        Assert.Equal(1, await context.ApprovalRequests.CountAsync());
        Assert.Equal(1, await context.DecisionAuditLogs.CountAsync());

        var history = await context.OptimizationHistories.SingleAsync();
        Assert.Equal(150, history.InputTokens);
        Assert.Equal(60, history.OutputTokens);

        var outboxTypes = await context.OutboxMessages.Select(m => m.EventType).ToListAsync();
        Assert.Contains("RouteApprovalRequestedEvent", outboxTypes);
        Assert.Contains("AiUsageEvent", outboxTypes);
        Assert.Contains("RouteRiskEvaluatedEvent", outboxTypes);
    }
}

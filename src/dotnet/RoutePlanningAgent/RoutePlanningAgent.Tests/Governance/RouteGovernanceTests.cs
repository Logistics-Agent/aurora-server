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
using Route = RoutePlanningAgent.Domain.Route;

namespace RoutePlanningAgent.Tests.Governance;

public class RouteGovernanceTests
{
    private static RequestRouteRecommendationHandler CreateRecommendationHandler(
        RoutePlanningDbContext context,
        IRouteRuleEngine ruleEngine,
        ITenantRuleConfigService? ruleConfigService = null,
        IRouteAiService? aiService = null)
    {
        var rulesConfig = ruleConfigService ?? Substitute.For<ITenantRuleConfigService>();
        var policyProvider = new RouteRiskPolicyProvider(context, rulesConfig);

        return new RequestRouteRecommendationHandler(
            context,
            ruleEngine,
            aiService ?? Substitute.For<IRouteAiService>(),
            Substitute.For<IComplianceRagService>(),
            rulesConfig,
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
    public async Task Scenario1_LowRisk_StaffExecutesWithoutManager()
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

        var handler = CreateRecommendationHandler(context, ruleEngine);
        var recDto = await handler.Handle(new RequestRouteRecommendationCommand(route.Id), CancellationToken.None);

        Assert.Equal("ExecutedByRules", recDto.AutomationDecision);
        Assert.Equal("Low", recDto.RiskLevel);
        Assert.Null(recDto.ApprovalRequestId);

        // Route tự động chuyển sang Ready, quyết định là NoApprovalRequired
        var savedRoute = await context.Routes.SingleAsync(r => r.Id == route.Id);
        Assert.Equal(RouteStatus.Ready, savedRoute.Status);
        Assert.Equal(GovernanceDecision.NoApprovalRequired, savedRoute.GovernanceDecision);

        // Staff kích hoạt (Active) trực tiếp không cần Manager
        var policyProvider = new RouteRiskPolicyProvider(
            context, Substitute.For<ITenantRuleConfigService>());
        var statusHandler = new UpdateRouteStatusHandler(
            context, new RouteGovernanceService(context), policyProvider, new FakeCurrentUser(TestDb.TenantId, TestDb.UserId), new OutboxWriter(context));
        var statusDto = await statusHandler.Handle(new UpdateRouteStatusCommand(route.Id, "Active"), CancellationToken.None);

        Assert.Equal("Active", statusDto.Status);
    }

    [Fact]
    public async Task Scenario2_MediumRisk_StaffAllowed_AuditGenerated()
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
                new() { RuleName = "OnDemandTypeRule", RuleCode = "ROUTE_ON_DEMAND_OPERATION", Passed = false, RiskLevel = RouteRiskLevel.Medium, Message = "Chuyến đi On-Demand" }
            });

        var handler = CreateRecommendationHandler(context, ruleEngine);
        var recDto = await handler.Handle(new RequestRouteRecommendationCommand(route.Id), CancellationToken.None);

        Assert.Equal("ExecutedByRules", recDto.AutomationDecision);
        Assert.Equal("Medium", recDto.RiskLevel);
        Assert.Null(recDto.ApprovalRequestId);

        // Bản ghi RiskAssessment và AuditLog đã được sinh ra
        var assessment = await context.RiskAssessments.SingleAsync(a => a.RouteId == route.Id);
        Assert.Equal(RouteRiskLevel.Medium, assessment.RiskLevel);
        Assert.Equal(GovernanceDecision.StaffAllowed, assessment.GovernanceDecision);

        // Staff vẫn được phép kích hoạt Active
        var policyProvider = new RouteRiskPolicyProvider(
            context, Substitute.For<ITenantRuleConfigService>());
        var statusHandler = new UpdateRouteStatusHandler(
            context, new RouteGovernanceService(context), policyProvider, new FakeCurrentUser(TestDb.TenantId, TestDb.UserId), new OutboxWriter(context));
        var statusDto = await statusHandler.Handle(new UpdateRouteStatusCommand(route.Id, "Active"), CancellationToken.None);

        Assert.Equal("Active", statusDto.Status);
    }

    [Fact]
    public async Task Scenario3_HighRisk_StaffCannotExecuteWithoutManagerApproval()
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
                new() { RuleName = "HeavyWeightRule", RuleCode = "ROUTE_WEIGHT_CAPACITY", Passed = false, RiskLevel = RouteRiskLevel.High, RequiresApproval = true, Message = "Tải trọng vượt 5 tấn" }
            });

        var handler = CreateRecommendationHandler(context, ruleEngine);
        var recDto = await handler.Handle(new RequestRouteRecommendationCommand(route.Id), CancellationToken.None);

        Assert.Equal("PendingApproval", recDto.AutomationDecision);
        Assert.Equal("High", recDto.RiskLevel);
        Assert.NotNull(recDto.ApprovalRequestId);

        // Route có quyết định ManagerApprovalRequired và vẫn ở Draft (chưa Ready)
        var savedRoute = await context.Routes.SingleAsync(r => r.Id == route.Id);
        Assert.Equal(GovernanceDecision.ManagerApprovalRequired, savedRoute.GovernanceDecision);
        Assert.Equal(RouteStatus.Draft, savedRoute.Status);

        // Nếu Staff cố tình chuyển sang Active -> BỊ CHẶN (không hợp lệ từ Draft sang Active)
        var policyProvider = new RouteRiskPolicyProvider(
            context, Substitute.For<ITenantRuleConfigService>());
        var statusHandler = new UpdateRouteStatusHandler(
            context, new RouteGovernanceService(context), policyProvider, new FakeCurrentUser(TestDb.TenantId, TestDb.UserId), new OutboxWriter(context));

        await Assert.ThrowsAsync<DomainException>(
            () => statusHandler.Handle(new UpdateRouteStatusCommand(route.Id, "Active"), CancellationToken.None));
    }

    [Fact]
    public async Task Scenario4_HighRisk_ManagerApproves_ExecutionAllowed()
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
                new() { RuleName = "HeavyWeightRule", RuleCode = "ROUTE_WEIGHT_CAPACITY", Passed = false, RiskLevel = RouteRiskLevel.High, RequiresApproval = true, Message = "Quá tải" }
            });

        var recHandler = CreateRecommendationHandler(context, ruleEngine);
        var recDto = await recHandler.Handle(new RequestRouteRecommendationCommand(route.Id), CancellationToken.None);

        var approvalId = recDto.ApprovalRequestId!.Value;

        // Manager phê duyệt
        var approvalService = new ApprovalService(context);
        var approveHandler = new ApproveRouteHandler(approvalService, new FakeCurrentUser(TestDb.TenantId, TestDb.UserId));
        await approveHandler.Handle(new ApproveRouteCommand(approvalId, "Quản lý chấp thuận chuyến hàng nặng"), CancellationToken.None);

        // Sau khi duyệt -> Route chuyển sang Ready và GovernanceDecision chuyển sang StaffAllowed
        var approvedRoute = await context.Routes.SingleAsync(r => r.Id == route.Id);
        Assert.Equal(RouteStatus.Ready, approvedRoute.Status);
        Assert.Equal(GovernanceDecision.StaffAllowed, approvedRoute.GovernanceDecision);

        // Nhân viên kích hoạt Active thành công
        var policyProvider = new RouteRiskPolicyProvider(
            context, Substitute.For<ITenantRuleConfigService>());
        var statusHandler = new UpdateRouteStatusHandler(
            context, new RouteGovernanceService(context), policyProvider, new FakeCurrentUser(TestDb.TenantId, TestDb.UserId), new OutboxWriter(context));
        var statusDto = await statusHandler.Handle(new UpdateRouteStatusCommand(route.Id, "Active"), CancellationToken.None);

        Assert.Equal("Active", statusDto.Status);
    }

    [Fact]
    public async Task Scenario5_HighRisk_ManagerRejects_ReworkStateDraft_ExecutionDenied()
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
                new() { RuleName = "HeavyWeightRule", RuleCode = "ROUTE_WEIGHT_CAPACITY", Passed = false, RiskLevel = RouteRiskLevel.High, RequiresApproval = true, Message = "Quá tải" }
            });

        var recHandler = CreateRecommendationHandler(context, ruleEngine);
        var recDto = await recHandler.Handle(new RequestRouteRecommendationCommand(route.Id), CancellationToken.None);

        var approvalId = recDto.ApprovalRequestId!.Value;

        // Manager từ chối (bắt buộc lý do)
        var approvalService = new ApprovalService(context);
        var rejectHandler = new RejectRouteHandler(approvalService, new FakeCurrentUser(TestDb.TenantId, TestDb.UserId));
        await rejectHandler.Handle(new RejectRouteCommand(approvalId, "Tải trọng nguy hiểm không được phép", "Yêu cầu chia nhỏ lô hàng"), CancellationToken.None);

        // Route chuyển sang Draft (Rework state, KHÔNG phải Cancelled)
        var rejectedRoute = await context.Routes.SingleAsync(r => r.Id == route.Id);
        Assert.Equal(RouteStatus.Draft, rejectedRoute.Status);

        // Cố tình chuyển sang Active -> BỊ CHẶN (Transition Draft -> Active không hợp lệ)
        var policyProvider = new RouteRiskPolicyProvider(
            context, Substitute.For<ITenantRuleConfigService>());
        var statusHandler = new UpdateRouteStatusHandler(
            context, new RouteGovernanceService(context), policyProvider, new FakeCurrentUser(TestDb.TenantId, TestDb.UserId), new OutboxWriter(context));

        await Assert.ThrowsAsync<DomainException>(
            () => statusHandler.Handle(new UpdateRouteStatusCommand(route.Id, "Active"), CancellationToken.None));
    }

    [Fact]
    public async Task Scenario6_StaffModifiesBusinessData_RiskRecalculatedToLow_ExecutionProceeds()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        SeedDefaultRiskPolicy(context);
        // Ban đầu route quá tải 6000kg -> High Risk
        var route = RouteBuilder.Build(status: RouteStatus.Draft);
        route.MaxWeightKg = 6000m;
        context.Routes.Add(route);
        await context.SaveChangesAsync();

        var ruleEngine = Substitute.For<IRouteRuleEngine>();
        ruleEngine.EvaluateAllAsync(Arg.Any<Infrastructure.Rules.RouteRuleContext>(), Arg.Any<CancellationToken>())
            .Returns(
                // Lần 1: High risk
                _ => Task.FromResult<IReadOnlyList<RuleResult>>(new List<RuleResult>
                {
                    new() { RuleName = "HeavyWeightRule", RuleCode = "ROUTE_WEIGHT_CAPACITY", Passed = false, RiskLevel = RouteRiskLevel.High, RequiresApproval = true, Message = "Quá tải 6000kg" }
                }),
                // Lần 2 (sau khi sửa data): Low risk
                _ => Task.FromResult<IReadOnlyList<RuleResult>>(new List<RuleResult>
                {
                    new() { RuleName = "HeavyWeightRule", RuleCode = "ROUTE_WEIGHT_CAPACITY", Passed = true, RiskLevel = RouteRiskLevel.Low, RequiresApproval = false }
                })
            );

        var recHandler = CreateRecommendationHandler(context, ruleEngine);
        var rec1 = await recHandler.Handle(new RequestRouteRecommendationCommand(route.Id), CancellationToken.None);
        Assert.Equal("High", rec1.RiskLevel);
        Assert.Equal("PendingApproval", rec1.AutomationDecision);

        // Nhân viên sửa dữ liệu nghiệp vụ (giảm trọng lượng xuống 3000kg)
        context.ChangeTracker.Clear();
        var updateHandler = new UpdateRouteHandler(
            context, new FakeCurrentUser(TestDb.TenantId, TestDb.UserId), new OutboxWriter(context));

        var stops = new List<RouteStopInputDto>
        {
            new() { Sequence = 1, StopType = "Pickup", LocationName = "Kho A", Address = "HN", Latitude = 21.0, Longitude = 105.8 },
            new() { Sequence = 2, StopType = "Delivery", LocationName = "Kho B", Address = "HP", Latitude = 20.8, Longitude = 106.6 }
        };

        await updateHandler.Handle(new UpdateRouteCommand(
            route.Id, "Route HN-HP", "Đã giảm tải", "Fixed", 3000m, 15m, 100m, 120, stops), CancellationToken.None);

        context.ChangeTracker.Clear();
        var updatedRoute = await context.Routes.SingleAsync(r => r.Id == route.Id);
        Assert.Equal(2, updatedRoute.Version); // Version tăng lên 2

        // Chạy lại đánh giá rủi ro hệ thống
        var rec2 = await recHandler.Handle(new RequestRouteRecommendationCommand(route.Id), CancellationToken.None);
        Assert.Equal("Low", rec2.RiskLevel);
        Assert.Equal("ExecutedByRules", rec2.AutomationDecision);
        Assert.Null(rec2.ApprovalRequestId);

        // Route tự động Ready và quyết định NoApprovalRequired
        var savedRoute = await context.Routes.SingleAsync(r => r.Id == route.Id);
        Assert.Equal(RouteStatus.Ready, savedRoute.Status);
        Assert.Equal(GovernanceDecision.NoApprovalRequired, savedRoute.GovernanceDecision);
    }

    [Fact]
    public async Task Scenario7_StaleApprovalOnOldVersion_CannotAuthorizeNewModifiedVersion()
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
                new() { RuleName = "HeavyWeightRule", RuleCode = "ROUTE_WEIGHT_CAPACITY", Passed = false, RiskLevel = RouteRiskLevel.High, RequiresApproval = true }
            });

        var recHandler = CreateRecommendationHandler(context, ruleEngine);
        var recDto = await recHandler.Handle(new RequestRouteRecommendationCommand(route.Id), CancellationToken.None);

        var approvalId = recDto.ApprovalRequestId!.Value;

        // Manager phê duyệt cho Version 1
        var approvalService = new ApprovalService(context);
        await approvalService.ApproveAsync(approvalId, TestDb.UserId, "Duyệt version 1");

        // Nhân viên sửa route -> Version tăng lên 2, LastAssessedAt bị reset
        context.ChangeTracker.Clear();
        var updateHandler = new UpdateRouteHandler(
            context, new FakeCurrentUser(TestDb.TenantId, TestDb.UserId), new OutboxWriter(context));
        var stops = new List<RouteStopInputDto>
        {
            new() { Sequence = 1, StopType = "Pickup", LocationName = "Kho A", Address = "HN", Latitude = 21.0, Longitude = 105.8 },
            new() { Sequence = 2, StopType = "Delivery", LocationName = "Kho B", Address = "HP", Latitude = 20.8, Longitude = 106.6 }
        };
        await updateHandler.Handle(new UpdateRouteCommand(
            route.Id, "Route Sửa", null, "Fixed", 5500m, 20m, 100m, 120, stops), CancellationToken.None);

        context.ChangeTracker.Clear();
        var modifiedRoute = await context.Routes.SingleAsync(r => r.Id == route.Id);
        Assert.Equal(2, modifiedRoute.Version);

        // Route vẫn có GovernanceDecision = ManagerApprovalRequired khi đánh giá lại
        modifiedRoute.GovernanceDecision = GovernanceDecision.ManagerApprovalRequired;
        await context.SaveChangesAsync();

        // Cố tình kích hoạt -> Phê duyệt cũ của version 1 KHÔNG thể dùng cho version 2
        var policyProvider = new RouteRiskPolicyProvider(
            context, Substitute.For<ITenantRuleConfigService>());
        var effectivePolicy = await policyProvider.GetEffectivePolicyAsync(TestDb.TenantId);
        var governanceService = new RouteGovernanceService(context);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => governanceService.ValidateExecutionAuthorizedAsync(modifiedRoute, effectivePolicy, CancellationToken.None));
    }

    [Fact]
    public async Task Scenario8_SoftDelete_DraftIsLowRisk_ActiveIsBlocked()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var governance = new RouteGovernanceService(context);
        var policy = new EffectiveRiskPolicy();

        // 1. Draft -> Low risk, StaffAllowed
        var draftRoute = RouteBuilder.Build(status: RouteStatus.Draft);
        var draftAssessment = governance.AssessSoftDeleteRisk(draftRoute, policy);
        Assert.Equal(RouteRiskLevel.Low, draftAssessment.RiskLevel);
        Assert.Equal(GovernanceDecision.StaffAllowed, draftAssessment.Decision);

        // 2. Ready -> Medium risk, StaffAllowed
        var readyRoute = RouteBuilder.Build(status: RouteStatus.Ready);
        var readyAssessment = governance.AssessSoftDeleteRisk(readyRoute, policy);
        Assert.Equal(RouteRiskLevel.Medium, readyAssessment.RiskLevel);
        Assert.Equal(GovernanceDecision.StaffAllowed, readyAssessment.Decision);

        // 3. Active -> Critical risk, Blocked
        var activeRoute = RouteBuilder.Build(status: RouteStatus.Active);
        var activeAssessment = governance.AssessSoftDeleteRisk(activeRoute, policy);
        Assert.Equal(RouteRiskLevel.Critical, activeAssessment.RiskLevel);
        Assert.Equal(GovernanceDecision.Blocked, activeAssessment.Decision);
    }

    [Fact]
    public async Task Scenario9_DeterministicRuleOverridesAiDowngrade()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var route = RouteBuilder.Build(status: RouteStatus.Draft);
        var governance = new RouteGovernanceService(context);
        var policy = new EffectiveRiskPolicy();

        // Rule vi phạm High risk
        var ruleResults = new List<RuleResult>
        {
            new() { RuleName = "HeavyWeightRule", RuleCode = "ROUTE_WEIGHT_CAPACITY", Passed = false, RiskLevel = RouteRiskLevel.High, RequiresApproval = true }
        };

        // AI đề xuất Low risk với confidence cao 0.95 (thậm chí nếu AiGovernance cho phép Full Autonomous)
        var aiResult = new RouteAiResult
        {
            Recommendation = new RouteRecommendationDto
            {
                RouteId = route.Id,
                RiskLevel = "Low",
                ConfidenceScore = 0.95,
                Summary = "AI cho rằng tuyến đường ổn"
            },
            Provider = "Gemini",
            Model = "gemini-2.5-flash",
            Success = true
        };

        var result = await governance.AssessRouteAsync(
            route, policy, ruleResults, null, aiResult);

        // QUY TẮC BẢO VỆ: AI autonomy không bao giờ được phép hạ mức rủi ro High xuống Low
        Assert.Equal(RouteRiskLevel.High, result.RiskLevel);
        Assert.Equal(GovernanceDecision.ManagerApprovalRequired, result.Decision);
    }

    [Fact]
    public async Task Scenario10_PolicyResolution_ExplicitPlatformDefaultVsTenantCustom()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var ruleConfigService = Substitute.For<ITenantRuleConfigService>();
        var provider = new RouteRiskPolicyProvider(context, ruleConfigService);

        // 1. Explicit USE_PLATFORM_DEFAULT -> Platform Default Policy v1
        context.TenantRiskPolicyConfigs.Add(new TenantRiskPolicyConfig
        {
            TenantId = TestDb.TenantId,
            PolicyMode = RiskPolicyMode.UsePlatformDefault,
            ActivePolicyId = RouteRiskPolicyProvider.PlatformDefaultPolicyId,
            ActivePolicyVersion = 1
        });
        await context.SaveChangesAsync();

        var defaultPolicy = await provider.GetEffectivePolicyAsync(TestDb.TenantId);
        Assert.Equal(RouteRiskPolicyProvider.PlatformDefaultPolicyId, defaultPolicy.PolicyId);
        Assert.Equal(1, defaultPolicy.Version);
        Assert.Equal(RiskPolicySource.PlatformDefault, defaultPolicy.Source);

        // 2. Explicit USE_CUSTOM_POLICY -> Tenant Policy
        context.ChangeTracker.Clear();
        var customTenantId = Guid.NewGuid();
        context.TenantRiskPolicyConfigs.Add(new TenantRiskPolicyConfig
        {
            TenantId = customTenantId,
            PolicyMode = RiskPolicyMode.UseCustomPolicy,
            ActivePolicyId = $"tenant-policy-{customTenantId}",
            ActivePolicyVersion = 3
        });
        await context.SaveChangesAsync();

        var tenantPolicy = await provider.GetEffectivePolicyAsync(customTenantId);
        Assert.Equal($"tenant-policy-{customTenantId}", tenantPolicy.PolicyId);
        Assert.Equal(3, tenantPolicy.Version);
        Assert.Equal(RiskPolicySource.Tenant, tenantPolicy.Source);
    }

    [Fact]
    public async Task Scenario11_UnconfiguredTenant_ThrowsRiskPolicyNotConfiguredException()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var ruleConfigService = Substitute.For<ITenantRuleConfigService>();
        var provider = new RouteRiskPolicyProvider(context, ruleConfigService);

        var unconfiguredTenantId = Guid.NewGuid();

        // UNCONFIGURED TENANT: Không có bản ghi TenantRiskPolicyConfig -> Ném RiskPolicyNotConfiguredException
        await Assert.ThrowsAsync<RiskPolicyNotConfiguredException>(
            () => provider.GetEffectivePolicyAsync(unconfiguredTenantId));
    }

    [Fact]
    public async Task Scenario12_CustomPolicyRetrievalFailure_NeverSilentlyFallsBackToDefault()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var ruleConfigService = Substitute.For<ITenantRuleConfigService>();
        var provider = new RouteRiskPolicyProvider(context, ruleConfigService);

        var customTenantId = Guid.NewGuid();
        context.TenantRiskPolicyConfigs.Add(new TenantRiskPolicyConfig
        {
            TenantId = customTenantId,
            PolicyMode = RiskPolicyMode.UseCustomPolicy,
            ActivePolicyId = $"tenant-policy-{customTenantId}",
            ActivePolicyVersion = 2
        });
        context.TenantRuleConfigs.Add(new TenantRuleConfig
        {
            TenantId = customTenantId,
            RuleName = "HeavyWeightRule"
        });
        await context.SaveChangesAsync();

        // Giả lập lỗi khi đọc thresholds tuỳ chỉnh của tenant
        ruleConfigService.GetThresholdsAsync(customTenantId, "HeavyWeightRule", Arg.Any<CancellationToken>())
            .Returns(Task.FromException<TenantRuleThresholds>(new InvalidOperationException("Redis cache cluster unreachable")));

        // FAIL-CLOSED: Ném PolicyUnavailableException, KHÔNG bao giờ fallback ngầm định về default
        await Assert.ThrowsAsync<PolicyUnavailableException>(
            () => provider.GetEffectivePolicyAsync(customTenantId));
    }

    [Fact]
    public async Task Scenario13_StalePolicyVersion_RejectsExecutionUntilReassessed()
    {
        var (context, connection) = TestDb.Create();
        await using var _ = connection;

        var route = RouteBuilder.Build(status: RouteStatus.Ready);
        context.Routes.Add(route);

        // Bản đánh giá rủi ro được tạo dưới Policy Version 1
        context.RiskAssessments.Add(new RiskAssessment
        {
            RouteId = route.Id,
            RouteVersion = route.Version,
            TenantId = TestDb.TenantId,
            RiskLevel = RouteRiskLevel.Low,
            GovernanceDecision = GovernanceDecision.NoApprovalRequired,
            PolicyId = RouteRiskPolicyProvider.PlatformDefaultPolicyId,
            PolicyVersion = 1,
            PolicySource = RiskPolicySource.PlatformDefault,
            Source = "Test",
            ReasonCodes = "[]",
            ReasonDetails = "Assessed with policy v1",
            PolicyApplied = "RulesOnly",
            AssessedByUserId = TestDb.UserId
        });
        await context.SaveChangesAsync();

        var governance = new RouteGovernanceService(context);

        // Chính sách hiện tại đã được nâng cấp lên Version 2
        var updatedPolicy = new EffectiveRiskPolicy
        {
            PolicyId = RouteRiskPolicyProvider.PlatformDefaultPolicyId,
            Version = 2,
            Source = RiskPolicySource.PlatformDefault
        };

        // Execution Boundary phát hiện STALE POLICY VERSION và từ chối kích hoạt
        var ex = await Assert.ThrowsAsync<ForbiddenException>(
            () => governance.ValidateExecutionAuthorizedAsync(route, updatedPolicy, CancellationToken.None));

        Assert.Contains("lỗi thời do chính sách rủi ro đã thay đổi", ex.Message);
    }
}

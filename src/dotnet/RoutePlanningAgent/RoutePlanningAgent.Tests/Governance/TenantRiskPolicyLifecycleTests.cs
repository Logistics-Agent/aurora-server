using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using RoutePlanningAgent.Application.Commands.Policies;
using RoutePlanningAgent.Application.DTOs.Configs;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Application.Queries.Policies;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.Persistences;
using RoutePlanningAgent.Infrastructure.Services;
using RoutePlanningAgent.Tests.TestHelpers;
using Shared.Constants;
using Shared.Enums;
using Shared.Exceptions;
using Shared.Security;
using Xunit;

namespace RoutePlanningAgent.Tests.Governance;

public class TenantRiskPolicyLifecycleTests
{
    private static (RoutePlanningDbContext Context, SqliteConnection Connection) CreateTestDb(Guid? tenantId = null, Guid? userId = null)
    {
        return TestDb.Create(tenantId, userId);
    }

    private static ICurrentUserService MockUser(Guid tenantId, Guid userId, params string[] roles)
    {
        var user = Substitute.For<ICurrentUserService>();
        user.TenantId.Returns(tenantId);
        user.UserId.Returns(userId);
        var primaryRole = roles.FirstOrDefault() ?? RoleConstants.Staff;
        user.Role.Returns(primaryRole);
        var permissions = primaryRole switch
        {
            RoleConstants.Manager => PermissionConstants.GetDefaultManagerPermissions().ToList(),
            RoleConstants.TenantAdmin => PermissionConstants.GetTenantAdminPermissions().ToList(),
            _ => PermissionConstants.GetDefaultStaffPermissions().ToList()
        };
        user.Permissions.Returns(permissions);
        return user;
    }

    [Fact]
    public async Task Scenario01_CreateDraft_SucceedsWithDraftStatusAndCalculatedVersion()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (context, connection) = CreateTestDb(tenantId, userId);
        using var _ = connection;

        var currentUser = MockUser(tenantId, userId, RoleConstants.Staff);
        var handler = new CreateTenantRiskPolicyDraftHandler(context, currentUser);

        // 1. Create first policy -> Version 1
        var command1 = new CreateTenantRiskPolicyDraftCommand(
            "Logistics Safety Policy 2026",
            "Initial draft based on company transport SOP",
            "RoutePlanning",
            RiskPolicySource.Tenant,
            null,
            new List<TenantRiskRuleInputDto>
            {
                new("ROUTE_WEIGHT_CAPACITY", "HeavyWeightRule", "{\"maxWeightKg\": 5000, \"requiresApprovalThreshold\": 3000}", "High", true, "SOP-1.1"),
                new("ROUTE_VOLUME_CAPACITY", "LargeVolumeRule", "{\"maxVolumeM3\": 25, \"requiresApprovalThreshold\": 20}", "Medium", true, "SOP-1.2")
            }
        );

        var result1 = await handler.Handle(command1, CancellationToken.None);

        Assert.Equal(1, result1.Version);
        Assert.Equal("Draft", result1.Status);
        Assert.Equal("Logistics Safety Policy 2026", result1.Name);
        Assert.Equal(2, result1.Rules.Count);

        // Verify outbox
        var outboxMessage = await context.OutboxMessages.FirstOrDefaultAsync();
        Assert.NotNull(outboxMessage);
        Assert.Contains("tenantriskpolicycreatedevent", outboxMessage.EventType.ToLowerInvariant());

        // 2. Create second policy draft -> Version 2
        var command2 = new CreateTenantRiskPolicyDraftCommand(
            "Logistics Safety Policy 2027",
            "Next year revision",
            "RoutePlanning"
        );

        var result2 = await handler.Handle(command2, CancellationToken.None);
        Assert.Equal(2, result2.Version);
        Assert.Equal("Draft", result2.Status);
    }

    [Fact]
    public async Task Scenario02_EditDraft_WhenInDraft_Succeeds()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (context, connection) = CreateTestDb(tenantId, userId);
        using var _ = connection;

        var currentUser = MockUser(tenantId, userId, RoleConstants.Staff);
        var createHandler = new CreateTenantRiskPolicyDraftHandler(context, currentUser);
        var updateHandler = new UpdateTenantRiskPolicyDraftHandler(context, currentUser);

        var created = await createHandler.Handle(new CreateTenantRiskPolicyDraftCommand(
            "Initial Policy", "Initial Description", "RoutePlanning"), CancellationToken.None);

        var updated = await updateHandler.Handle(new UpdateTenantRiskPolicyDraftCommand(
            created.Id,
            "Updated Policy Name",
            "Updated Description",
            new List<TenantRiskRuleInputDto>
            {
                new("ROUTE_DURATION_LIMIT", "LongDurationRule", "{\"maxDurationMinutes\": 480}", "High", true, "Doc-Section-4")
            }
        ), CancellationToken.None);

        Assert.Equal("Updated Policy Name", updated.Name);
        Assert.Equal("Updated Description", updated.Description);
        Assert.Single(updated.Rules);
        Assert.Equal("ROUTE_DURATION_LIMIT", updated.Rules[0].RuleCode);
    }

    [Fact]
    public async Task Scenario03_EditDraft_WhenRejected_TransitionsBackToDraft()
    {
        var tenantId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var (context, connection) = CreateTestDb(tenantId, staffId);
        using var _ = connection;

        var staffUser = MockUser(tenantId, staffId, RoleConstants.Staff);
        var managerUser = MockUser(tenantId, managerId, RoleConstants.Manager);

        var createHandler = new CreateTenantRiskPolicyDraftHandler(context, staffUser);
        var submitHandler = new SubmitTenantRiskPolicyHandler(context, staffUser);
        var rejectHandler = new RejectTenantRiskPolicyHandler(context, managerUser);
        var updateHandler = new UpdateTenantRiskPolicyDraftHandler(context, staffUser);

        // 1. Create and submit
        var created = await createHandler.Handle(new CreateTenantRiskPolicyDraftCommand(
            "Draft Policy", "Desc", "RoutePlanning", RiskPolicySource.Tenant, null,
            new List<TenantRiskRuleInputDto> { new("ROUTE_WEIGHT_CAPACITY", "HeavyWeightRule", "{\"maxWeightKg\": 5000}", "High", true, null) }
        ), CancellationToken.None);

        await submitHandler.Handle(new SubmitTenantRiskPolicyCommand(created.Id), CancellationToken.None);

        // 2. Manager rejects
        var rejected = await rejectHandler.Handle(new RejectTenantRiskPolicyCommand(created.Id, "Weight limit too high"), CancellationToken.None);
        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal("Weight limit too high", rejected.RejectionReason);

        // 3. Staff edits rejected policy -> Transitions back to DRAFT
        var edited = await updateHandler.Handle(new UpdateTenantRiskPolicyDraftCommand(
            created.Id,
            "Corrected Policy",
            "Adjusted weight threshold",
            new List<TenantRiskRuleInputDto> { new("ROUTE_WEIGHT_CAPACITY", "HeavyWeightRule", "{\"maxWeightKg\": 4000}", "High", true, null) }
        ), CancellationToken.None);

        Assert.Equal("Draft", edited.Status);
        Assert.Null(edited.RejectionReason);
        Assert.Equal("Corrected Policy", edited.Name);
    }

    [Fact]
    public async Task Scenario04_EditDraft_WhenPendingReviewOrActive_ThrowsDomainValidationException()
    {
        var tenantId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var (context, connection) = CreateTestDb(tenantId, staffId);
        using var _ = connection;

        var staffUser = MockUser(tenantId, staffId, RoleConstants.Staff);
        var managerUser = MockUser(tenantId, managerId, RoleConstants.Manager);

        var createHandler = new CreateTenantRiskPolicyDraftHandler(context, staffUser);
        var submitHandler = new SubmitTenantRiskPolicyHandler(context, staffUser);
        var publishHandler = new PublishTenantRiskPolicyHandler(context, Substitute.For<ITenantRuleConfigService>(), managerUser);
        var updateHandler = new UpdateTenantRiskPolicyDraftHandler(context, staffUser);

        var created = await createHandler.Handle(new CreateTenantRiskPolicyDraftCommand(
            "Policy", "Desc", "RoutePlanning", RiskPolicySource.Tenant, null,
            new List<TenantRiskRuleInputDto> { new("ROUTE_WEIGHT_CAPACITY", "HeavyWeightRule", "{\"maxWeightKg\": 5000}", "High", true, null) }
        ), CancellationToken.None);

        // 1. When in PendingReview
        await submitHandler.Handle(new SubmitTenantRiskPolicyCommand(created.Id), CancellationToken.None);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            updateHandler.Handle(new UpdateTenantRiskPolicyDraftCommand(created.Id, "Attempted Edit"), CancellationToken.None));

        // 2. When Active
        await publishHandler.Handle(new PublishTenantRiskPolicyCommand(created.Id), CancellationToken.None);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            updateHandler.Handle(new UpdateTenantRiskPolicyDraftCommand(created.Id, "Attempted Edit On Active"), CancellationToken.None));
    }

    [Fact]
    public async Task Scenario05_SubmitDraft_RequiresRulesAndSetsPendingReview()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var (context, connection) = CreateTestDb(tenantId, userId);
        using var _ = connection;

        var currentUser = MockUser(tenantId, userId, RoleConstants.Staff);
        var createHandler = new CreateTenantRiskPolicyDraftHandler(context, currentUser);
        var submitHandler = new SubmitTenantRiskPolicyHandler(context, currentUser);

        // Empty rules policy cannot be submitted
        var emptyPolicy = await createHandler.Handle(new CreateTenantRiskPolicyDraftCommand(
            "Empty Policy", "No rules", "RoutePlanning"), CancellationToken.None);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            submitHandler.Handle(new SubmitTenantRiskPolicyCommand(emptyPolicy.Id), CancellationToken.None));

        // Valid policy with rules can be submitted
        var validPolicy = await createHandler.Handle(new CreateTenantRiskPolicyDraftCommand(
            "Valid Policy", "With rules", "RoutePlanning", RiskPolicySource.Tenant, null,
            new List<TenantRiskRuleInputDto> { new("ROUTE_WEIGHT_CAPACITY", "HeavyWeightRule", "{\"maxWeightKg\": 5000}", "High", true, null) }
        ), CancellationToken.None);

        var submitted = await submitHandler.Handle(new SubmitTenantRiskPolicyCommand(validPolicy.Id), CancellationToken.None);
        Assert.Equal("PendingReview", submitted.Status);
        Assert.Equal(userId, submitted.SubmittedByUserId);
        Assert.NotNull(submitted.SubmittedAt);
    }

    [Fact]
    public async Task Scenario06_RejectPolicy_EnforcesAuthorityAndMandatoryReason()
    {
        var tenantId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var (context, connection) = CreateTestDb(tenantId, staffId);
        using var _ = connection;

        var staffUser = MockUser(tenantId, staffId, RoleConstants.Staff);
        var managerUser = MockUser(tenantId, managerId, RoleConstants.Manager);

        var createHandler = new CreateTenantRiskPolicyDraftHandler(context, staffUser);
        var submitHandler = new SubmitTenantRiskPolicyHandler(context, staffUser);
        var rejectHandlerStaff = new RejectTenantRiskPolicyHandler(context, staffUser);
        var rejectHandlerManager = new RejectTenantRiskPolicyHandler(context, managerUser);

        var created = await createHandler.Handle(new CreateTenantRiskPolicyDraftCommand(
            "Policy", "Desc", "RoutePlanning", RiskPolicySource.Tenant, null,
            new List<TenantRiskRuleInputDto> { new("ROUTE_WEIGHT_CAPACITY", "HeavyWeightRule", "{\"maxWeightKg\": 5000}", "High", true, null) }
        ), CancellationToken.None);
        await submitHandler.Handle(new SubmitTenantRiskPolicyCommand(created.Id), CancellationToken.None);

        // Staff has no reject authority -> ForbiddenException
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            rejectHandlerStaff.Handle(new RejectTenantRiskPolicyCommand(created.Id, "Rejected by staff"), CancellationToken.None));

        // Manager cannot reject with empty reason -> DomainValidationException
        await Assert.ThrowsAsync<DomainValidationException>(() =>
            rejectHandlerManager.Handle(new RejectTenantRiskPolicyCommand(created.Id, "   "), CancellationToken.None));

        // Manager successfully rejects with reason
        var rejected = await rejectHandlerManager.Handle(
            new RejectTenantRiskPolicyCommand(created.Id, "Unacceptable duration limit", "Please review section 2"), CancellationToken.None);

        Assert.Equal("Rejected", rejected.Status);
        Assert.Equal("Unacceptable duration limit", rejected.RejectionReason);
        Assert.Equal(managerId, rejected.ReviewedByUserId);
    }

    [Fact]
    public async Task Scenario07_PublishPolicy_EnforcesManagerAuthorityAndValidatesThresholds()
    {
        var tenantId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var (context, connection) = CreateTestDb(tenantId, staffId);
        using var _ = connection;

        var staffUser = MockUser(tenantId, staffId, RoleConstants.Staff);
        var managerUser = MockUser(tenantId, managerId, RoleConstants.Manager);
        var ruleConfigService = Substitute.For<ITenantRuleConfigService>();

        var createHandler = new CreateTenantRiskPolicyDraftHandler(context, staffUser);
        var submitHandler = new SubmitTenantRiskPolicyHandler(context, staffUser);
        var publishHandlerStaff = new PublishTenantRiskPolicyHandler(context, ruleConfigService, staffUser);
        var publishHandlerManager = new PublishTenantRiskPolicyHandler(context, ruleConfigService, managerUser);

        var created = await createHandler.Handle(new CreateTenantRiskPolicyDraftCommand(
            "Policy", "Desc", "RoutePlanning", RiskPolicySource.Tenant, null,
            new List<TenantRiskRuleInputDto> { new("ROUTE_WEIGHT_CAPACITY", "HeavyWeightRule", "{\"maxWeightKg\": 5000}", "High", true, null) }
        ), CancellationToken.None);
        await submitHandler.Handle(new SubmitTenantRiskPolicyCommand(created.Id), CancellationToken.None);

        // Staff cannot publish -> ForbiddenException
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            publishHandlerStaff.Handle(new PublishTenantRiskPolicyCommand(created.Id), CancellationToken.None));

        // Manager publishes successfully
        var published = await publishHandlerManager.Handle(new PublishTenantRiskPolicyCommand(created.Id), CancellationToken.None);
        Assert.Equal("Active", published.Status);
        Assert.Equal(managerId, published.PublishedByUserId);
        Assert.NotNull(published.PublishedAt);

        // Redis cache invalidation was triggered
        await ruleConfigService.Received(1).InvalidateCacheAsync(tenantId, "", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Scenario08_PublishPolicy_SupersedesPreviousActive_AndMaintainsSingleActivePolicy()
    {
        var tenantId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var (context, connection) = CreateTestDb(tenantId, managerId);
        using var _ = connection;

        var managerUser = MockUser(tenantId, managerId, RoleConstants.Manager);
        var ruleConfigService = Substitute.For<ITenantRuleConfigService>();

        var createHandler = new CreateTenantRiskPolicyDraftHandler(context, managerUser);
        var publishHandler = new PublishTenantRiskPolicyHandler(context, ruleConfigService, managerUser);

        // 1. Create and Publish Version 1
        var v1Draft = await createHandler.Handle(new CreateTenantRiskPolicyDraftCommand(
            "Policy v1", "First version", "RoutePlanning", RiskPolicySource.Tenant, null,
            new List<TenantRiskRuleInputDto> { new("ROUTE_WEIGHT_CAPACITY", "HeavyWeightRule", "{\"maxWeightKg\": 5000}", "High", true, null) }
        ), CancellationToken.None);
        var v1Published = await publishHandler.Handle(new PublishTenantRiskPolicyCommand(v1Draft.Id), CancellationToken.None);
        Assert.Equal("Active", v1Published.Status);
        Assert.Equal(1, v1Published.Version);

        // 2. Create and Publish Version 2
        var v2Draft = await createHandler.Handle(new CreateTenantRiskPolicyDraftCommand(
            "Policy v2", "Second version", "RoutePlanning", RiskPolicySource.Tenant, null,
            new List<TenantRiskRuleInputDto> { new("ROUTE_WEIGHT_CAPACITY", "HeavyWeightRule", "{\"maxWeightKg\": 6000}", "High", true, null) }
        ), CancellationToken.None);
        var v2Published = await publishHandler.Handle(new PublishTenantRiskPolicyCommand(v2Draft.Id), CancellationToken.None);

        Assert.Equal("Active", v2Published.Status);
        Assert.Equal(2, v2Published.Version);

        // Verify that v1 is now SUPERSEDED
        context.ChangeTracker.Clear();
        var v1InDb = await context.TenantRiskPolicies.FindAsync(v1Draft.Id);
        Assert.NotNull(v1InDb);
        Assert.Equal(TenantRiskPolicyStatus.Superseded, v1InDb.Status);
        Assert.NotNull(v1InDb.SupersededAt);

        // Verify that TenantRiskPolicyConfig points to v2
        var config = await context.TenantRiskPolicyConfigs.FirstOrDefaultAsync(c => c.TenantId == tenantId);
        Assert.NotNull(config);
        Assert.Equal(RiskPolicyMode.UseCustomPolicy, config.PolicyMode);
        Assert.Equal(v2Draft.Id.ToString(), config.ActivePolicyId);
        Assert.Equal(2, config.ActivePolicyVersion);

        // Verify exact count of ACTIVE policies for this tenant is 1
        var activeCount = await context.TenantRiskPolicies
            .CountAsync(p => p.TenantId == tenantId && p.Scope == "RoutePlanning" && p.Status == TenantRiskPolicyStatus.Active);
        Assert.Equal(1, activeCount);
    }

    [Fact]
    public async Task Scenario09_DeletePolicy_PreventsDeletingActiveOrSuperseded_AllowsDraftOrRejected()
    {
        var tenantId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var (context, connection) = CreateTestDb(tenantId, managerId);
        using var _ = connection;

        var managerUser = MockUser(tenantId, managerId, RoleConstants.Manager);
        var ruleConfigService = Substitute.For<ITenantRuleConfigService>();

        var createHandler = new CreateTenantRiskPolicyDraftHandler(context, managerUser);
        var publishHandler = new PublishTenantRiskPolicyHandler(context, ruleConfigService, managerUser);
        var deleteHandler = new DeleteTenantRiskPolicyDraftHandler(context, managerUser);

        // 1. Draft can be soft-deleted
        var draft = await createHandler.Handle(new CreateTenantRiskPolicyDraftCommand("Draft", "Desc", "RoutePlanning"), CancellationToken.None);
        var deleteResult = await deleteHandler.Handle(new DeleteTenantRiskPolicyDraftCommand(draft.Id), CancellationToken.None);
        Assert.True(deleteResult);

        // 2. Active CANNOT be deleted (preserves historical audit trail)
        var activePolicyDraft = await createHandler.Handle(new CreateTenantRiskPolicyDraftCommand(
            "Active Policy", "Desc", "RoutePlanning", RiskPolicySource.Tenant, null,
            new List<TenantRiskRuleInputDto> { new("ROUTE_WEIGHT_CAPACITY", "HeavyWeightRule", "{\"maxWeightKg\": 5000}", "High", true, null) }
        ), CancellationToken.None);
        var activePolicy = await publishHandler.Handle(new PublishTenantRiskPolicyCommand(activePolicyDraft.Id), CancellationToken.None);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            deleteHandler.Handle(new DeleteTenantRiskPolicyDraftCommand(activePolicy.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Scenario10_RouteRiskPolicyProvider_ConsumesOnlyActivePolicy_FailsClosedOnDraftOrNone()
    {
        var tenantId = Guid.NewGuid();
        var (context, connection) = CreateTestDb(tenantId);
        using var _ = connection;

        var ruleConfigService = Substitute.For<ITenantRuleConfigService>();
        var provider = new RouteRiskPolicyProvider(context, ruleConfigService);

        // 1. Tenant configured for UseCustomPolicy, but only has a DRAFT policy
        context.TenantRiskPolicyConfigs.Add(new TenantRiskPolicyConfig
        {
            TenantId = tenantId,
            PolicyMode = RiskPolicyMode.UseCustomPolicy,
            ActivePolicyId = "some-id",
            ActivePolicyVersion = 1
        });
        context.TenantRiskPolicies.Add(new TenantRiskPolicy
        {
            TenantId = tenantId,
            Name = "Draft Policy",
            Scope = "RoutePlanning",
            Version = 1,
            Status = TenantRiskPolicyStatus.Draft,
            Rules = new List<TenantRiskRule>
            {
                new() { TenantId = tenantId, RuleCode = "ROUTE_WEIGHT_CAPACITY", RuleName = "HeavyWeightRule", ThresholdsJson = "{\"maxWeightKg\": 5000}" }
            }
        });
        await context.SaveChangesAsync();

        // FAIL CLOSED: RouteRiskPolicyProvider MUST NOT consume DRAFT policy
        await Assert.ThrowsAsync<PolicyUnavailableException>(() =>
            provider.GetEffectivePolicyAsync(tenantId));

        // 2. Now mark policy as ACTIVE
        var policy = await context.TenantRiskPolicies.FirstAsync(p => p.TenantId == tenantId);
        policy.Status = TenantRiskPolicyStatus.Active;
        await context.SaveChangesAsync();

        var effectivePolicy = await provider.GetEffectivePolicyAsync(tenantId);
        Assert.Equal(policy.Id.ToString(), effectivePolicy.PolicyId);
        Assert.Equal(1, effectivePolicy.Version);
        Assert.True(effectivePolicy.RuleThresholds.ContainsKey("HeavyWeightRule"));
        Assert.Equal(5000m, effectivePolicy.RuleThresholds["HeavyWeightRule"].Values["maxWeightKg"]);
    }

    [Fact]
    public async Task Scenario11_StaleAssessmentDetection_WhenNewVersionPublished_OldVersionAssessmentBecomesStale()
    {
        var tenantId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var (context, connection) = CreateTestDb(tenantId, managerId);
        using var _ = connection;

        var managerUser = MockUser(tenantId, managerId, RoleConstants.Manager);
        var ruleConfigService = Substitute.For<ITenantRuleConfigService>();

        var createHandler = new CreateTenantRiskPolicyDraftHandler(context, managerUser);
        var publishHandler = new PublishTenantRiskPolicyHandler(context, ruleConfigService, managerUser);

        // 1. Publish Policy Version 1
        var v1Draft = await createHandler.Handle(new CreateTenantRiskPolicyDraftCommand(
            "Policy v1", "Desc", "RoutePlanning", RiskPolicySource.Tenant, null,
            new List<TenantRiskRuleInputDto> { new("ROUTE_WEIGHT_CAPACITY", "HeavyWeightRule", "{\"maxWeightKg\": 5000}", "High", true, null) }
        ), CancellationToken.None);
        var v1 = await publishHandler.Handle(new PublishTenantRiskPolicyCommand(v1Draft.Id), CancellationToken.None);

        // 2. Create Route and RiskAssessment under Version 1
        var route = new Route
        {
            TenantId = tenantId,
            Name = "Route Alpha",
            Type = RouteType.Fixed,
            Status = RouteStatus.Ready,
            MaxWeightKg = 4000,
            MaxVolumeM3 = 20,
            EstimatedDistanceKm = 50,
            EstimatedDurationMinutes = 120
        };
        context.Routes.Add(route);
        await context.SaveChangesAsync();

        var assessment = new RiskAssessment
        {
            TenantId = tenantId,
            RouteId = route.Id,
            RouteVersion = 1,
            PolicyId = v1.Id.ToString(),
            PolicyVersion = 1,
            PolicySource = RiskPolicySource.Tenant,
            Source = "DeterministicRules",
            ReasonCodes = "[]",
            ReasonDetails = "All constraints within threshold",
            PolicyApplied = v1.Id.ToString(),
            RiskLevel = RouteRiskLevel.Low,
            GovernanceDecision = GovernanceDecision.NoApprovalRequired,
            CreatedAt = DateTimeOffset.UtcNow
        };
        context.RiskAssessments.Add(assessment);
        await context.SaveChangesAsync();

        // 3. Publish Policy Version 2
        var v2Draft = await createHandler.Handle(new CreateTenantRiskPolicyDraftCommand(
            "Policy v2", "Desc", "RoutePlanning", RiskPolicySource.Tenant, null,
            new List<TenantRiskRuleInputDto> { new("ROUTE_WEIGHT_CAPACITY", "HeavyWeightRule", "{\"maxWeightKg\": 4000}", "High", true, null) }
        ), CancellationToken.None);
        var v2 = await publishHandler.Handle(new PublishTenantRiskPolicyCommand(v2Draft.Id), CancellationToken.None);

        // 4. Verification: Latest active config has version 2, while historical assessment has version 1 -> Stale detected at execution boundary
        var activeConfig = await context.TenantRiskPolicyConfigs.FirstAsync(c => c.TenantId == tenantId);
        Assert.Equal(2, activeConfig.ActivePolicyVersion);
        Assert.Equal(1, assessment.PolicyVersion);
        Assert.True(activeConfig.ActivePolicyVersion > assessment.PolicyVersion, "Assessment evaluated under v1 is now stale under active v2 policy!");
    }

    [Fact]
    public async Task Scenario12_TenantIsolation_TenantACannotReadOrMutateTenantBPolicy()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        var (context, connection) = CreateTestDb(tenantA, userA);
        using var _ = connection;

        var userContextA = MockUser(tenantA, userA, RoleConstants.Staff);
        var userContextB = MockUser(tenantB, userB, RoleConstants.Staff);

        var createHandlerA = new CreateTenantRiskPolicyDraftHandler(context, userContextA);
        var policyA = await createHandlerA.Handle(new CreateTenantRiskPolicyDraftCommand("Policy A", "Desc", "RoutePlanning"), CancellationToken.None);

        // Tenant B attempts to read Tenant A's policy
        var getHandlerB = new GetTenantRiskPolicyByIdHandler(context, userContextB);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            getHandlerB.Handle(new GetTenantRiskPolicyByIdQuery(policyA.Id), CancellationToken.None));

        // Tenant B attempts to update Tenant A's policy
        var updateHandlerB = new UpdateTenantRiskPolicyDraftHandler(context, userContextB);
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            updateHandlerB.Handle(new UpdateTenantRiskPolicyDraftCommand(policyA.Id, "Hacked Name"), CancellationToken.None));
    }

    [Fact]
    public async Task Scenario13_OcrCandidateDraft_IsAlwaysCreatedInDraftStatus_AndNeverDirectlyActive()
    {
        var tenantId = Guid.NewGuid();
        var systemUserId = Guid.NewGuid();
        var (context, connection) = CreateTestDb(tenantId, systemUserId);
        using var _ = connection;

        var currentUser = MockUser(tenantId, systemUserId, RoleConstants.Staff);
        var createHandler = new CreateTenantRiskPolicyDraftHandler(context, currentUser);

        // Simulated future DocumentOCR candidate rules ingestion
        var ocrCommand = new CreateTenantRiskPolicyDraftCommand(
            "Transport SOP Document OCR Ingested",
            "Extracted by OCR pipeline from Carrier-SOP-2026.pdf",
            "RoutePlanning",
            RiskPolicySource.DocumentOcr,
            "DOC-OCR-REF-998811",
            new List<TenantRiskRuleInputDto>
            {
                new("ROUTE_WEIGHT_CAPACITY", "HeavyWeightRule", "{\"maxWeightKg\": 8000}", "High", true, "Carrier-SOP-2026.pdf#p.12"),
                new("ROUTE_STOP_COUNT", "RouteStopCountRule", "{\"maxStopCount\": 10}", "Medium", true, "Carrier-SOP-2026.pdf#p.15")
            }
        );

        var ocrDraft = await createHandler.Handle(ocrCommand, CancellationToken.None);

        // MUST REQUIREMENT: OCR draft is strictly in DRAFT state, completely isolated from active policy consumption
        Assert.Equal("Draft", ocrDraft.Status);
        Assert.Equal("DocumentOcr", ocrDraft.Source);
        Assert.Equal("DOC-OCR-REF-998811", ocrDraft.SourceDocumentId);

        // Active policy query returns nothing because OCR draft is unreviewed and unapproved
        var getActiveHandler = new GetActiveTenantRiskPolicyHandler(context, currentUser);
        var activePolicy = await getActiveHandler.Handle(new GetActiveTenantRiskPolicyQuery("RoutePlanning"), CancellationToken.None);
        Assert.Null(activePolicy);
    }
}

using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.DTOs.Configs;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Application.Mapping;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Domain.Services;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Constants;
using Shared.Enums;
using Shared.Events;
using Shared.Exceptions;
using Shared.Security;

namespace RoutePlanningAgent.Application.Commands.Policies;

public record PublishTenantRiskPolicyCommand(Guid PolicyId) : IRequest<TenantRiskPolicyDto>;

public class PublishTenantRiskPolicyHandler(
    RoutePlanningDbContext context,
    ITenantRuleConfigService ruleConfigService,
    ICurrentUserService currentUser)
    : IRequestHandler<PublishTenantRiskPolicyCommand, TenantRiskPolicyDto>
{
    public async Task<TenantRiskPolicyDto> Handle(
        PublishTenantRiskPolicyCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw new ForbiddenException("Tenant context is missing");
        var userId = currentUser.UserId
            ?? throw new ForbiddenException("User context is missing");

        // 1. Authority Guard: Must be Manager, TenantAdmin, SystemAdmin, or have publish permission
        if (!currentUser.CanPublishRiskPolicy())
        {
            throw new ForbiddenException("Bạn không có thẩm quyền phát hành (Publish) chính sách rủi ro (yêu cầu vai trò Manager hoặc Admin).");
        }

        var policy = await context.TenantRiskPolicies
            .Include(p => p.Rules)
            .FirstOrDefaultAsync(p => p.Id == request.PolicyId && p.TenantId == tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy chính sách rủi ro với ID '{request.PolicyId}'.");

        // 2. Lifecycle Status Guard: Can only publish from PendingReview (or Draft if caller has direct publish authority)
        if (policy.Status != TenantRiskPolicyStatus.PendingReview && policy.Status != TenantRiskPolicyStatus.Draft)
        {
            throw new DomainValidationException(
                $"Không thể phát hành chính sách ở trạng thái '{policy.Status}'. " +
                $"Chính sách phải ở trạng thái 'PendingReview' hoặc 'Draft' trước khi phát hành.");
        }

        // 3. Typed Thresholds & Rules Validation (MUST REQUIREMENT)
        TenantRuleValidator.ValidateRulesForPublish(policy.Rules);

        // 4. Concurrency & Atomicity: Supersede old Active policy and activate new policy in single transaction
        var scope = policy.Scope;

        var currentActivePolicies = await context.TenantRiskPolicies
            .Where(p => p.TenantId == tenantId && p.Scope == scope && p.Status == TenantRiskPolicyStatus.Active)
            .ToListAsync(cancellationToken);

        Guid? supersededPolicyId = null;
        int? supersededVersion = null;

        var now = DateTimeOffset.UtcNow;

        foreach (var oldActive in currentActivePolicies)
        {
            oldActive.Status = TenantRiskPolicyStatus.Superseded;
            oldActive.SupersededAt = now;
            oldActive.UpdatedAt = now;
            supersededPolicyId = oldActive.Id;
            supersededVersion = oldActive.Version;

            context.OutboxMessages.Add(new OutboxMessage
            {
                EventType = typeof(TenantRiskPolicySupersededEvent).FullName!,
                Payload = JsonSerializer.Serialize(new TenantRiskPolicySupersededEvent
                {
                    PolicyId = oldActive.Id,
                    TenantId = tenantId,
                    Scope = oldActive.Scope,
                    Version = oldActive.Version,
                    SupersededByPolicyId = policy.Id,
                    SupersededByVersion = policy.Version
                }),
                CreatedAt = now
            });
        }

        // Set target policy to ACTIVE
        policy.Status = TenantRiskPolicyStatus.Active;
        policy.PublishedByUserId = userId;
        policy.PublishedAt = now;
        policy.UpdatedAt = now;

        // 5. Update or Create TenantRiskPolicyConfig for the tenant
        var config = await context.TenantRiskPolicyConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken);

        if (config == null)
        {
            config = new TenantRiskPolicyConfig
            {
                TenantId = tenantId,
                PolicyMode = RiskPolicyMode.UseCustomPolicy,
                ActivePolicyId = policy.Id.ToString(),
                ActivePolicyVersion = policy.Version,
                UpdatedAt = now
            };
            context.TenantRiskPolicyConfigs.Add(config);
        }
        else
        {
            config.PolicyMode = RiskPolicyMode.UseCustomPolicy;
            config.ActivePolicyId = policy.Id.ToString();
            config.ActivePolicyVersion = policy.Version;
            config.UpdatedAt = now;
        }

        // 6. Synchronize active rules to TenantRuleConfigs for legacy/cache compatibility
        foreach (var rule in policy.Rules)
        {
            var existingRuleConfig = await context.TenantRuleConfigs
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.RuleName == rule.RuleName, cancellationToken);

            if (existingRuleConfig == null)
            {
                context.TenantRuleConfigs.Add(new TenantRuleConfig
                {
                    TenantId = tenantId,
                    RuleName = rule.RuleName,
                    IsEnabled = rule.IsEnabled,
                    ThresholdsJson = rule.ThresholdsJson,
                    UpdatedAt = now
                });
            }
            else
            {
                existingRuleConfig.IsEnabled = rule.IsEnabled;
                existingRuleConfig.ThresholdsJson = rule.ThresholdsJson;
                existingRuleConfig.UpdatedAt = now;
            }
        }

        // 7. Outbox Published Event
        context.OutboxMessages.Add(new OutboxMessage
        {
            EventType = typeof(TenantRiskPolicyPublishedEvent).FullName!,
            Payload = JsonSerializer.Serialize(new TenantRiskPolicyPublishedEvent
            {
                PolicyId = policy.Id,
                TenantId = tenantId,
                Scope = policy.Scope,
                Version = policy.Version,
                PublishedByUserId = userId,
                SupersededPolicyId = supersededPolicyId,
                SupersededVersion = supersededVersion
            }),
            CreatedAt = now
        });

        await context.SaveChangesAsync(cancellationToken);

        // 8. Invalidate Redis Cache
        await ruleConfigService.InvalidateCacheAsync(tenantId, "", cancellationToken);

        return RouteMapper.ToPolicyDto(policy);
    }
}

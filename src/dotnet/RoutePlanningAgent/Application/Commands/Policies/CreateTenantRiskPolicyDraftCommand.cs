using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.DTOs.Configs;
using RoutePlanningAgent.Application.Mapping;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Domain.Services;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Enums;
using Shared.Events;
using Shared.Exceptions;
using Shared.Security;

namespace RoutePlanningAgent.Application.Commands.Policies;

public record CreateTenantRiskPolicyDraftCommand(
    string Name,
    string? Description,
    string Scope = "RoutePlanning",
    RiskPolicySource Source = RiskPolicySource.Tenant,
    string? SourceDocumentId = null,
    IReadOnlyList<TenantRiskRuleInputDto>? Rules = null
) : IRequest<TenantRiskPolicyDto>;

public class CreateTenantRiskPolicyDraftHandler(
    RoutePlanningDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateTenantRiskPolicyDraftCommand, TenantRiskPolicyDto>
{
    public async Task<TenantRiskPolicyDto> Handle(
        CreateTenantRiskPolicyDraftCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw new ForbiddenException("Tenant context is missing");
        var userId = currentUser.UserId
            ?? throw new ForbiddenException("User context is missing");

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new DomainValidationException("Tên chính sách (Name) không được để trống.");
        }

        var scope = string.IsNullOrWhiteSpace(request.Scope) ? "RoutePlanning" : request.Scope.Trim();

        // 1. Calculate next version (concurrency safe per tenant + scope)
        var maxVersion = await context.TenantRiskPolicies
            .IgnoreQueryFilters()
            .Where(p => p.TenantId == tenantId && p.Scope == scope)
            .MaxAsync(p => (int?)p.Version, cancellationToken) ?? 0;

        var nextVersion = maxVersion + 1;

        // 2. Create Policy Aggregate in DRAFT status
        var policy = new TenantRiskPolicy
        {
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Scope = scope,
            Version = nextVersion,
            Status = TenantRiskPolicyStatus.Draft,
            Source = request.Source,
            SourceDocumentId = request.SourceDocumentId,
            Rules = new List<TenantRiskRule>()
        };

        // 3. Populate initial structured rules if provided
        if (request.Rules != null && request.Rules.Count > 0)
        {
            foreach (var r in request.Rules)
            {
                if (string.IsNullOrWhiteSpace(r.RuleCode)) continue;

                var ruleCode = r.RuleCode.Trim().ToUpperInvariant();
                var ruleName = !string.IsNullOrWhiteSpace(r.RuleName)
                    ? r.RuleName.Trim()
                    : (TenantRuleValidator.CodeToNameMap.TryGetValue(ruleCode, out var mappedName) ? mappedName : ruleCode);

                var riskEffect = Enum.TryParse<RouteRiskLevel>(r.RiskEffect, true, out var parsedRisk)
                    ? parsedRisk
                    : RouteRiskLevel.High;

                var thresholdsJson = r.ThresholdsJson ?? "{}";
                TenantRuleValidator.ValidateThresholdsJson(ruleCode, thresholdsJson);

                policy.Rules.Add(new TenantRiskRule
                {
                    TenantId = tenantId,
                    RuleCode = ruleCode,
                    RuleName = ruleName,
                    ThresholdsJson = thresholdsJson,
                    RiskEffect = riskEffect,
                    IsEnabled = r.IsEnabled ?? true,
                    SourceReference = r.SourceReference
                });
            }
        }

        context.TenantRiskPolicies.Add(policy);

        // 4. Outbox Event
        context.OutboxMessages.Add(new OutboxMessage
        {
            EventType = typeof(TenantRiskPolicyCreatedEvent).FullName!,
            Payload = JsonSerializer.Serialize(new TenantRiskPolicyCreatedEvent
            {
                PolicyId = policy.Id,
                TenantId = tenantId,
                Scope = policy.Scope,
                Version = policy.Version,
                Source = policy.Source.ToString(),
                CreatedByUserId = userId
            }),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);

        return RouteMapper.ToPolicyDto(policy);
    }
}

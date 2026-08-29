using System;
using System.Collections.Generic;
using System.Linq;
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
using Shared.Exceptions;
using Shared.Security;

namespace RoutePlanningAgent.Application.Commands.Policies;

public record UpdateTenantRiskPolicyDraftCommand(
    Guid PolicyId,
    string? Name = null,
    string? Description = null,
    IReadOnlyList<TenantRiskRuleInputDto>? Rules = null
) : IRequest<TenantRiskPolicyDto>;

public class UpdateTenantRiskPolicyDraftHandler(
    RoutePlanningDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateTenantRiskPolicyDraftCommand, TenantRiskPolicyDto>
{
    public async Task<TenantRiskPolicyDto> Handle(
        UpdateTenantRiskPolicyDraftCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw new ForbiddenException("Tenant context is missing");

        var policy = await context.TenantRiskPolicies
            .Include(p => p.Rules)
            .FirstOrDefaultAsync(p => p.Id == request.PolicyId && p.TenantId == tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy chính sách rủi ro với ID '{request.PolicyId}'.");

        // Guard: Only DRAFT or REJECTED policies can be modified
        if (policy.Status != TenantRiskPolicyStatus.Draft && policy.Status != TenantRiskPolicyStatus.Rejected)
        {
            throw new DomainValidationException(
                $"Không thể chỉnh sửa chính sách ở trạng thái '{policy.Status}'. " +
                $"Chỉ có chính sách ở trạng thái 'Draft' hoặc 'Rejected' mới được phép sửa đổi.");
        }

        // If previously REJECTED, transition back to DRAFT for new review cycle
        if (policy.Status == TenantRiskPolicyStatus.Rejected)
        {
            policy.Status = TenantRiskPolicyStatus.Draft;
            policy.RejectionReason = null;
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            policy.Name = request.Name.Trim();
        }

        if (request.Description != null)
        {
            policy.Description = request.Description.Trim();
        }

        // Replace rules if provided
        if (request.Rules != null)
        {
            foreach (var existingRule in policy.Rules.ToList())
            {
                context.TenantRiskRules.Remove(existingRule);
            }

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

                context.TenantRiskRules.Add(new TenantRiskRule
                {
                    TenantId = tenantId,
                    PolicyId = policy.Id,
                    RuleCode = ruleCode,
                    RuleName = ruleName,
                    ThresholdsJson = thresholdsJson,
                    RiskEffect = riskEffect,
                    IsEnabled = r.IsEnabled ?? true,
                    SourceReference = r.SourceReference
                });
            }
        }

        policy.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        // Reload updated policy with newly inserted rules for accurate DTO response
        var reloaded = await context.TenantRiskPolicies
            .Include(p => p.Rules)
            .FirstAsync(p => p.Id == policy.Id, cancellationToken);

        return RouteMapper.ToPolicyDto(reloaded);
    }
}

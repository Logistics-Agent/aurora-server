using System;
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
using Shared.Events;
using Shared.Exceptions;
using Shared.Security;

namespace RoutePlanningAgent.Application.Commands.Policies;

public record SubmitTenantRiskPolicyCommand(Guid PolicyId) : IRequest<TenantRiskPolicyDto>;

public class SubmitTenantRiskPolicyHandler(
    RoutePlanningDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<SubmitTenantRiskPolicyCommand, TenantRiskPolicyDto>
{
    public async Task<TenantRiskPolicyDto> Handle(
        SubmitTenantRiskPolicyCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw new ForbiddenException("Tenant context is missing");
        var userId = currentUser.UserId
            ?? throw new ForbiddenException("User context is missing");

        var policy = await context.TenantRiskPolicies
            .Include(p => p.Rules)
            .FirstOrDefaultAsync(p => p.Id == request.PolicyId && p.TenantId == tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy chính sách rủi ro với ID '{request.PolicyId}'.");

        if (policy.Status != TenantRiskPolicyStatus.Draft)
        {
            throw new DomainValidationException(
                $"Chỉ có chính sách ở trạng thái 'Draft' mới có thể gửi phê duyệt. Trạng thái hiện tại: '{policy.Status}'.");
        }

        // Validate that policy has rules before submitting
        TenantRuleValidator.ValidateRulesForPublish(policy.Rules);

        policy.Status = TenantRiskPolicyStatus.PendingReview;
        policy.SubmittedByUserId = userId;
        policy.SubmittedAt = DateTimeOffset.UtcNow;
        policy.UpdatedAt = DateTimeOffset.UtcNow;

        context.OutboxMessages.Add(new OutboxMessage
        {
            EventType = typeof(TenantRiskPolicySubmittedEvent).FullName!,
            Payload = JsonSerializer.Serialize(new TenantRiskPolicySubmittedEvent
            {
                PolicyId = policy.Id,
                TenantId = tenantId,
                Scope = policy.Scope,
                Version = policy.Version,
                SubmittedByUserId = userId
            }),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);

        return RouteMapper.ToPolicyDto(policy);
    }
}

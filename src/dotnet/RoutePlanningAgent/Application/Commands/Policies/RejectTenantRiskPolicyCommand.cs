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
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Constants;
using Shared.Events;
using Shared.Exceptions;
using Shared.Security;

namespace RoutePlanningAgent.Application.Commands.Policies;

public record RejectTenantRiskPolicyCommand(
    Guid PolicyId,
    string Reason,
    string? Comment = null
) : IRequest<TenantRiskPolicyDto>;

public class RejectTenantRiskPolicyHandler(
    RoutePlanningDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<RejectTenantRiskPolicyCommand, TenantRiskPolicyDto>
{
    public async Task<TenantRiskPolicyDto> Handle(
        RejectTenantRiskPolicyCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw new ForbiddenException("Tenant context is missing");
        var userId = currentUser.UserId
            ?? throw new ForbiddenException("User context is missing");

        // Authority check: Pure Capability Authorization (Requires route_planning:policy:publish or route_planning:policy:manage)
        if (!currentUser.HasPermission(PermissionConstants.RoutePlanning.PolicyPublish) &&
            !currentUser.HasPermission(PermissionConstants.RoutePlanning.PolicyManage))
        {
            throw new ForbiddenException("Bạn không có quyền từ chối / xét duyệt chính sách rủi ro (yêu cầu quyền 'route_planning:policy:publish' hoặc 'route_planning:policy:manage').");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new DomainValidationException("Lý do từ chối (Reason) là bắt buộc khi từ chối chính sách rủi ro.");
        }

        var policy = await context.TenantRiskPolicies
            .Include(p => p.Rules)
            .FirstOrDefaultAsync(p => p.Id == request.PolicyId && p.TenantId == tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy chính sách rủi ro với ID '{request.PolicyId}'.");

        if (policy.Status != TenantRiskPolicyStatus.PendingReview)
        {
            throw new DomainValidationException(
                $"Chỉ có chính sách ở trạng thái 'PendingReview' mới có thể từ chối. Trạng thái hiện tại: '{policy.Status}'.");
        }

        policy.Status = TenantRiskPolicyStatus.Rejected;
        policy.ReviewedByUserId = userId;
        policy.ReviewedAt = DateTimeOffset.UtcNow;
        policy.ReviewerComment = request.Comment?.Trim();
        policy.RejectionReason = request.Reason.Trim();
        policy.UpdatedAt = DateTimeOffset.UtcNow;

        context.OutboxMessages.Add(new OutboxMessage
        {
            EventType = typeof(TenantRiskPolicyRejectedEvent).FullName!,
            Payload = JsonSerializer.Serialize(new TenantRiskPolicyRejectedEvent
            {
                PolicyId = policy.Id,
                TenantId = tenantId,
                Scope = policy.Scope,
                Version = policy.Version,
                ReviewedByUserId = userId,
                RejectionReason = policy.RejectionReason
            }),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await context.SaveChangesAsync(cancellationToken);

        return RouteMapper.ToPolicyDto(policy);
    }
}

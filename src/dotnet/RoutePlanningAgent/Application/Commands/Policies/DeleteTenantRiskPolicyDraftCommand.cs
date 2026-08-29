using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Exceptions;
using Shared.Security;

namespace RoutePlanningAgent.Application.Commands.Policies;

public record DeleteTenantRiskPolicyDraftCommand(Guid PolicyId) : IRequest<bool>;

public class DeleteTenantRiskPolicyDraftHandler(
    RoutePlanningDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteTenantRiskPolicyDraftCommand, bool>
{
    public async Task<bool> Handle(
        DeleteTenantRiskPolicyDraftCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw new ForbiddenException("Tenant context is missing");

        var policy = await context.TenantRiskPolicies
            .Include(p => p.Rules)
            .FirstOrDefaultAsync(p => p.Id == request.PolicyId && p.TenantId == tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy chính sách rủi ro với ID '{request.PolicyId}'.");

        // CRITICAL INVARIANT: Never soft-delete historical Active or Superseded policies
        if (policy.Status == TenantRiskPolicyStatus.Active || policy.Status == TenantRiskPolicyStatus.Superseded)
        {
            throw new DomainValidationException(
                $"Không được phép xoá chính sách ở trạng thái '{policy.Status}'. " +
                $"Chính sách đã phát hành hoặc đã được lưu lịch sử cần được bảo toàn để phục vụ audit trail.");
        }

        // Soft delete policy and child rules
        policy.IsDeleted = true;
        policy.UpdatedAt = DateTimeOffset.UtcNow;

        foreach (var rule in policy.Rules)
        {
            rule.IsDeleted = true;
            rule.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);

        return true;
    }
}

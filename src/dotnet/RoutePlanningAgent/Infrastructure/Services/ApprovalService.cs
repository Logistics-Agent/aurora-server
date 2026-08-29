using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Exceptions;

namespace RoutePlanningAgent.Infrastructure.Services;

public class ApprovalService(RoutePlanningDbContext context) : IApprovalService
{
    public async Task<ApprovalRequest> CreateAsync(
        Guid routeId,
        string reason,
        string aiSummary,
        string? complianceSummary,
        int routeVersion = 1,
        string policyId = "platform-default-route-governance",
        int policyVersion = 1,
        CancellationToken ct = default)
    {
        var route = await context.Routes.FirstOrDefaultAsync(r => r.Id == routeId, ct)
            ?? throw new NotFoundException($"Route '{routeId}' not found");

        var approval = new ApprovalRequest
        {
            RouteId = routeId,
            RouteVersion = routeVersion > 0 ? routeVersion : route.Version,
            PolicyId = policyId,
            PolicyVersion = policyVersion > 0 ? policyVersion : 1,
            Feature = "RoutePlanning",
            Status = ApprovalStatus.Pending,
            Reason = reason,
            AiSummary = aiSummary,
            ComplianceSummary = complianceSummary,
            TenantId = route.TenantId
        };

        context.ApprovalRequests.Add(approval);
        // KHÔNG SaveChanges — caller (RequestRouteRecommendationHandler) sở hữu transaction
        return approval;
    }

    public async Task<ApprovalRequest> ApproveAsync(
        Guid approvalId, Guid reviewerUserId, string? comment, CancellationToken ct = default)
    {
        var approval = await LoadPendingAsync(approvalId, ct);

        approval.Status = ApprovalStatus.Approved;
        approval.ReviewedByUserId = reviewerUserId;
        approval.ReviewedAt = DateTimeOffset.UtcNow;
        approval.ReviewerComment = comment;

        // Phê duyệt → route sẵn sàng vận hành và cấp quyền StaffAllowed
        if (approval.Route is not null)
        {
            approval.Route.Status = RouteStatus.Ready;
            approval.Route.GovernanceDecision = Shared.Enums.GovernanceDecision.StaffAllowed;
        }

        await context.SaveChangesAsync(ct);
        return approval;
    }

    public async Task<ApprovalRequest> RejectAsync(
        Guid approvalId, Guid reviewerUserId, string reason, string? comment, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Reason là bắt buộc khi reject approval request");

        var approval = await LoadPendingAsync(approvalId, ct);

        approval.Status = ApprovalStatus.Rejected;
        approval.ReviewedByUserId = reviewerUserId;
        approval.ReviewedAt = DateTimeOffset.UtcNow;
        approval.RejectionReason = reason;
        approval.ReviewerComment = comment;

        // Từ chối → chuyển về Draft (Rework) để nhân viên điều chỉnh dữ liệu nghiệp vụ
        if (approval.Route is not null)
        {
            approval.Route.Status = RouteStatus.Draft;
            approval.Route.GovernanceDecision = Shared.Enums.GovernanceDecision.ManagerApprovalRequired;
        }

        await context.SaveChangesAsync(ct);
        return approval;
    }

    private async Task<ApprovalRequest> LoadPendingAsync(Guid approvalId, CancellationToken ct)
    {
        var approval = await context.ApprovalRequests
            .Include(a => a.Route)
            .FirstOrDefaultAsync(a => a.Id == approvalId, ct)
            ?? throw new NotFoundException($"Approval request '{approvalId}' not found");

        if (approval.Status != ApprovalStatus.Pending)
            throw new ConflictException($"Approval request đã được xử lý (trạng thái hiện tại: {approval.Status})");

        return approval;
    }
}

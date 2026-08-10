using System;
using System.Threading;
using System.Threading.Tasks;
using RoutePlanningAgent.Domain;

namespace RoutePlanningAgent.Application.Interfaces;

public interface IApprovalService
{
    /// <summary>
    /// Tạo approval request — KHÔNG tự SaveChanges (caller sở hữu transaction).
    /// </summary>
    Task<ApprovalRequest> CreateAsync(Guid routeId, string reason, string aiSummary, string? complianceSummary, CancellationToken ct = default);

    /// <summary>Phê duyệt: approval → Approved, route → Ready.</summary>
    Task<ApprovalRequest> ApproveAsync(Guid approvalId, Guid reviewerUserId, string? comment, CancellationToken ct = default);

    /// <summary>Từ chối: approval → Rejected (kèm reason bắt buộc), route → Cancelled.</summary>
    Task<ApprovalRequest> RejectAsync(Guid approvalId, Guid reviewerUserId, string reason, string? comment, CancellationToken ct = default);
}

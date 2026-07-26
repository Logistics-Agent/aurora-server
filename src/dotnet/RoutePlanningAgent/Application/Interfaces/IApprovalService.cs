using System;
using System.Threading;
using System.Threading.Tasks;
using RoutePlanningAgent.Domain;

namespace RoutePlanningAgent.Application.Interfaces;

public interface IApprovalService
{
    Task<ApprovalRequest> CreateAsync(Guid routeId, string reason, string aiSummary, string? complianceSummary, CancellationToken ct = default);
    Task<ApprovalRequest> ApproveAsync(Guid approvalId, Guid reviewerUserId, string? comment, CancellationToken ct = default);
    Task<ApprovalRequest> RejectAsync(Guid approvalId, Guid reviewerUserId, string? comment, CancellationToken ct = default);
}

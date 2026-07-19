using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.Persistences;

namespace RoutePlanningAgent.Infrastructure.Services;

public class ApprovalService(RoutePlanningDbContext context) : IApprovalService
{
    public async Task<ApprovalRequest> CreateAsync(
        Guid routeId, string reason, string aiSummary, string? complianceSummary, CancellationToken ct = default)
    {
        var route = await context.Routes.FirstOrDefaultAsync(r => r.Id == routeId, ct)
            ?? throw new Exception("Route not found");

        var approval = new ApprovalRequest
        {
            RouteId = routeId,
            Feature = "RoutePlanning",
            Status = ApprovalStatus.Pending,
            Reason = reason,
            AiSummary = aiSummary,
            ComplianceSummary = complianceSummary,
            TenantId = route.TenantId
        };

        context.ApprovalRequests.Add(approval);
        await context.SaveChangesAsync(ct);

        return approval;
    }

    public async Task<ApprovalRequest> ApproveAsync(
        Guid approvalId, Guid reviewerUserId, string? comment, CancellationToken ct = default)
    {
        var approval = await context.ApprovalRequests
            .Include(a => a.Route)
            .FirstOrDefaultAsync(a => a.Id == approvalId, ct)
            ?? throw new Exception("Approval request not found");

        if (approval.Status != ApprovalStatus.Pending)
            throw new Exception("Approval request is already processed");

        approval.Status = ApprovalStatus.Approved;
        approval.ReviewedByUserId = reviewerUserId;
        approval.ReviewedAt = DateTimeOffset.UtcNow;
        approval.ReviewerComment = comment;

        // If approved, update route status
        if (approval.Route is not null)
        {
            approval.Route.Status = RouteStatus.Ready;
        }

        await context.SaveChangesAsync(ct);
        return approval;
    }

    public async Task<ApprovalRequest> RejectAsync(
        Guid approvalId, Guid reviewerUserId, string? comment, CancellationToken ct = default)
    {
        var approval = await context.ApprovalRequests
            .Include(a => a.Route)
            .FirstOrDefaultAsync(a => a.Id == approvalId, ct)
            ?? throw new Exception("Approval request not found");

        if (approval.Status != ApprovalStatus.Pending)
            throw new Exception("Approval request is already processed");

        approval.Status = ApprovalStatus.Rejected;
        approval.ReviewedByUserId = reviewerUserId;
        approval.ReviewedAt = DateTimeOffset.UtcNow;
        approval.ReviewerComment = comment;

        // If rejected, update route status
        if (approval.Route is not null)
        {
            approval.Route.Status = RouteStatus.Cancelled;
        }

        await context.SaveChangesAsync(ct);
        return approval;
    }
}

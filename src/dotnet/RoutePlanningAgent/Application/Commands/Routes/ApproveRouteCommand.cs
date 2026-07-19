using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Domain;
using Shared.Security;

namespace RoutePlanningAgent.Application.Commands.Routes;

public record ApproveRouteCommand(
    Guid ApprovalId,
    bool IsApproved,
    string? Comment
) : IRequest<ApprovalRequestDto>;

public class ApproveRouteHandler(
    IApprovalService approvalService,
    ICurrentUserService currentUser)
    : IRequestHandler<ApproveRouteCommand, ApprovalRequestDto>
{
    public async Task<ApprovalRequestDto> Handle(ApproveRouteCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new Exception("User ID context is missing");

        ApprovalRequest approval;
        if (request.IsApproved)
        {
            approval = await approvalService.ApproveAsync(request.ApprovalId, userId, request.Comment, cancellationToken);
        }
        else
        {
            approval = await approvalService.RejectAsync(request.ApprovalId, userId, request.Comment, cancellationToken);
        }

        return new ApprovalRequestDto
        {
            Id = approval.Id,
            RouteId = approval.RouteId,
            RouteName = approval.Route?.Name ?? string.Empty,
            Status = approval.Status.ToString(),
            Reason = approval.Reason,
            AiSummary = approval.AiSummary,
            ComplianceSummary = approval.ComplianceSummary,
            CreatedAt = approval.CreatedAt
        };
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Application.Mapping;
using Shared.Exceptions;
using Shared.Security;

namespace RoutePlanningAgent.Application.Commands.Routes;

/// <summary>
/// Phê duyệt approval request. Từ chối dùng RejectRouteCommand (tách riêng, bắt buộc Reason).
/// </summary>
public record ApproveRouteCommand(
    Guid ApprovalId,
    string? Comment
) : IRequest<ApprovalRequestDto>;

public class ApproveRouteHandler(
    IApprovalService approvalService,
    ICurrentUserService currentUser)
    : IRequestHandler<ApproveRouteCommand, ApprovalRequestDto>
{
    public async Task<ApprovalRequestDto> Handle(ApproveRouteCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenException("User context is missing");

        var approval = await approvalService.ApproveAsync(
            request.ApprovalId, userId, request.Comment, cancellationToken);

        return RouteMapper.ToApprovalDto(approval);
    }
}

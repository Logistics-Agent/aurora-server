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
/// Từ chối approval request. Reason là BẮT BUỘC — người duyệt phải nêu lý do từ chối.
/// </summary>
public record RejectRouteCommand(
    Guid ApprovalId,
    string Reason,
    string? Comment
) : IRequest<ApprovalRequestDto>;

public class RejectRouteHandler(
    IApprovalService approvalService,
    ICurrentUserService currentUser)
    : IRequestHandler<RejectRouteCommand, ApprovalRequestDto>
{
    public async Task<ApprovalRequestDto> Handle(RejectRouteCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new ForbiddenException("User context is missing");

        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new DomainException("Reason là bắt buộc khi reject approval request");

        var approval = await approvalService.RejectAsync(
            request.ApprovalId, userId, request.Reason, request.Comment, cancellationToken);

        return RouteMapper.ToApprovalDto(approval);
    }
}

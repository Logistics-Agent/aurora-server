using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Application.Mapping;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Events;
using Shared.Exceptions;
using Shared.Security;

namespace RoutePlanningAgent.Application.Commands.Routes;

public record UpdateRouteStatusCommand(Guid Id, string NewStatus) : IRequest<RouteDto>;

public class UpdateRouteStatusHandler(
    RoutePlanningDbContext context,
    IRouteGovernanceService governanceService,
    IRouteRiskPolicyProvider policyProvider,
    ICurrentUserService currentUser,
    IOutboxWriter outbox)
    : IRequestHandler<UpdateRouteStatusCommand, RouteDto>
{
    private const string Feature = "RoutePlanning";

    /// <summary>
    /// Bảng chuyển trạng thái hợp lệ. Approval flow (Approve/Reject) là "người ghi" thứ hai
    /// được phép set Ready/Draft/Cancelled trực tiếp qua ApprovalService.
    /// </summary>
    public static readonly IReadOnlyDictionary<RouteStatus, RouteStatus[]> AllowedTransitions =
        new Dictionary<RouteStatus, RouteStatus[]>
        {
            [RouteStatus.Draft] = [RouteStatus.Optimizing, RouteStatus.Cancelled],
            [RouteStatus.Optimizing] = [RouteStatus.Ready, RouteStatus.Draft],
            [RouteStatus.Ready] = [RouteStatus.Active, RouteStatus.Draft, RouteStatus.Cancelled],
            [RouteStatus.Active] = [RouteStatus.Completed, RouteStatus.Cancelled],
            [RouteStatus.Completed] = [RouteStatus.Archived],
            [RouteStatus.Cancelled] = [RouteStatus.Archived]
        };

    public async Task<RouteDto> Handle(UpdateRouteStatusCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw new ForbiddenException("Tenant context is missing");

        if (!Enum.TryParse<RouteStatus>(request.NewStatus, true, out var newStatus))
            throw new DomainException(
                $"RouteStatus '{request.NewStatus}' không hợp lệ. Giá trị cho phép: {string.Join(", ", Enum.GetNames<RouteStatus>())}");

        var route = await context.Routes
            .Include(r => r.Stops)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Route '{request.Id}' not found");

        var allowed = AllowedTransitions.GetValueOrDefault(route.Status, []);
        if (!allowed.Contains(newStatus))
            throw new DomainException(
                $"Không thể chuyển từ {route.Status} sang {newStatus}. " +
                $"Trạng thái cho phép: {(allowed.Length > 0 ? string.Join(", ", allowed) : "(không có)")}");

        // EXECUTION BOUNDARY: Kiểm tra thẩm quyền quản trị toàn diện khi kích hoạt vận hành (Active)
        if (newStatus == RouteStatus.Active)
        {
            var effectivePolicy = await policyProvider.GetEffectivePolicyAsync(tenantId, Feature, cancellationToken);
            await governanceService.ValidateExecutionAuthorizedAsync(route, effectivePolicy, cancellationToken);
        }

        var oldStatus = route.Status;
        route.Status = newStatus;

        outbox.Enqueue(new RouteStatusChangedEvent
        {
            RouteId = route.Id,
            TenantId = tenantId,
            OldStatus = oldStatus.ToString(),
            NewStatus = newStatus.ToString(),
            ChangedByUserId = currentUser.UserId ?? Guid.Empty
        });

        await context.SaveChangesAsync(cancellationToken);

        return RouteMapper.ToDto(route);
    }
}

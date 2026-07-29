using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Events;
using Shared.Exceptions;
using Shared.Security;

namespace RoutePlanningAgent.Application.Commands.Routes;

public record DeleteRouteCommand(Guid Id) : IRequest<bool>;

public class DeleteRouteHandler(
    RoutePlanningDbContext context,
    ICurrentUserService currentUser,
    IOutboxWriter outbox)
    : IRequestHandler<DeleteRouteCommand, bool>
{
    public async Task<bool> Handle(DeleteRouteCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw new ForbiddenException("Tenant context is missing");

        var route = await context.Routes
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Route '{request.Id}' not found");

        if (route.Status == RouteStatus.Active)
            throw new ConflictException("Route đang Active — không thể xoá. Hãy Complete/Cancel trước.");

        // Soft delete — global query filter sẽ ẩn route khỏi mọi query
        route.IsDeleted = true;

        outbox.Enqueue(new RouteDeletedEvent
        {
            RouteId = route.Id,
            TenantId = tenantId,
            DeletedByUserId = currentUser.UserId ?? Guid.Empty
        });

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}

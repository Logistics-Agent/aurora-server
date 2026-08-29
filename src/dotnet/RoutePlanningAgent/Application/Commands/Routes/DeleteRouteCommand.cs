using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Enums;
using Shared.Events;
using Shared.Exceptions;
using Shared.Security;

namespace RoutePlanningAgent.Application.Commands.Routes;

public record DeleteRouteCommand(Guid Id) : IRequest<bool>;

public class DeleteRouteHandler(
    RoutePlanningDbContext context,
    IRouteGovernanceService governanceService,
    IRouteRiskPolicyProvider policyProvider,
    ICurrentUserService currentUser,
    IOutboxWriter outbox)
    : IRequestHandler<DeleteRouteCommand, bool>
{
    private const string Feature = "RoutePlanning";

    public async Task<bool> Handle(DeleteRouteCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw new ForbiddenException("Tenant context is missing");

        var route = await context.Routes
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Route '{request.Id}' not found");

        var effectivePolicy = await policyProvider.GetEffectivePolicyAsync(tenantId, Feature, cancellationToken);

        // Đánh giá rủi ro xóa theo tác động nghiệp vụ và chính sách hiệu lực
        var assessment = governanceService.AssessSoftDeleteRisk(route, effectivePolicy);
        if (assessment.Decision == GovernanceDecision.Blocked)
        {
            throw new ConflictException(assessment.ReasonDetails);
        }

        // Soft delete — global query filter sẽ ẩn route khỏi mọi query thông thường
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

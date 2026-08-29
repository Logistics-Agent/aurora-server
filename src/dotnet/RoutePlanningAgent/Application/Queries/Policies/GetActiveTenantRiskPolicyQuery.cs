using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.DTOs.Configs;
using RoutePlanningAgent.Application.Mapping;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Exceptions;
using Shared.Security;

namespace RoutePlanningAgent.Application.Queries.Policies;

public record GetActiveTenantRiskPolicyQuery(string Scope = "RoutePlanning") : IRequest<TenantRiskPolicyDto?>;

public class GetActiveTenantRiskPolicyHandler(
    RoutePlanningDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<GetActiveTenantRiskPolicyQuery, TenantRiskPolicyDto?>
{
    public async Task<TenantRiskPolicyDto?> Handle(
        GetActiveTenantRiskPolicyQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw new ForbiddenException("Tenant context is missing");

        var scope = string.IsNullOrWhiteSpace(request.Scope) ? "RoutePlanning" : request.Scope.Trim();

        var policy = await context.TenantRiskPolicies
            .AsNoTracking()
            .Include(p => p.Rules)
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Scope == scope && p.Status == TenantRiskPolicyStatus.Active, cancellationToken);

        return policy == null ? null : RouteMapper.ToPolicyDto(policy);
    }
}

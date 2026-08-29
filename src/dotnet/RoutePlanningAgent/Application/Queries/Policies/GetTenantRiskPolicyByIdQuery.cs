using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.DTOs.Configs;
using RoutePlanningAgent.Application.Mapping;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Exceptions;
using Shared.Security;

namespace RoutePlanningAgent.Application.Queries.Policies;

public record GetTenantRiskPolicyByIdQuery(Guid PolicyId) : IRequest<TenantRiskPolicyDto>;

public class GetTenantRiskPolicyByIdHandler(
    RoutePlanningDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<GetTenantRiskPolicyByIdQuery, TenantRiskPolicyDto>
{
    public async Task<TenantRiskPolicyDto> Handle(
        GetTenantRiskPolicyByIdQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw new ForbiddenException("Tenant context is missing");

        var policy = await context.TenantRiskPolicies
            .AsNoTracking()
            .Include(p => p.Rules)
            .FirstOrDefaultAsync(p => p.Id == request.PolicyId && p.TenantId == tenantId, cancellationToken)
            ?? throw new KeyNotFoundException($"Không tìm thấy chính sách rủi ro với ID '{request.PolicyId}'.");

        return RouteMapper.ToPolicyDto(policy);
    }
}

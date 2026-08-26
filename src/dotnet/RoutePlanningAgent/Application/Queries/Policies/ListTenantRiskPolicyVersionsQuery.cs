using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.DTOs.Configs;
using RoutePlanningAgent.Application.Mapping;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Exceptions;
using Shared.Pagination;
using Shared.Security;

namespace RoutePlanningAgent.Application.Queries.Policies;

public record ListTenantRiskPolicyVersionsQuery(
    string Scope = "RoutePlanning",
    int Page = 1,
    int Limit = 20
) : IRequest<PagedResult<TenantRiskPolicyDto>>;

public class ListTenantRiskPolicyVersionsHandler(
    RoutePlanningDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<ListTenantRiskPolicyVersionsQuery, PagedResult<TenantRiskPolicyDto>>
{
    public async Task<PagedResult<TenantRiskPolicyDto>> Handle(
        ListTenantRiskPolicyVersionsQuery request,
        CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw new ForbiddenException("Tenant context is missing");

        var scope = string.IsNullOrWhiteSpace(request.Scope) ? "RoutePlanning" : request.Scope.Trim();

        var query = context.TenantRiskPolicies
            .AsNoTracking()
            .Include(p => p.Rules)
            .Where(p => p.TenantId == tenantId && p.Scope == scope)
            .OrderByDescending(p => p.Version);

        var paged = await query.ToPagedResultAsync(
            new PagedRequest { Page = request.Page, Limit = request.Limit }, cancellationToken);

        return new PagedResult<TenantRiskPolicyDto>(
            paged.Items.Select(RouteMapper.ToPolicyDto).ToList(),
            paged.TotalItems,
            paged.Page,
            paged.Limit
        );
    }
}

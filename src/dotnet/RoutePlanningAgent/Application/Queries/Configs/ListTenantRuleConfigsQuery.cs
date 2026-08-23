using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.DTOs.Configs;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Exceptions;
using Shared.Pagination;
using Shared.Security;

namespace RoutePlanningAgent.Application.Queries.Configs;

public record ListTenantRuleConfigsQuery(int Page, int Limit) : IRequest<PagedResult<TenantRuleConfigDto>>;

public class ListTenantRuleConfigsHandler(
    RoutePlanningDbContext context,
    ICurrentUserService currentUser)
    : IRequestHandler<ListTenantRuleConfigsQuery, PagedResult<TenantRuleConfigDto>>
{
    public async Task<PagedResult<TenantRuleConfigDto>> Handle(
        ListTenantRuleConfigsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw new ForbiddenException("Tenant context is missing");

        var paged = await context.TenantRuleConfigs
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.RuleName)
            .ToPagedResultAsync(new PagedRequest { Page = request.Page, Limit = request.Limit }, cancellationToken);

        return new PagedResult<TenantRuleConfigDto>(
            paged.Items.Select(c => new TenantRuleConfigDto
            {
                Id = c.Id,
                TenantId = c.TenantId,
                RuleName = c.RuleName,
                IsEnabled = c.IsEnabled,
                Thresholds = JsonSerializer.Deserialize<Dictionary<string, decimal>>(c.ThresholdsJson) ?? [],
                UpdatedAt = c.UpdatedAt
            }).ToList(),
            paged.TotalItems,
            paged.Page,
            paged.Limit);
    }
}

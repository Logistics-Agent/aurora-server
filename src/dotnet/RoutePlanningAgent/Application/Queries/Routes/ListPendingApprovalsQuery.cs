using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Application.Mapping;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Pagination;

namespace RoutePlanningAgent.Application.Queries.Routes;

public record ListPendingApprovalsQuery(int Page, int Limit) : IRequest<PagedResult<ApprovalRequestDto>>;

public class ListPendingApprovalsHandler(RoutePlanningDbContext context)
    : IRequestHandler<ListPendingApprovalsQuery, PagedResult<ApprovalRequestDto>>
{
    public async Task<PagedResult<ApprovalRequestDto>> Handle(
        ListPendingApprovalsQuery request, CancellationToken cancellationToken)
    {
        var paged = await context.ApprovalRequests
            .Include(a => a.Route)
            .AsNoTracking()
            .Where(a => a.Status == ApprovalStatus.Pending)
            .OrderByDescending(a => a.CreatedAt)
            .ToPagedResultAsync(new PagedRequest { Page = request.Page, Limit = request.Limit }, cancellationToken);

        return new PagedResult<ApprovalRequestDto>(
            paged.Items.Select(RouteMapper.ToApprovalDto).ToList(),
            paged.TotalItems,
            paged.Page,
            paged.Limit);
    }
}

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Application.Mapping;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Exceptions;
using Shared.Pagination;

namespace RoutePlanningAgent.Application.Queries.Routes;

/// <summary>
/// Danh sách route có phân trang + filter theo status (rỗng = tất cả).
/// </summary>
public record ListRoutesQuery(int Page, int Limit, string? Status) : IRequest<PagedResult<RouteDto>>;

public class ListRoutesHandler(RoutePlanningDbContext context)
    : IRequestHandler<ListRoutesQuery, PagedResult<RouteDto>>
{
    public async Task<PagedResult<RouteDto>> Handle(ListRoutesQuery request, CancellationToken cancellationToken)
    {
        var query = context.Routes
            .Include(r => r.Stops)
            .AsNoTracking()
            .AsSplitQuery();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<RouteStatus>(request.Status, true, out var status))
                throw new DomainException(
                    $"RouteStatus '{request.Status}' không hợp lệ. Giá trị cho phép: {string.Join(", ", Enum.GetNames<RouteStatus>())}");

            query = query.Where(r => r.Status == status);
        }

        var paged = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToPagedResultAsync(new PagedRequest { Page = request.Page, Limit = request.Limit }, cancellationToken);

        return new PagedResult<RouteDto>(
            paged.Items.Select(RouteMapper.ToDto).ToList(),
            paged.TotalItems,
            paged.Page,
            paged.Limit);
    }
}

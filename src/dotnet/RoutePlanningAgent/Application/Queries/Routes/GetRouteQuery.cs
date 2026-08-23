using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Application.Mapping;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Exceptions;

namespace RoutePlanningAgent.Application.Queries.Routes;

public record GetRouteQuery(Guid Id) : IRequest<RouteDto>;

public class GetRouteHandler(RoutePlanningDbContext context) : IRequestHandler<GetRouteQuery, RouteDto>
{
    public async Task<RouteDto> Handle(GetRouteQuery request, CancellationToken cancellationToken)
    {
        var route = await context.Routes
            .Include(r => r.Stops)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Route '{request.Id}' not found");

        return RouteMapper.ToDto(route);
    }
}

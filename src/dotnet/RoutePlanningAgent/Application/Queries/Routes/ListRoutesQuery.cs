using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Infrastructure.Persistences;

namespace RoutePlanningAgent.Application.Queries.Routes;

public record ListRoutesQuery : IRequest<List<RouteDto>>;

public class ListRoutesHandler(RoutePlanningDbContext context) : IRequestHandler<ListRoutesQuery, List<RouteDto>>
{
    public async Task<List<RouteDto>> Handle(ListRoutesQuery request, CancellationToken cancellationToken)
    {
        var routes = await context.Routes
            .Include(r => r.Stops)
            .AsNoTracking()
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return routes.Select(route => new RouteDto
        {
            Id = route.Id,
            TenantId = route.TenantId,
            Name = route.Name,
            Description = route.Description,
            RouteType = route.Type.ToString(),
            Status = route.Status.ToString(),
            RiskLevel = route.RiskLevel.ToString(),
            EstimatedDistanceKm = route.EstimatedDistanceKm,
            EstimatedDurationMinutes = route.EstimatedDurationMinutes,
            MaxWeightKg = route.MaxWeightKg,
            MaxVolumeM3 = route.MaxVolumeM3,
            IsAiGenerated = route.IsAiGenerated,
            OptimizedAt = route.OptimizedAt,
            Version = route.Version,
            CreatedAt = route.CreatedAt,
            Stops = route.Stops.Select(s => new RouteStopDto
            {
                Id = s.Id,
                Sequence = s.Sequence,
                StopType = s.StopType.ToString(),
                LocationName = s.LocationName,
                Address = s.Address,
                Latitude = s.Latitude,
                Longitude = s.Longitude,
                EstimatedArrivalMinutes = s.EstimatedArrivalMinutes,
                ServiceDurationMinutes = s.ServiceDurationMinutes
            }).OrderBy(s => s.Sequence).ToList()
        }).ToList();
    }
}

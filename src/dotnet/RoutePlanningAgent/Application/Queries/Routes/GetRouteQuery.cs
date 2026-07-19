using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Infrastructure.Persistences;

namespace RoutePlanningAgent.Application.Queries.Routes;

public record GetRouteQuery(Guid Id) : IRequest<RouteDto>;

public class GetRouteHandler(RoutePlanningDbContext context) : IRequestHandler<GetRouteQuery, RouteDto>
{
    public async Task<RouteDto> Handle(GetRouteQuery request, CancellationToken cancellationToken)
    {
        var route = await context.Routes
            .Include(r => r.Stops)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new Exception("Route not found");

        return new RouteDto
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
        };
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MassTransit;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Security;
using Shared.Events;

namespace RoutePlanningAgent.Application.Commands.Routes;

public record CreateRouteCommand(
    string Name,
    string? Description,
    string RouteType,
    decimal MaxWeightKg,
    decimal MaxVolumeM3,
    List<RouteStopInputDto> Stops
) : IRequest<RouteDto>;

public class CreateRouteHandler(
    RoutePlanningDbContext context,
    ICurrentUserService currentUser,
    IPublishEndpoint publisher)
    : IRequestHandler<CreateRouteCommand, RouteDto>
{
    public async Task<RouteDto> Handle(CreateRouteCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId ?? throw new Exception("Tenant ID context is missing");

        if (!Enum.TryParse<RouteType>(request.RouteType, true, out var routeType))
            routeType = RouteType.Fixed;

        var route = new Route
        {
            Name = request.Name,
            Description = request.Description,
            Type = routeType,
            Status = RouteStatus.Draft,
            RiskLevel = Shared.Enums.RouteRiskLevel.Low,
            MaxWeightKg = request.MaxWeightKg,
            MaxVolumeM3 = request.MaxVolumeM3,
            IsAiGenerated = false,
            TenantId = tenantId
        };

        foreach (var stopInput in request.Stops)
        {
            if (!Enum.TryParse<StopType>(stopInput.StopType, true, out var stopType))
                stopType = StopType.Pickup;

            route.Stops.Add(new RouteStop
            {
                Sequence = stopInput.Sequence,
                StopType = stopType,
                LocationName = stopInput.LocationName,
                Address = stopInput.Address,
                Latitude = stopInput.Latitude,
                Longitude = stopInput.Longitude,
                EstimatedArrivalMinutes = stopInput.EstimatedArrivalMinutes,
                ServiceDurationMinutes = stopInput.ServiceDurationMinutes,
                Route = route
            });
        }

        context.Routes.Add(route);
        await context.SaveChangesAsync(cancellationToken);

        // Publish route created event
        await publisher.Publish(new RouteCreatedEvent
        {
            RouteId = route.Id,
            TenantId = tenantId,
            RouteName = route.Name,
            RouteType = route.Type.ToString(),
            RiskLevel = route.RiskLevel.ToString(),
            CreatedByUserId = currentUser.UserId ?? Guid.Empty
        }, cancellationToken);

        return MapToDto(route);
    }

    private static RouteDto MapToDto(Route route)
    {
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

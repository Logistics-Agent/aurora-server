using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Application.Mapping;
using RoutePlanningAgent.Application.Validation;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Exceptions;
using Shared.Security;
using Shared.Events;
using Route = RoutePlanningAgent.Domain.Route; // tránh nhầm với Microsoft.AspNetCore.Routing.Route

namespace RoutePlanningAgent.Application.Commands.Routes;

public record CreateRouteCommand(
    string Name,
    string? Description,
    string RouteType,
    decimal MaxWeightKg,
    decimal MaxVolumeM3,
    decimal EstimatedDistanceKm,
    int EstimatedDurationMinutes,
    List<RouteStopInputDto> Stops
) : IRequest<RouteDto>;

public class CreateRouteHandler(
    RoutePlanningDbContext context,
    ICurrentUserService currentUser,
    IOutboxWriter outbox)
    : IRequestHandler<CreateRouteCommand, RouteDto>
{
    public async Task<RouteDto> Handle(CreateRouteCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw new ForbiddenException("Tenant context is missing");

        RouteValidator.Validate(
            request.Name, request.MaxWeightKg, request.MaxVolumeM3,
            request.EstimatedDistanceKm, request.EstimatedDurationMinutes, request.Stops);

        var routeType = RouteValidator.ParseRouteType(request.RouteType);

        var route = new Route
        {
            Name = request.Name,
            Description = request.Description,
            Type = routeType,
            Status = RouteStatus.Draft,
            RiskLevel = Shared.Enums.RouteRiskLevel.Low,
            MaxWeightKg = request.MaxWeightKg,
            MaxVolumeM3 = request.MaxVolumeM3,
            // Ước tính ban đầu nhập tay — sẽ bị ghi đè khi OptimizeRoute (OSRM+VROOM) chạy
            EstimatedDistanceKm = request.EstimatedDistanceKm,
            EstimatedDurationMinutes = request.EstimatedDurationMinutes,
            IsAiGenerated = false,
            TenantId = tenantId
        };

        foreach (var stopInput in request.Stops)
        {
            route.Stops.Add(new RouteStop
            {
                Sequence = stopInput.Sequence,
                StopType = RouteValidator.ParseStopType(stopInput.StopType),
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

        // Outbox: event được ghi CÙNG transaction — OutboxProcessor relay lên RabbitMQ sau
        outbox.Enqueue(new RouteCreatedEvent
        {
            RouteId = route.Id,
            TenantId = tenantId,
            RouteName = route.Name,
            RouteType = route.Type.ToString(),
            RiskLevel = route.RiskLevel.ToString(),
            CreatedByUserId = currentUser.UserId ?? Guid.Empty
        });

        await context.SaveChangesAsync(cancellationToken);

        return RouteMapper.ToDto(route);
    }
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Application.Mapping;
using RoutePlanningAgent.Application.Validation;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Events;
using Shared.Exceptions;
using Shared.Security;

namespace RoutePlanningAgent.Application.Commands.Routes;

public record UpdateRouteCommand(
    Guid Id,
    string Name,
    string? Description,
    string RouteType,
    decimal MaxWeightKg,
    decimal MaxVolumeM3,
    decimal EstimatedDistanceKm,
    int EstimatedDurationMinutes,
    List<RouteStopInputDto> Stops
) : IRequest<RouteDto>;

public class UpdateRouteHandler(
    RoutePlanningDbContext context,
    ICurrentUserService currentUser,
    IOutboxWriter outbox)
    : IRequestHandler<UpdateRouteCommand, RouteDto>
{
    public async Task<RouteDto> Handle(UpdateRouteCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw new ForbiddenException("Tenant context is missing");

        var route = await context.Routes
            .Include(r => r.Stops)
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException($"Route '{request.Id}' not found");

        // Chỉ Draft/Ready mới được sửa
        if (route.Status is not (RouteStatus.Draft or RouteStatus.Ready))
            throw new ConflictException(
                $"Route đang ở trạng thái {route.Status} — chỉ Draft hoặc Ready mới được sửa");

        RouteValidator.Validate(
            request.Name, request.MaxWeightKg, request.MaxVolumeM3,
            request.EstimatedDistanceKm, request.EstimatedDurationMinutes, request.Stops);

        route.Name = request.Name;
        route.Description = request.Description;
        route.Type = RouteValidator.ParseRouteType(request.RouteType);
        route.MaxWeightKg = request.MaxWeightKg;
        route.MaxVolumeM3 = request.MaxVolumeM3;
        route.EstimatedDistanceKm = request.EstimatedDistanceKm;
        route.EstimatedDurationMinutes = request.EstimatedDurationMinutes;

        // Thay toàn bộ danh sách stops
        context.RouteStops.RemoveRange(route.Stops);
        route.Stops.Clear();
        foreach (var stopInput in request.Stops)
        {
            route.Stops.Add(new RouteStop
            {
                RouteId = route.Id,
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

        // Dữ liệu thay đổi → risk cần đánh giá lại, optimization cũ hết hiệu lực
        route.RiskLevel = Shared.Enums.RouteRiskLevel.Low;
        route.OptimizedAt = null;
        route.Version++;

        outbox.Enqueue(new RouteUpdatedEvent
        {
            RouteId = route.Id,
            TenantId = tenantId,
            RouteName = route.Name,
            Version = route.Version,
            UpdatedByUserId = currentUser.UserId ?? Guid.Empty
        });

        await context.SaveChangesAsync(cancellationToken);

        return RouteMapper.ToDto(route);
    }
}

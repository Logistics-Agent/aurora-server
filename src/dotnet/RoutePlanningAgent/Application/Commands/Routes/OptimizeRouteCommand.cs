using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Application.DTOs.Routes;
using RoutePlanningAgent.Application.Interfaces;
using RoutePlanningAgent.Application.Mapping;
using RoutePlanningAgent.Domain;
using RoutePlanningAgent.Domain.Enums;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Events;
using Shared.Exceptions;
using Shared.Security;

namespace RoutePlanningAgent.Application.Commands.Routes;

/// <summary>
/// Tối ưu thứ tự điểm dừng bằng VROOM + OSRM (MLD):
/// reorder Sequence, cập nhật ETA từng stop, tổng distance/duration, OptimizedAt, Version++.
/// </summary>
public record OptimizeRouteCommand(Guid RouteId) : IRequest<RouteDto>;

public class OptimizeRouteHandler(
    RoutePlanningDbContext context,
    ICurrentUserService currentUser,
    IRouteOptimizationService optimizationService,
    IOutboxWriter outbox)
    : IRequestHandler<OptimizeRouteCommand, RouteDto>
{
    public async Task<RouteDto> Handle(OptimizeRouteCommand request, CancellationToken cancellationToken)
    {
        var tenantId = currentUser.TenantId
            ?? throw new ForbiddenException("Tenant context is missing");

        var route = await context.Routes
            .Include(r => r.Stops)
            .FirstOrDefaultAsync(r => r.Id == request.RouteId, cancellationToken)
            ?? throw new NotFoundException($"Route '{request.RouteId}' not found");

        if (route.Status is not (RouteStatus.Draft or RouteStatus.Ready))
            throw new ConflictException(
                $"Route đang ở trạng thái {route.Status} — chỉ Draft hoặc Ready mới được tối ưu");

        // Gọi solver (VROOM + OSRM) — lỗi solver → DomainException, không đổi trạng thái route
        var result = await optimizationService.OptimizeAsync(route, cancellationToken);

        // Áp kết quả: reorder + ETA
        var stopById = route.Stops.ToDictionary(s => s.Id);
        foreach (var optimized in result.Stops)
        {
            if (stopById.TryGetValue(optimized.StopId, out var stop))
            {
                stop.Sequence = optimized.Sequence;
                stop.EstimatedArrivalMinutes = optimized.EstimatedArrivalMinutes;
            }
        }

        route.EstimatedDistanceKm = result.TotalDistanceKm;
        route.EstimatedDurationMinutes = result.TotalDurationMinutes;
        route.OptimizedAt = DateTime.UtcNow;
        route.Status = RouteStatus.Ready;
        route.Version++;

        context.OptimizationHistories.Add(new RouteOptimizationHistory
        {
            RouteId = route.Id,
            Provider = result.Provider,
            Model = result.Model,
            PromptVersion = "n/a", // solver — không dùng prompt LLM
            TotalDistanceKm = result.TotalDistanceKm,
            TotalDurationMinutes = result.TotalDurationMinutes,
            InputTokens = 0,
            OutputTokens = 0
        });

        outbox.Enqueue(new RouteOptimizedEvent
        {
            RouteId = route.Id,
            TenantId = tenantId,
            Provider = result.Provider,
            Model = result.Model,
            TotalDistanceKm = result.TotalDistanceKm,
            TotalDurationMinutes = result.TotalDurationMinutes
        });

        await context.SaveChangesAsync(cancellationToken);

        return RouteMapper.ToDto(route);
    }
}

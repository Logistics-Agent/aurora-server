using GpsTracking.Domain.Entities;
using GpsTracking.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shipment.Contracts.Events;

namespace GpsTracking.Application.Shipments;

public interface IShipmentAssignmentProjector
{
    Task ProjectAsync(RouteAssignedEvent message, CancellationToken cancellationToken = default);
    Task ProjectAsync(ShipmentCancelledEvent message, CancellationToken cancellationToken = default);
    Task ProjectAsync(ShipmentCompletedEvent message, CancellationToken cancellationToken = default);
}

public sealed class ShipmentAssignmentProjector(
    GpsTrackingDbContext dbContext,
    TimeProvider timeProvider) : IShipmentAssignmentProjector
{
    public async Task ProjectAsync(
        RouteAssignedEvent message,
        CancellationToken cancellationToken = default)
    {
        Validate(message.EventId, message.TenantId, message.ShipmentId, message.ContractVersion);
        if (string.IsNullOrWhiteSpace(message.VehicleId)
            || string.IsNullOrWhiteSpace(message.RouteId)
            || message.AssignedAt == default)
        {
            throw new DomainException("Route assignment requires vehicle, route, and assigned time.");
        }
        if (await IsConsumed(nameof(RouteAssignedEvent), message.EventId, cancellationToken))
            return;

        var state = await dbContext.ShipmentTrackingStates
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.TenantId == message.TenantId
                && item.ShipmentId == message.ShipmentId, cancellationToken);
        var shouldProject = state is null;
        if (state is null)
        {
            state = ShipmentTrackingState.Create(
                message.TenantId, message.ShipmentId, message.AssignedAt, false);
            dbContext.ShipmentTrackingStates.Add(state);
        }
        else
        {
            shouldProject = state.ObserveAssignment(message.AssignedAt);
        }

        if (shouldProject)
        {
            var vehicleId = message.VehicleId.Trim();
            var routeId = message.RouteId.Trim();
            var active = await dbContext.VehicleShipmentAssignments
                .IgnoreQueryFilters()
                .Where(item => item.TenantId == message.TenantId
                    && item.EndedAt == null
                    && (item.VehicleId == vehicleId || item.ShipmentId == message.ShipmentId))
                .ToListAsync(cancellationToken);
            foreach (var assignment in active)
            {
                if (assignment.AssignedAt <= message.AssignedAt)
                    assignment.End(message.AssignedAt);
            }

            var exactExists = active.Any(item => item.EndedAt == null
                && item.VehicleId == vehicleId
                && item.ShipmentId == message.ShipmentId
                && item.RouteId == routeId);
            if (!exactExists)
            {
                dbContext.VehicleShipmentAssignments.Add(VehicleShipmentAssignment.Create(
                    message.TenantId,
                    message.ShipmentId,
                    routeId,
                    vehicleId,
                    message.AssignedAt));
            }
        }

        AddReceipt(message.TenantId, message.EventId, nameof(RouteAssignedEvent), message.ContractVersion);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task ProjectAsync(
        ShipmentCancelledEvent message,
        CancellationToken cancellationToken = default) =>
        ProjectTerminalAsync(
            message.EventId,
            message.TenantId,
            message.ShipmentId,
            message.ContractVersion,
            message.CancelledAt,
            nameof(ShipmentCancelledEvent),
            cancellationToken);

    public Task ProjectAsync(
        ShipmentCompletedEvent message,
        CancellationToken cancellationToken = default) =>
        ProjectTerminalAsync(
            message.EventId,
            message.TenantId,
            message.ShipmentId,
            message.ContractVersion,
            message.CompletedAt,
            nameof(ShipmentCompletedEvent),
            cancellationToken);

    private async Task ProjectTerminalAsync(
        Guid eventId,
        Guid tenantId,
        Guid shipmentId,
        int contractVersion,
        DateTimeOffset occurredAt,
        string eventType,
        CancellationToken cancellationToken)
    {
        Validate(eventId, tenantId, shipmentId, contractVersion);
        if (occurredAt == default)
            throw new DomainException("Terminal shipment event time is required.");
        if (await IsConsumed(eventType, eventId, cancellationToken))
            return;

        var state = await dbContext.ShipmentTrackingStates
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.TenantId == tenantId
                && item.ShipmentId == shipmentId, cancellationToken);
        if (state is null)
        {
            state = ShipmentTrackingState.Create(tenantId, shipmentId, occurredAt, true);
            dbContext.ShipmentTrackingStates.Add(state);
        }
        else
        {
            state.Close(occurredAt);
        }

        var active = await dbContext.VehicleShipmentAssignments
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId
                && item.ShipmentId == shipmentId
                && item.EndedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var assignment in active)
            assignment.End(occurredAt < assignment.AssignedAt ? assignment.AssignedAt : occurredAt);

        AddReceipt(tenantId, eventId, eventType, contractVersion);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task<bool> IsConsumed(
        string eventType,
        Guid eventId,
        CancellationToken cancellationToken) =>
        dbContext.ConsumedIntegrationEvents
            .IgnoreQueryFilters()
            .AnyAsync(item => item.SourceEventType == eventType
                && item.SourceEventId == eventId, cancellationToken);

    private void AddReceipt(Guid tenantId, Guid eventId, string eventType, int contractVersion) =>
        dbContext.ConsumedIntegrationEvents.Add(ConsumedIntegrationEvent.Create(
            tenantId,
            eventId,
            eventType,
            contractVersion,
            timeProvider.GetUtcNow()));

    private static void Validate(
        Guid eventId,
        Guid tenantId,
        Guid shipmentId,
        int contractVersion)
    {
        if (eventId == Guid.Empty || tenantId == Guid.Empty || shipmentId == Guid.Empty)
            throw new DomainException("Shipment event identity is invalid.");
        if (contractVersion <= 0)
            throw new DomainException("Shipment event contract version is invalid.");
    }
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Shared.Exceptions;
using Shared.Security;
using Shipment.Contracts.Events;
using ShipmentWorkflow.Domain.Entities;
using ShipmentWorkflow.Domain.Enums;
using ShipmentWorkflow.Infrastructure.Persistences;
using ShipmentEntity = global::ShipmentWorkflow.Domain.Entities.Shipment;

namespace ShipmentWorkflow.Application.Commands.Shipments;

internal static class ShipmentCommandHelpers
{
    internal static Guid RequireTenantId(ICurrentUserService currentUser)
    {
        return currentUser.TenantId
            ?? throw new DomainException("TenantId was not found in the authenticated user context.");
    }

    internal static async Task<ShipmentEntity> GetShipmentAsync(
        ShipmentWorkflowDbContext dbContext,
        Guid shipmentId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Shipments
            .Include(shipment => shipment.CargoItems)
            .Include(shipment => shipment.Locations)
            .Include(shipment => shipment.Documents)
            .Include(shipment => shipment.Milestones)
            .Include(shipment => shipment.StatusHistories)
            .SingleOrDefaultAsync(shipment => shipment.Id == shipmentId, cancellationToken)
            ?? throw new NotFoundException("Shipment was not found.");
    }

    internal static void AddStatusChangedOutbox(
        ShipmentWorkflowDbContext dbContext,
        ShipmentEntity shipment,
        ShipmentStatus oldStatus,
        string? note)
    {
        var changedAt = DateTimeOffset.UtcNow;
        var integrationEvent = new ShipmentStatusChangedEvent
        {
            ShipmentId = shipment.Id,
            TenantId = shipment.TenantId,
            OldStatus = oldStatus.ToString(),
            NewStatus = shipment.Status.ToString(),
            Note = note,
            ChangedAt = changedAt
        };

        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            EventType = nameof(ShipmentStatusChangedEvent),
            Payload = JsonSerializer.Serialize(integrationEvent),
            CreatedAt = changedAt
        });
    }

    internal static async Task PersistLifecycleStateAsync(
        ShipmentWorkflowDbContext dbContext,
        ShipmentEntity shipment,
        ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var actor = currentUser.UserId?.ToString() ?? "system";

        await dbContext.Shipments
            .IgnoreQueryFilters()
            .Where(existing =>
                existing.Id == shipment.Id &&
                existing.TenantId == shipment.TenantId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(existing => existing.Status, shipment.Status)
                .SetProperty(existing => existing.ActualPickupTime, shipment.ActualPickupTime)
                .SetProperty(existing => existing.ActualDeliveryTime, shipment.ActualDeliveryTime)
                .SetProperty(existing => existing.Notes, shipment.Notes)
                .SetProperty(existing => existing.UpdatedAt, now)
                .SetProperty(existing => existing.UpdatedBy, actor), cancellationToken);

        dbContext.Entry(shipment).State = EntityState.Detached;
    }

    internal static void AddCancelledOutbox(
        ShipmentWorkflowDbContext dbContext,
        ShipmentEntity shipment,
        string reason)
    {
        var cancelledAt = DateTimeOffset.UtcNow;
        var integrationEvent = new ShipmentCancelledEvent
        {
            ShipmentId = shipment.Id,
            TenantId = shipment.TenantId,
            Reason = reason,
            CancelledAt = cancelledAt
        };

        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            EventType = nameof(ShipmentCancelledEvent),
            Payload = JsonSerializer.Serialize(integrationEvent),
            CreatedAt = cancelledAt
        });
    }

    internal static void EnsurePreOperationalMutation(ShipmentEntity shipment)
    {
        if (shipment.Status is not (ShipmentStatus.Draft or ShipmentStatus.Created or ShipmentStatus.Submitted))
        {
            throw new DomainException("Shipment can only be changed before operational processing starts.");
        }
    }

    internal static void MarkAggregateRootUnchanged(
        ShipmentWorkflowDbContext dbContext,
        ShipmentEntity shipment)
    {
        dbContext.Entry(shipment).State = EntityState.Unchanged;
    }

    internal static void AddCargoUpdatedOutbox(
        ShipmentWorkflowDbContext dbContext,
        ShipmentEntity shipment,
        Guid cargoItemId,
        string action)
    {
        var updatedAt = DateTimeOffset.UtcNow;
        var integrationEvent = new CargoUpdatedEvent
        {
            ShipmentId = shipment.Id,
            TenantId = shipment.TenantId,
            CargoItemId = cargoItemId,
            Action = action,
            UpdatedAt = updatedAt
        };

        dbContext.OutboxMessages.Add(new OutboxMessage
        {
            EventType = nameof(CargoUpdatedEvent),
            Payload = JsonSerializer.Serialize(integrationEvent),
            CreatedAt = updatedAt
        });
    }

    internal static void ApplyStatusTransition(
        ShipmentEntity shipment,
        ShipmentStatus requestedStatus,
        Guid? actorId,
        string? note = null)
    {
        switch (requestedStatus)
        {
            case ShipmentStatus.Submitted:
                shipment.Submit(actorId);
                break;
            case ShipmentStatus.Planning:
                shipment.StartPlanning(actorId);
                break;
            case ShipmentStatus.Negotiating:
                shipment.StartNegotiation(actorId);
                break;
            case ShipmentStatus.Confirmed:
                shipment.Confirm(actorId);
                break;
            case ShipmentStatus.PickedUp:
                shipment.MarkPickedUp(actorId);
                break;
            case ShipmentStatus.InTransit:
                shipment.MarkInTransit(actorId);
                break;
            case ShipmentStatus.CustomsProcessing:
                shipment.StartCustomsProcessing(actorId);
                break;
            case ShipmentStatus.Delivered:
                shipment.MarkDelivered(actorId);
                break;
            case ShipmentStatus.Completed:
                shipment.Complete(actorId);
                break;
            default:
                throw new DomainException($"Unsupported shipment status transition: {requestedStatus}.");
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            shipment.Notes = string.IsNullOrWhiteSpace(shipment.Notes)
                ? note.Trim()
                : $"{shipment.Notes.Trim()}\n{note.Trim()}";
        }
    }
}

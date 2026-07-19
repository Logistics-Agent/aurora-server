using Notification.Application.Services;
using Notification.Domain.Enums;
using Shipment.Contracts.Events;

namespace Notification.Tests.Application;

public sealed class ShipmentEventNotificationFactoryTests
{
    [Fact]
    public void MapsShipmentCreatedEvent()
    {
        var message = new ShipmentCreatedEvent
        {
            EventId = Guid.CreateVersion7(),
            TenantId = Guid.CreateVersion7(),
            ShipmentId = Guid.CreateVersion7(),
            ShipmentNumber = "SHP-1001",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = ShipmentEventNotificationFactory.Create(message);

        Assert.Equal(NotificationEventType.ShipmentCreated, result.EventType);
        Assert.Equal(nameof(ShipmentCreatedEvent), result.SourceEventType);
        Assert.Contains("SHP-1001", result.Body);
    }

    [Fact]
    public void MapsShipmentStatusChangedEvent()
    {
        var message = new ShipmentStatusChangedEvent
        {
            EventId = Guid.CreateVersion7(),
            TenantId = Guid.CreateVersion7(),
            ShipmentId = Guid.CreateVersion7(),
            OldStatus = "Draft",
            NewStatus = "Submitted",
            Note = "Ready for planning",
            ChangedAt = DateTimeOffset.UtcNow
        };

        var result = ShipmentEventNotificationFactory.Create(message);

        Assert.Equal(NotificationEventType.ShipmentStatusChanged, result.EventType);
        Assert.Contains("Draft to Submitted", result.Body);
        Assert.Contains("Ready for planning", result.Body);
    }

    [Fact]
    public void MapsShipmentCancelledEvent()
    {
        var message = new ShipmentCancelledEvent
        {
            EventId = Guid.CreateVersion7(),
            TenantId = Guid.CreateVersion7(),
            ShipmentId = Guid.CreateVersion7(),
            Reason = "Customer request",
            CancelledAt = DateTimeOffset.UtcNow
        };

        var result = ShipmentEventNotificationFactory.Create(message);

        Assert.Equal(NotificationEventType.ShipmentCancelled, result.EventType);
        Assert.Contains("Customer request", result.Body);
    }
}

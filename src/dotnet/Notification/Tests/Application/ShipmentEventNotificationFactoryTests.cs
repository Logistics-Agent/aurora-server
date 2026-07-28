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

    [Theory]
    [InlineData("Submitted")]
    [InlineData("PickedUp")]
    [InlineData("Delivered")]
    [InlineData("Completed")]
    public void MapsShipmentLifecycleEvents(string lifecycle)
    {
        var eventId = Guid.CreateVersion7();
        var tenantId = Guid.CreateVersion7();
        var shipmentId = Guid.CreateVersion7();
        var occurredAt = DateTimeOffset.UtcNow;

        var result = lifecycle switch
        {
            "Submitted" => ShipmentEventNotificationFactory.Create(new ShipmentSubmittedEvent
            {
                EventId = eventId,
                TenantId = tenantId,
                ShipmentId = shipmentId,
                ShipmentNumber = "SHP-2001",
                CurrentStatus = lifecycle,
                SubmittedAt = occurredAt
            }),
            "PickedUp" => ShipmentEventNotificationFactory.Create(new ShipmentPickedUpEvent
            {
                EventId = eventId,
                TenantId = tenantId,
                ShipmentId = shipmentId,
                ShipmentNumber = "SHP-2001",
                CurrentStatus = lifecycle,
                PickedUpAt = occurredAt
            }),
            "Delivered" => ShipmentEventNotificationFactory.Create(new ShipmentDeliveredEvent
            {
                EventId = eventId,
                TenantId = tenantId,
                ShipmentId = shipmentId,
                ShipmentNumber = "SHP-2001",
                CurrentStatus = lifecycle,
                DeliveredAt = occurredAt
            }),
            "Completed" => ShipmentEventNotificationFactory.Create(new ShipmentCompletedEvent
            {
                EventId = eventId,
                TenantId = tenantId,
                ShipmentId = shipmentId,
                ShipmentNumber = "SHP-2001",
                CurrentStatus = lifecycle,
                CompletedAt = occurredAt
            }),
            _ => throw new InvalidOperationException()
        };

        Assert.Equal(Enum.Parse<NotificationEventType>($"Shipment{lifecycle}"), result.EventType);
        Assert.Equal(eventId, result.EventId);
        Assert.Equal(tenantId, result.TenantId);
        Assert.Equal(shipmentId, result.ShipmentId);
        Assert.Contains("SHP-2001", result.Body);
    }

    [Fact]
    public void MapsDocumentAttachedEvent()
    {
        var message = new DocumentAttachedEvent
        {
            EventId = Guid.CreateVersion7(),
            TenantId = Guid.CreateVersion7(),
            ShipmentId = Guid.CreateVersion7(),
            DocumentId = Guid.CreateVersion7(),
            DocumentType = "BillOfLading",
            FileName = "bol.pdf",
            AttachedAt = DateTimeOffset.UtcNow
        };

        var result = ShipmentEventNotificationFactory.Create(message);

        Assert.Equal(NotificationEventType.DocumentAttached, result.EventType);
        Assert.Contains("bol.pdf", result.Body);
        Assert.Contains("BillOfLading", result.Body);
    }
}

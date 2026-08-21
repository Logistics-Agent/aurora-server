using System.Text.Json;
using Shipment.Contracts.Events;

namespace ShipmentWorkflow.Infrastructure.BackgroundJobs;

public static class ShipmentIntegrationEventTypeRegistry
{
    private static readonly IReadOnlyDictionary<string, Type> EventTypes =
        new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            [nameof(ShipmentCreatedEvent)] = typeof(ShipmentCreatedEvent),
            [nameof(ShipmentSubmittedEvent)] = typeof(ShipmentSubmittedEvent),
            [nameof(ShipmentUpdatedEvent)] = typeof(ShipmentUpdatedEvent),
            [nameof(ShipmentCancelledEvent)] = typeof(ShipmentCancelledEvent),
            [nameof(ShipmentStatusChangedEvent)] = typeof(ShipmentStatusChangedEvent),
            [nameof(CargoUpdatedEvent)] = typeof(CargoUpdatedEvent),
            [nameof(DocumentAttachedEvent)] = typeof(DocumentAttachedEvent),
            [nameof(RouteAssignedEvent)] = typeof(RouteAssignedEvent),
            [nameof(ShipmentPickedUpEvent)] = typeof(ShipmentPickedUpEvent),
            [nameof(ShipmentDeliveredEvent)] = typeof(ShipmentDeliveredEvent),
            [nameof(ShipmentCompletedEvent)] = typeof(ShipmentCompletedEvent)
        };

    public static bool TryResolve(string eventType, out Type? resolvedType) =>
        EventTypes.TryGetValue(eventType, out resolvedType);

    public static object Deserialize(string eventType, string payload)
    {
        if (!TryResolve(eventType, out var resolvedType) || resolvedType is null)
        {
            throw new InvalidOperationException(
                $"Unsupported Shipment outbox event type '{eventType}'.");
        }

        return JsonSerializer.Deserialize(payload, resolvedType)
            ?? throw new JsonException(
                $"Shipment outbox event '{eventType}' deserialized to null.");
    }
}

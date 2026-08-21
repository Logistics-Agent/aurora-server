using System.Text.Json;
using GpsTracking.Contracts.Events;

namespace GpsTracking.Infrastructure.BackgroundJobs;

public static class GpsIntegrationEventTypeRegistry
{
    private static readonly IReadOnlyDictionary<string, Type> EventTypes =
        new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            [nameof(GpsPositionUpdatedEvent)] = typeof(GpsPositionUpdatedEvent),
            [nameof(GpsMonitoringAlertRaisedEvent)] = typeof(GpsMonitoringAlertRaisedEvent)
        };

    public static bool TryResolve(string eventType, out Type? resolvedType) =>
        EventTypes.TryGetValue(eventType, out resolvedType);

    public static object Deserialize(string eventType, string content)
    {
        if (!TryResolve(eventType, out var resolvedType) || resolvedType is null)
            throw new InvalidOperationException($"Unsupported GPS outbox event type '{eventType}'.");

        return JsonSerializer.Deserialize(content, resolvedType)
            ?? throw new JsonException($"GPS outbox event '{eventType}' deserialized to null.");
    }
}

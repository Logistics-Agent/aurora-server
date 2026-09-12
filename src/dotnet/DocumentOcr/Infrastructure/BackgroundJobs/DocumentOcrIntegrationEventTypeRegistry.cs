using System.Text.Json;
using DocumentOcr.Contracts.Events;

namespace DocumentOcr.Infrastructure.BackgroundJobs;

public static class DocumentOcrIntegrationEventTypeRegistry
{
    private static readonly IReadOnlyDictionary<string, Type> EventTypes =
        new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            [nameof(DocumentOcrCompletedEvent)] = typeof(DocumentOcrCompletedEvent),
            [nameof(DocumentOcrFailedEvent)] = typeof(DocumentOcrFailedEvent),
            [nameof(DocumentOcrRequiresReviewEvent)] = typeof(DocumentOcrRequiresReviewEvent)
        };

    public static bool TryResolve(string eventType, out Type? resolvedType) =>
        EventTypes.TryGetValue(eventType, out resolvedType);

    public static object Deserialize(string eventType, string content)
    {
        if (!TryResolve(eventType, out var resolvedType) || resolvedType is null)
            throw new InvalidOperationException($"Unsupported Document OCR outbox event type '{eventType}'.");

        var value = JsonSerializer.Deserialize(content, resolvedType)
            ?? throw new JsonException($"Document OCR outbox event '{eventType}' deserialized to null.");
        switch (value)
        {
            case DocumentOcrCompletedEvent completed:
                DocumentOcrEventContract.ValidateVersion(eventType, completed.ContractVersion);
                break;
            case DocumentOcrFailedEvent failed:
                DocumentOcrEventContract.ValidateVersion(eventType, failed.ContractVersion);
                break;
            case DocumentOcrRequiresReviewEvent requiresReview:
                DocumentOcrEventContract.ValidateVersion(eventType, requiresReview.ContractVersion);
                break;
        }

        return value;
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace DocumentOcr.Contracts.Events;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DocumentOcrPurpose
{
    Unspecified = 0,
    ShipmentDocument = 1,
    RegulatoryCorpus = 2,
    KnowledgeCorpus = 3
}

public static class DocumentOcrCorrelationId
{
    public static Guid FromTrace(string? traceOrCorrelationId)
    {
        if (Guid.TryParse(traceOrCorrelationId, out var parsed) && parsed != Guid.Empty)
            return parsed;

        if (string.IsNullOrWhiteSpace(traceOrCorrelationId))
            return Guid.CreateVersion7();

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(traceOrCorrelationId.Trim()));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash.AsSpan(0, 16));
    }
}

public static class DocumentOcrEventContract
{
    public static void ValidateVersion(string eventType, int contractVersion)
    {
        var supported = eventType switch
        {
            nameof(DocumentOcrCompletedEvent) => 2,
            nameof(DocumentOcrFailedEvent) => 2,
            nameof(DocumentOcrRequiresReviewEvent) => 1,
            _ => throw new NotSupportedException($"Unsupported Document OCR event type '{eventType}'.")
        };

        if (contractVersion != supported)
        {
            throw new NotSupportedException(
                $"Unsupported {eventType} contract version {contractVersion}; expected {supported}.");
        }
    }

    public static Guid ParseResourceId(string? externalContextId, string eventType)
    {
        if (!Guid.TryParse(externalContextId, out var resourceId) || resourceId == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"{eventType} requires a GUID external context resource id.");
        }

        return resourceId;
    }
}

using System.Text.Json;
using DocumentOcr.Contracts.Events;
using DocumentOcr.Domain.Entities;

namespace DocumentOcr.Application.Jobs;

public static class DocumentOcrOutboxFactory
{
    public static OutboxMessage CreateFailed(DocumentOcrJob job, DateTimeOffset occurredAt)
    {
        var integrationEvent = new DocumentOcrFailedEvent
        {
            TenantId = job.TenantId,
            JobId = job.Id,
            ExternalDocumentId = job.ExternalDocumentId,
            ExternalShipmentId = job.ExternalShipmentId,
            ExternalContextId = job.ExternalContextId,
            ErrorCode = job.ErrorCode ?? "document_processing_failed",
            ErrorMessage = job.ErrorMessage ?? "Document processing failed.",
            OccurredAt = occurredAt
        };
        return OutboxMessage.Create(
            job.TenantId,
            integrationEvent.EventId,
            nameof(DocumentOcrFailedEvent),
            JsonSerializer.Serialize(integrationEvent),
            occurredAt);
    }
}

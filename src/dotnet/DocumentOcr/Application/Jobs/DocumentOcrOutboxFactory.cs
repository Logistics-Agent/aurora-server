using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DocumentOcr.Contracts.Events;
using DocumentOcr.Domain.Entities;

namespace DocumentOcr.Application.Jobs;

public sealed record DocumentOcrEventInput(
    Guid TenantId,
    Guid? ShipmentId,
    Guid DocumentId,
    Guid JobId,
    Guid CorrelationId,
    DocumentOcrPurpose Purpose = DocumentOcrPurpose.ShipmentDocument,
    string? ExternalContextId = null);

public static class DocumentOcrOutboxFactory
{
    public static OutboxMessage CreateFailed(DocumentOcrJob job, DateTimeOffset occurredAt) =>
        Failed(Input(job), occurredAt, job.ErrorCode ?? "document_processing_failed", job.ErrorMessage ?? "Document processing failed.", job.Confidence ?? 0m, job.NormalizedJson, job.ArtifactReference);

    public static OutboxMessage CreateCompleted(DocumentOcrJob job, DateTimeOffset occurredAt) =>
        Completed(Input(job), occurredAt, job);

    public static OutboxMessage CreateRequiresReview(DocumentOcrJob job, DateTimeOffset occurredAt) =>
        RequiresReview(Input(job), occurredAt, job);

    public static OutboxMessage Completed(DocumentOcrEventInput input, DateTimeOffset? occurredAt = null) =>
        Completed(input, occurredAt ?? DateTimeOffset.UtcNow, input.DocumentId, input.ShipmentId, input.ExternalContextId, string.Empty, "{}", null, string.Empty, 0m, false);

    public static OutboxMessage Failed(DocumentOcrEventInput input, DateTimeOffset? occurredAt = null) =>
        Failed(input, occurredAt ?? DateTimeOffset.UtcNow, "document_processing_failed", "Document processing failed.", 0m, null, null);

    public static OutboxMessage RequiresReview(DocumentOcrEventInput input, DateTimeOffset? occurredAt = null) =>
        RequiresReview(input, occurredAt ?? DateTimeOffset.UtcNow, input.DocumentId, input.ShipmentId, input.ExternalContextId, string.Empty, "{}", null, 0m);

    private static OutboxMessage Completed(DocumentOcrEventInput input, DateTimeOffset occurredAt, DocumentOcrJob job) =>
        Completed(input, occurredAt, job.ExternalDocumentId, job.ExternalShipmentId, input.ExternalContextId, job.DetectedDocumentType?.ToString() ?? string.Empty, job.NormalizedJson ?? "{}", job.ArtifactReference, job.ExtractionMode.ToString(), job.Confidence ?? 0m, job.NeedsReview ?? false);

    private static OutboxMessage Completed(
        DocumentOcrEventInput input,
        DateTimeOffset occurredAt,
        Guid externalDocumentId,
        Guid? externalShipmentId,
        string? externalContextId,
        string detectedDocumentType,
        string normalizedJson,
        string? artifactReference,
        string extractionMode,
        decimal confidence,
        bool needsReview)
    {
        var integrationEvent = new DocumentOcrCompletedEvent
        {
            TenantId = input.TenantId,
            JobId = input.JobId,
            ShipmentId = input.ShipmentId,
            DocumentId = input.DocumentId,
            CorrelationId = input.CorrelationId,
            Purpose = input.Purpose,
            ExternalDocumentId = externalDocumentId,
            ExternalShipmentId = externalShipmentId,
            ExternalContextId = externalContextId,
            DetectedDocumentType = detectedDocumentType,
            NormalizedJson = normalizedJson,
            NormalizedJsonHash = Hash(normalizedJson),
            ArtifactReference = artifactReference,
            ExtractionMode = extractionMode,
            Confidence = confidence,
            NeedsReview = needsReview,
            ReviewState = "COMPLETED",
            OccurredAt = occurredAt
        };
        return Serialize(input.TenantId, integrationEvent.EventId, nameof(DocumentOcrCompletedEvent), integrationEvent, occurredAt);
    }

    private static OutboxMessage Failed(DocumentOcrEventInput input, DateTimeOffset occurredAt, string errorCode, string errorMessage, decimal confidence, string? normalizedJson, string? artifactReference)
    {
        var integrationEvent = new DocumentOcrFailedEvent
        {
            TenantId = input.TenantId,
            JobId = input.JobId,
            ShipmentId = input.ShipmentId,
            DocumentId = input.DocumentId,
            CorrelationId = input.CorrelationId,
            Purpose = input.Purpose,
            ExternalDocumentId = input.DocumentId,
            ExternalShipmentId = input.ShipmentId,
            ExternalContextId = input.ExternalContextId,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            Confidence = confidence,
            NormalizedJsonHash = string.IsNullOrWhiteSpace(normalizedJson) ? null : Hash(normalizedJson),
            ArtifactReference = artifactReference,
            OccurredAt = occurredAt
        };
        return Serialize(input.TenantId, integrationEvent.EventId, nameof(DocumentOcrFailedEvent), integrationEvent, occurredAt);
    }

    private static OutboxMessage RequiresReview(DocumentOcrEventInput input, DateTimeOffset occurredAt, DocumentOcrJob job) =>
        RequiresReview(input, occurredAt, job.ExternalDocumentId, job.ExternalShipmentId, input.ExternalContextId, job.DetectedDocumentType?.ToString() ?? string.Empty, job.NormalizedJson ?? "{}", job.ArtifactReference, job.Confidence ?? 0m);

    private static OutboxMessage RequiresReview(
        DocumentOcrEventInput input,
        DateTimeOffset occurredAt,
        Guid externalDocumentId,
        Guid? externalShipmentId,
        string? externalContextId,
        string detectedDocumentType,
        string normalizedJson,
        string? artifactReference,
        decimal confidence)
    {
        var integrationEvent = new DocumentOcrRequiresReviewEvent
        {
            TenantId = input.TenantId,
            JobId = input.JobId,
            ShipmentId = input.ShipmentId,
            DocumentId = input.DocumentId,
            CorrelationId = input.CorrelationId,
            Purpose = input.Purpose,
            ExternalDocumentId = externalDocumentId,
            ExternalShipmentId = externalShipmentId,
            ExternalContextId = externalContextId,
            DetectedDocumentType = detectedDocumentType,
            NormalizedJsonHash = Hash(normalizedJson),
            ArtifactReference = artifactReference,
            Confidence = confidence,
            OccurredAt = occurredAt
        };
        return Serialize(input.TenantId, integrationEvent.EventId, nameof(DocumentOcrRequiresReviewEvent), integrationEvent, occurredAt);
    }

    private static DocumentOcrEventInput Input(DocumentOcrJob job) => new(
        job.TenantId,
        job.ExternalShipmentId,
        job.ExternalDocumentId,
        job.Id,
        job.InitiatingCorrelationId,
        job.Purpose,
        job.ExternalContextId);

    private static OutboxMessage Serialize<T>(Guid tenantId, Guid eventId, string eventType, T value, DateTimeOffset occurredAt) =>
        OutboxMessage.Create(tenantId, eventId, eventType, JsonSerializer.Serialize(value), occurredAt);

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

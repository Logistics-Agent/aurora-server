using DocumentOcr.Domain.Enums;
using Shared.Entity;

namespace DocumentOcr.Domain.Entities;

public sealed class DocumentOcrJob : TenantAuditableEntity
{
    private readonly List<OcrProviderAttempt> _attempts = [];

    private DocumentOcrJob() { }

    public string IdempotencyKey { get; private set; } = string.Empty;
    public string StorageReference { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string MimeType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public Guid ExternalDocumentId { get; private set; }
    public Guid? ExternalShipmentId { get; private set; }
    public OcrDocumentType DocumentTypeHint { get; private set; }
    public OcrDocumentType? DetectedDocumentType { get; private set; }
    public DocumentOcrJobStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public string? NormalizedJson { get; private set; }
    public string? FieldConfidenceJson { get; private set; }
    public decimal? Confidence { get; private set; }
    public bool? NeedsReview { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset? ProcessingStartedAt { get; private set; }
    public DateTimeOffset? LeaseExpiresAt { get; private set; }
    public DateTimeOffset? HeartbeatAt { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? FailedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public IReadOnlyCollection<OcrProviderAttempt> Attempts => _attempts.AsReadOnly();

    public static DocumentOcrJob Create(
        Guid tenantId,
        string idempotencyKey,
        string storageReference,
        string fileName,
        string mimeType,
        long sizeBytes,
        OcrDocumentType documentTypeHint,
        Guid externalDocumentId,
        Guid? externalShipmentId,
        DateTimeOffset createdAt)
    {
        DocumentOcrValidation.RequiredId(tenantId, nameof(tenantId));
        DocumentOcrValidation.RequiredId(externalDocumentId, nameof(externalDocumentId));
        DocumentOcrValidation.OptionalId(externalShipmentId, nameof(externalShipmentId));
        if (sizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "SizeBytes must be positive.");
        if (!Enum.IsDefined(documentTypeHint))
            throw new ArgumentOutOfRangeException(nameof(documentTypeHint));
        if (createdAt == default)
            throw new ArgumentException("CreatedAt is required.", nameof(createdAt));

        return new DocumentOcrJob
        {
            TenantId = tenantId,
            IdempotencyKey = DocumentOcrValidation.RequiredText(idempotencyKey, nameof(idempotencyKey), 150),
            StorageReference = DocumentOcrValidation.RequiredText(storageReference, nameof(storageReference), 1_000),
            FileName = DocumentOcrValidation.RequiredText(fileName, nameof(fileName), 255),
            MimeType = DocumentOcrValidation.RequiredText(mimeType, nameof(mimeType), 150).ToLowerInvariant(),
            SizeBytes = sizeBytes,
            DocumentTypeHint = documentTypeHint,
            ExternalDocumentId = externalDocumentId,
            ExternalShipmentId = externalShipmentId,
            Status = DocumentOcrJobStatus.Queued,
            CreatedAt = createdAt
        };
    }

    public OcrProviderAttempt Start(
        string providerName,
        DateTimeOffset startedAt,
        DateTimeOffset leaseExpiresAt)
    {
        EnsureStatus(DocumentOcrJobStatus.Queued);
        if (startedAt == default)
            throw new ArgumentException("StartedAt is required.", nameof(startedAt));
        if (leaseExpiresAt <= startedAt)
            throw new ArgumentOutOfRangeException(nameof(leaseExpiresAt), "Lease must expire after processing starts.");
        if (NextAttemptAt > startedAt)
            throw new InvalidOperationException("The job is not ready for another attempt.");

        var attempt = OcrProviderAttempt.Start(TenantId, Id, providerName, startedAt);
        _attempts.Add(attempt);
        Status = DocumentOcrJobStatus.Processing;
        AttemptCount++;
        ProcessingStartedAt = startedAt;
        LeaseExpiresAt = leaseExpiresAt;
        HeartbeatAt = startedAt;
        NextAttemptAt = null;
        UpdatedAt = startedAt;
        return attempt;
    }

    public void Complete(
        Guid attemptId,
        OcrDocumentType detectedDocumentType,
        string normalizedJson,
        string? fieldConfidenceJson,
        decimal confidence,
        bool needsReview,
        string? providerRequestId,
        DateTimeOffset completedAt)
    {
        EnsureStatus(DocumentOcrJobStatus.Processing);
        if (detectedDocumentType == OcrDocumentType.Unspecified || !Enum.IsDefined(detectedDocumentType))
            throw new ArgumentOutOfRangeException(nameof(detectedDocumentType));
        if (completedAt == default)
            throw new ArgumentException("CompletedAt is required.", nameof(completedAt));

        var attempt = GetProcessingAttempt(attemptId);
        var validatedJson = DocumentOcrValidation.Json(normalizedJson, nameof(normalizedJson), 100_000);
        var validatedFieldConfidence = DocumentOcrValidation.OptionalJson(
            fieldConfidenceJson, nameof(fieldConfidenceJson), 100_000);
        var validatedConfidence = DocumentOcrValidation.Confidence(confidence, nameof(confidence));

        attempt.Succeed(providerRequestId, completedAt);
        DetectedDocumentType = detectedDocumentType;
        NormalizedJson = validatedJson;
        FieldConfidenceJson = validatedFieldConfidence;
        Confidence = validatedConfidence;
        NeedsReview = needsReview;
        Status = DocumentOcrJobStatus.Completed;
        CompletedAt = completedAt;
        LeaseExpiresAt = null;
        HeartbeatAt = null;
        ErrorCode = null;
        ErrorMessage = null;
        UpdatedAt = completedAt;
    }

    public void RecordFailure(
        Guid attemptId,
        OcrAttemptOutcome outcome,
        string errorCode,
        string errorMessage,
        DateTimeOffset failedAt)
    {
        EnsureStatus(DocumentOcrJobStatus.Processing);
        if (failedAt == default)
            throw new ArgumentException("FailedAt is required.", nameof(failedAt));

        var attempt = GetProcessingAttempt(attemptId);
        var validatedCode = DocumentOcrValidation.RequiredText(errorCode, nameof(errorCode), 100);
        var validatedMessage = DocumentOcrValidation.RequiredText(errorMessage, nameof(errorMessage), 2_000);

        attempt.Fail(outcome, validatedCode, validatedMessage, failedAt);
        Status = DocumentOcrJobStatus.Failed;
        ErrorCode = validatedCode;
        ErrorMessage = validatedMessage;
        FailedAt = failedAt;
        LeaseExpiresAt = null;
        HeartbeatAt = null;
        UpdatedAt = failedAt;
    }

    public void ScheduleRetry(DateTimeOffset nextAttemptAt, DateTimeOffset scheduledAt)
    {
        EnsureStatus(DocumentOcrJobStatus.Failed);
        if (_attempts.LastOrDefault()?.Outcome != OcrAttemptOutcome.TransientFailure)
            throw new InvalidOperationException("Only transient provider failures can be retried.");
        if (nextAttemptAt <= scheduledAt)
            throw new ArgumentOutOfRangeException(nameof(nextAttemptAt), "Next attempt must be scheduled in the future.");

        Status = DocumentOcrJobStatus.Queued;
        NextAttemptAt = nextAttemptAt;
        UpdatedAt = scheduledAt;
    }

    public void Cancel(DateTimeOffset cancelledAt)
    {
        if (Status is not (DocumentOcrJobStatus.Queued or DocumentOcrJobStatus.Processing))
            throw new InvalidOperationException($"A {Status} job cannot be cancelled.");
        if (cancelledAt == default)
            throw new ArgumentException("CancelledAt is required.", nameof(cancelledAt));

        if (Status == DocumentOcrJobStatus.Processing)
            _attempts.Last(attempt => attempt.Outcome == OcrAttemptOutcome.Processing).Cancel(cancelledAt);

        Status = DocumentOcrJobStatus.Cancelled;
        CancelledAt = cancelledAt;
        LeaseExpiresAt = null;
        HeartbeatAt = null;
        NextAttemptAt = null;
        UpdatedAt = cancelledAt;
    }

    private OcrProviderAttempt GetProcessingAttempt(Guid attemptId)
    {
        DocumentOcrValidation.RequiredId(attemptId, nameof(attemptId));
        return _attempts.SingleOrDefault(attempt =>
                   attempt.Id == attemptId && attempt.Outcome == OcrAttemptOutcome.Processing)
               ?? throw new InvalidOperationException("The active provider attempt was not found.");
    }

    private void EnsureStatus(DocumentOcrJobStatus requiredStatus)
    {
        if (Status != requiredStatus)
            throw new InvalidOperationException($"Job must be {requiredStatus} but is {Status}.");
    }
}

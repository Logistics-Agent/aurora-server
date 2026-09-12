using Shared.Entity;
using Shared.Exceptions;
using ShipmentWorkflow.Domain.Enums;

namespace ShipmentWorkflow.Domain.Entities;

public sealed class DocumentIntake : TenantAuditableEntity
{
    public const int IdempotencyKeyMaxLength = 150;
    public const int RequestHashMaxLength = 64;
    public const int FileNameMaxLength = 255;
    public const int StorageReferenceMaxLength = 1_000;
    public const int FailureReasonMaxLength = 1_000;

    private DocumentIntake()
    {
    }

    public Guid ShipmentId { get; private set; }
    public Guid DocumentId { get; private set; }
    public Guid UploadId { get; private set; }
    public string StorageReference { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public DocumentType DocumentType { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public DocumentIntakeStatus Status { get; private set; }
    public string? FailureReason { get; private set; }
    public int StateVersion { get; private set; }

    public static DocumentIntake Create(
        Guid tenantId,
        Guid shipmentId,
        Guid documentId,
        Guid uploadId,
        string storageReference,
        string fileName,
        DocumentType documentType,
        string idempotencyKey,
        string requestHash,
        DateTimeOffset createdAt)
    {
        RequireId(tenantId, nameof(tenantId));
        RequireId(shipmentId, nameof(shipmentId));
        RequireId(documentId, nameof(documentId));
        RequireId(uploadId, nameof(uploadId));
        RequireText(storageReference, nameof(storageReference), StorageReferenceMaxLength);
        RequireText(fileName, nameof(fileName), FileNameMaxLength);
        RequireText(idempotencyKey, nameof(idempotencyKey), IdempotencyKeyMaxLength);
        RequireText(requestHash, nameof(requestHash), RequestHashMaxLength);
        if (createdAt == default)
            throw new ArgumentException("CreatedAt is required.", nameof(createdAt));

        return new DocumentIntake
        {
            TenantId = tenantId,
            ShipmentId = shipmentId,
            DocumentId = documentId,
            UploadId = uploadId,
            StorageReference = storageReference.Trim(),
            FileName = fileName.Trim(),
            DocumentType = documentType,
            IdempotencyKey = idempotencyKey.Trim(),
            RequestHash = requestHash.Trim().ToLowerInvariant(),
            Status = DocumentIntakeStatus.PendingAttachment,
            CreatedAt = createdAt
        };
    }

    public void AttachVerifiedDocument(
        string storageReference,
        string fileName,
        DateTimeOffset changedAt)
    {
        RequireText(storageReference, nameof(storageReference), StorageReferenceMaxLength);
        RequireText(fileName, nameof(fileName), FileNameMaxLength);
        if (Status is DocumentIntakeStatus.PendingOcr or DocumentIntakeStatus.Submitted)
        {
            EnsureAttachmentMetadata(storageReference, fileName);
            return;
        }
        if (Status == DocumentIntakeStatus.FailedRetryable)
        {
            EnsureAttachmentMetadata(storageReference, fileName);
            ResumeOcr(changedAt);
            return;
        }
        EnsureChangedAt(changedAt);
        if (Status != DocumentIntakeStatus.PendingAttachment)
            throw new DomainException("Document intake cannot attach a document from its current state.");

        StorageReference = storageReference.Trim();
        FileName = fileName.Trim();
        Status = DocumentIntakeStatus.PendingOcr;
        UpdatedAt = changedAt;
        AdvanceState();
    }

    public void ReplaceRetryableAttachmentMetadata(string storageReference, string fileName)
    {
        if (Status != DocumentIntakeStatus.FailedRetryable)
            throw new DomainException("Only retryable document intakes can replace attachment metadata.");
        StorageReference = RequireTextValue(storageReference, nameof(storageReference), StorageReferenceMaxLength);
        FileName = RequireTextValue(fileName, nameof(fileName), FileNameMaxLength);
    }

    public void MarkRetryable(string? failureReason, DateTimeOffset changedAt)
    {
        if (Status == DocumentIntakeStatus.FailedRetryable)
            return;
        EnsureChangedAt(changedAt);
        if (Status is not (DocumentIntakeStatus.PendingAttachment or DocumentIntakeStatus.PendingOcr))
            throw new DomainException("Document intake can only be marked retryable while pending attachment or OCR.");

        FailureReason = NormalizeOptionalText(failureReason, FailureReasonMaxLength);
        Status = DocumentIntakeStatus.FailedRetryable;
        UpdatedAt = changedAt;
        AdvanceState();
    }

    public void ResumeOcr(DateTimeOffset changedAt)
    {
        if (Status == DocumentIntakeStatus.PendingOcr)
            return;
        EnsureChangedAt(changedAt);
        if (Status != DocumentIntakeStatus.FailedRetryable)
            throw new DomainException("Document intake can only resume OCR from a retryable failure.");

        FailureReason = null;
        Status = DocumentIntakeStatus.PendingOcr;
        UpdatedAt = changedAt;
        AdvanceState();
    }

    public void MarkSubmitted(DateTimeOffset changedAt)
    {
        if (Status == DocumentIntakeStatus.Submitted)
            return;
        EnsureChangedAt(changedAt);
        if (Status != DocumentIntakeStatus.PendingOcr)
            throw new DomainException("Document intake can only be submitted after OCR is pending.");

        FailureReason = null;
        Status = DocumentIntakeStatus.Submitted;
        UpdatedAt = changedAt;
        AdvanceState();
    }

    private void AdvanceState() => StateVersion++;

    private void EnsureAttachmentMetadata(string storageReference, string fileName)
    {
        if (!string.Equals(StorageReference, storageReference.Trim(), StringComparison.Ordinal) ||
            !string.Equals(FileName, fileName.Trim(), StringComparison.Ordinal))
            throw new DomainException("The verified upload metadata does not match the document intake.");
    }

    private static void RequireId(Guid value, string name)
    {
        if (value == Guid.Empty)
            throw new DomainException($"{name} is required.");
    }

    private static void RequireText(string? value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{name} is required.");
        if (value.Trim().Length > maxLength)
            throw new DomainException($"{name} must be {maxLength} characters or fewer.");
    }

    private static string RequireTextValue(string? value, string name, int maxLength)
    {
        RequireText(value, name, maxLength);
        return value!.Trim();
    }

    private static string? NormalizeOptionalText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (value.Trim().Length > maxLength)
            throw new DomainException($"FailureReason must be {maxLength} characters or fewer.");
        return value.Trim();
    }

    private static void EnsureChangedAt(DateTimeOffset changedAt)
    {
        if (changedAt == default)
            throw new ArgumentException("ChangedAt is required.", nameof(changedAt));
    }
}

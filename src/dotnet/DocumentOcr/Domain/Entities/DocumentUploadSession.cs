using Shared.Entity;

namespace DocumentOcr.Domain.Entities;

public sealed class DocumentUploadSession : TenantAuditableEntity
{
    private DocumentUploadSession()
    {
    }

    public string IdempotencyKey { get; private set; } = string.Empty;
    public string RequestFingerprint { get; private set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public string DeclaredMimeType { get; private set; } = string.Empty;
    public long DeclaredSizeBytes { get; private set; }
    public string? DeclaredContentSha256 { get; private set; }
    public long MaximumSizeBytes { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public Domain.Enums.DocumentUploadStatus Status { get; private set; }
    public Domain.Enums.DocumentUploadCleanupStatus CleanupStatus { get; private set; }
    public int StateVersion { get; private set; }
    public string? VerifiedMimeType { get; private set; }
    public long? VerifiedSizeBytes { get; private set; }
    public string? VerifiedContentSha256 { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public DateTimeOffset? ExpiredAt { get; private set; }

    public static DocumentUploadSession Create(
        Guid tenantId,
        string idempotencyKey,
        string requestFingerprint,
        Guid uploadId,
        string objectKey,
        string fileName,
        string mimeType,
        long sizeBytes,
        string? contentSha256,
        long maximumSizeBytes,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (uploadId == Guid.Empty)
            throw new ArgumentException("UploadId is required.", nameof(uploadId));
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("IdempotencyKey is required.", nameof(idempotencyKey));
        if (string.IsNullOrWhiteSpace(requestFingerprint))
            throw new ArgumentException("RequestFingerprint is required.", nameof(requestFingerprint));
        if (string.IsNullOrWhiteSpace(objectKey))
            throw new ArgumentException("ObjectKey is required.", nameof(objectKey));
        if (sizeBytes <= 0 || maximumSizeBytes <= 0 || sizeBytes > maximumSizeBytes)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        if (expiresAt <= createdAt)
            throw new ArgumentException("ExpiresAt must be after CreatedAt.", nameof(expiresAt));

        return new DocumentUploadSession
        {
            Id = uploadId,
            TenantId = tenantId,
            IdempotencyKey = idempotencyKey,
            RequestFingerprint = requestFingerprint,
            ObjectKey = objectKey,
            FileName = fileName,
            DeclaredMimeType = mimeType,
            DeclaredSizeBytes = sizeBytes,
            DeclaredContentSha256 = contentSha256,
            MaximumSizeBytes = maximumSizeBytes,
            ExpiresAt = expiresAt,
            Status = Domain.Enums.DocumentUploadStatus.Pending,
            CreatedAt = createdAt
        };
    }

    public void MarkUploaded(
        string verifiedMimeType,
        long verifiedSizeBytes,
        string? verifiedContentSha256,
        DateTimeOffset verifiedAt)
    {
        if (Status != Domain.Enums.DocumentUploadStatus.Verifying)
            throw new InvalidOperationException("Only pending upload sessions can be verified.");
        VerifiedMimeType = verifiedMimeType;
        VerifiedSizeBytes = verifiedSizeBytes;
        VerifiedContentSha256 = verifiedContentSha256;
        VerifiedAt = verifiedAt;
        Status = Domain.Enums.DocumentUploadStatus.Uploaded;
        UpdatedAt = verifiedAt;
        AdvanceState();
    }

    public void BeginVerification(DateTimeOffset now)
    {
        if (Status != Domain.Enums.DocumentUploadStatus.Pending)
            throw new InvalidOperationException("Only pending upload sessions can begin verification.");
        Status = Domain.Enums.DocumentUploadStatus.Verifying;
        UpdatedAt = now;
        AdvanceState();
    }

    public void ReturnToPending(DateTimeOffset now)
    {
        if (Status != Domain.Enums.DocumentUploadStatus.Verifying)
            return;
        Status = Domain.Enums.DocumentUploadStatus.Pending;
        UpdatedAt = now;
        AdvanceState();
    }

    public void MarkConsumed(DateTimeOffset consumedAt)
    {
        if (Status != Domain.Enums.DocumentUploadStatus.Uploaded)
            throw new InvalidOperationException("Only uploaded sessions can be consumed.");
        Status = Domain.Enums.DocumentUploadStatus.Consumed;
        ConsumedAt = consumedAt;
        UpdatedAt = consumedAt;
        AdvanceState();
    }

    public void MarkExpired(DateTimeOffset expiredAt)
    {
        if (Status == Domain.Enums.DocumentUploadStatus.Consumed ||
            Status == Domain.Enums.DocumentUploadStatus.Expired)
            return;
        Status = Domain.Enums.DocumentUploadStatus.Expired;
        CleanupStatus = Domain.Enums.DocumentUploadCleanupStatus.DeletePending;
        ExpiredAt = expiredAt;
        UpdatedAt = expiredAt;
        AdvanceState();
    }

    public void MarkCleanupCompleted(DateTimeOffset completedAt)
    {
        if (Status != Domain.Enums.DocumentUploadStatus.Expired ||
            CleanupStatus != Domain.Enums.DocumentUploadCleanupStatus.DeletePending)
        {
            throw new InvalidOperationException("Only claimed expired sessions can complete cleanup.");
        }

        CleanupStatus = Domain.Enums.DocumentUploadCleanupStatus.Deleted;
        UpdatedAt = completedAt;
        AdvanceState();
    }

    private void AdvanceState() => StateVersion++;
}

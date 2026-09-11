using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DocumentOcr.Application.Providers;
using DocumentOcr.Application.Storage;
using DocumentOcr.Domain.Entities;
using DocumentOcr.Domain.Enums;
using DocumentOcr.Infrastructure.Persistences;
using Microsoft.EntityFrameworkCore;
using Shared.Exceptions;
using Shared.Security;

namespace DocumentOcr.Application.Uploads;

public sealed record CreateDocumentUploadInput(
    string IdempotencyKey,
    string FileName,
    string MimeType,
    long SizeBytes,
    string? ContentSha256);

public sealed record DocumentUploadReceipt(
    Guid UploadId,
    string StorageReference,
    string WriteUrl,
    IReadOnlyDictionary<string, string> RequiredHeaders,
    DateTimeOffset ExpiresAt,
    long MaximumSizeBytes,
    DocumentUploadStatus Status,
    string FileName,
    string MimeType,
    long SizeBytes,
    string? ContentSha256,
    string? VerifiedMimeType,
    long? VerifiedSizeBytes,
    string? VerifiedContentSha256);

public sealed class DocumentUploadValidationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class UploadSessionConflictException(string message) : Exception(message);

public sealed class DocumentUploadService(
    DocumentOcrDbContext dbContext,
    ICurrentUserService currentUser,
    TimeProvider timeProvider,
    DocumentInputPolicy inputPolicy,
    IDocumentInputStorage inputStorage,
    DocumentUploadOptions options)
{
    public async Task<DocumentUploadReceipt> CreateAsync(
        CreateDocumentUploadInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        var tenantId = RequireTenant();
        var idempotencyKey = Required(input.IdempotencyKey, nameof(input.IdempotencyKey), 150);
        var fileName = SanitizeFileName(input.FileName);
        var mimeType = Required(input.MimeType, nameof(input.MimeType), 150);
        var contentSha256 = NormalizeHash(input.ContentSha256);
        var maximumSizeBytes = GetMaximumSizeBytes();
        var fingerprint = Fingerprint(fileName, mimeType, input.SizeBytes, contentSha256);

        inputPolicy.ValidateMetadata(
            $"objects/{tenantId}/pending/{fileName}", fileName, mimeType, input.SizeBytes);

        var existing = await dbContext.UploadSessions.SingleOrDefaultAsync(
            session => session.TenantId == tenantId && session.IdempotencyKey == idempotencyKey,
            cancellationToken);
        if (existing is not null)
            return ReplayOrConflict(existing, fingerprint);

        var uploadId = Guid.CreateVersion7();
        var objectKey = $"objects/{tenantId}/{uploadId}/{fileName}";
        var createdAt = timeProvider.GetUtcNow();
        var session = DocumentUploadSession.Create(
            tenantId,
            idempotencyKey,
            fingerprint,
            uploadId,
            objectKey,
            fileName,
            mimeType,
            input.SizeBytes,
            contentSha256,
            maximumSizeBytes,
            createdAt.Add(options.SessionExpiry),
            createdAt);

        var target = await inputStorage.CreateSignedWriteTargetAsync(
            tenantId,
            uploadId,
            objectKey,
            fileName,
            mimeType,
            maximumSizeBytes,
            session.ExpiresAt,
            contentSha256,
            cancellationToken);
        session.SetWriteTarget(target, JsonSerializer.Serialize(target.RequiredHeaders));
        dbContext.UploadSessions.Add(session);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return ToReceipt(session);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            dbContext.ChangeTracker.Clear();
            existing = await dbContext.UploadSessions.SingleAsync(
                item => item.TenantId == tenantId && item.IdempotencyKey == idempotencyKey,
                cancellationToken);
            return ReplayOrConflict(existing, fingerprint);
        }
    }

    public async Task<DocumentUploadReceipt> VerifyAsync(
        Guid uploadId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        RequiredId(uploadId, nameof(uploadId));
        var session = await dbContext.UploadSessions.SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.Id == uploadId,
            cancellationToken) ?? throw new NotFoundException("Document upload session was not found.");

        if (session.Status == DocumentUploadStatus.Uploaded)
            return ToReceipt(session);
        if (session.Status == DocumentUploadStatus.Consumed)
            return ToReceipt(session);
        if (session.Status == DocumentUploadStatus.Expired)
            throw new DocumentUploadValidationException("UPLOAD_EXPIRED", "The upload session has expired.");

        var now = timeProvider.GetUtcNow();
        if (session.ExpiresAt <= now)
        {
            session.MarkExpired(now);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new DocumentUploadValidationException("UPLOAD_EXPIRED", "The upload session has expired.");
        }

        var expectedKey = $"objects/{tenantId}/{session.Id}/{session.FileName}";
        if (!IsTenantObjectKey(session.ObjectKey, tenantId) ||
            !string.Equals(session.ObjectKey, expectedKey, StringComparison.Ordinal))
        {
            throw new DocumentUploadValidationException(
                "UPLOAD_TENANT_MISMATCH", "The upload object is not owned by the current tenant.");
        }

        var metadata = await inputStorage.HeadAsync(tenantId, session.ObjectKey, cancellationToken)
            ?? throw new DocumentUploadValidationException(
                "UPLOAD_OBJECT_NOT_FOUND", "The upload object was not found.");
        if (!string.Equals(metadata.ObjectKey, session.ObjectKey, StringComparison.Ordinal))
            throw new DocumentUploadValidationException(
                "UPLOAD_TENANT_MISMATCH", "The upload object key does not match the session.");
        if (!string.Equals(metadata.ContentType, session.DeclaredMimeType, StringComparison.OrdinalIgnoreCase))
            throw new DocumentUploadValidationException(
                "UPLOAD_MIME_MISMATCH", "The uploaded MIME type does not match the declaration.");
        if (metadata.SizeBytes > session.MaximumSizeBytes)
            throw new DocumentUploadValidationException(
                "UPLOAD_SIZE_EXCEEDED", "The uploaded object exceeds the maximum size.");
        if (metadata.SizeBytes != session.DeclaredSizeBytes)
            throw new DocumentUploadValidationException(
                "UPLOAD_SIZE_MISMATCH", "The uploaded size does not match the declaration.");
        if (session.DeclaredContentSha256 is not null &&
            !string.Equals(session.DeclaredContentSha256, metadata.ContentSha256, StringComparison.Ordinal))
        {
            throw new DocumentUploadValidationException(
                "UPLOAD_HASH_MISMATCH", "The uploaded SHA-256 does not match the declaration.");
        }

        session.MarkUploaded(
            metadata.ContentType,
            metadata.SizeBytes,
            metadata.ContentSha256,
            now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToReceipt(session);
    }

    public async Task<DocumentUploadReceipt> GetAsync(
        Guid uploadId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        RequiredId(uploadId, nameof(uploadId));
        var session = await dbContext.UploadSessions.AsNoTracking().SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.Id == uploadId,
            cancellationToken) ?? throw new NotFoundException("Document upload session was not found.");
        return ToReceipt(session);
    }

    public async Task<DocumentUploadReceipt> ConsumeAsync(
        Guid uploadId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        RequiredId(uploadId, nameof(uploadId));
        var session = await dbContext.UploadSessions.SingleOrDefaultAsync(
            item => item.TenantId == tenantId && item.Id == uploadId,
            cancellationToken) ?? throw new NotFoundException("Document upload session was not found.");
        if (session.Status == DocumentUploadStatus.Consumed)
            return ToReceipt(session);
        session.MarkConsumed(timeProvider.GetUtcNow());
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToReceipt(session);
    }

    internal static DocumentUploadReceipt ToReceipt(DocumentUploadSession session)
    {
        var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(
            session.RequiredHeadersJson) ?? new Dictionary<string, string>();
        return new DocumentUploadReceipt(
            session.Id,
            session.ObjectKey,
            session.WriteUrl ?? string.Empty,
            headers,
            session.ExpiresAt,
            session.MaximumSizeBytes,
            session.Status,
            session.FileName,
            session.DeclaredMimeType,
            session.DeclaredSizeBytes,
            session.DeclaredContentSha256,
            session.VerifiedMimeType,
            session.VerifiedSizeBytes,
            session.VerifiedContentSha256);
    }

    private DocumentUploadReceipt ReplayOrConflict(
        DocumentUploadSession existing,
        string requestFingerprint)
    {
        if (!string.Equals(existing.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
            throw new UploadSessionConflictException("The idempotency key was already used with a different request.");
        return ToReceipt(existing);
    }

    private long GetMaximumSizeBytes() => inputPolicy.MaximumSizeBytes;

    private Guid RequireTenant() =>
        currentUser.TenantId is { } tenantId && tenantId != Guid.Empty
            ? tenantId
            : throw new UnauthorizedAccessException("Tenant context is required.");

    private static void RequiredId(Guid value, string name)
    {
        if (value == Guid.Empty)
            throw new ArgumentException($"{name} is required.", name);
    }

    private static string Required(string? value, string name, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException($"{name} is required.", name);
        if (normalized.Length > maxLength)
            throw new ArgumentOutOfRangeException(name);
        return normalized;
    }

    private static string SanitizeFileName(string? value)
    {
        var normalized = Required(value, nameof(value), 255).Replace('\\', '/');
        normalized = Path.GetFileName(normalized);
        var sanitized = new string(normalized.Select(character =>
            char.IsLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '_').ToArray());
        if (string.IsNullOrWhiteSpace(sanitized) || sanitized is "." or "..")
            throw new ArgumentException("FileName is invalid.", nameof(value));
        return sanitized;
    }

    private static string? NormalizeHash(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var normalized = value.Trim();
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)) ||
            normalized != normalized.ToLowerInvariant())
            throw new ArgumentException("ContentSha256 must be a lowercase SHA-256 hex value.", nameof(value));
        return normalized;
    }

    private static string Fingerprint(
        string fileName,
        string mimeType,
        long sizeBytes,
        string? contentSha256) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{fileName}\n{mimeType}\n{sizeBytes}\n{contentSha256 ?? string.Empty}"))).ToLowerInvariant();

    private static bool IsTenantObjectKey(string objectKey, Guid tenantId) =>
        objectKey.StartsWith($"objects/{tenantId}/", StringComparison.Ordinal) &&
        !objectKey.Contains("..", StringComparison.Ordinal) &&
        !objectKey.Contains('\\');

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation };
}

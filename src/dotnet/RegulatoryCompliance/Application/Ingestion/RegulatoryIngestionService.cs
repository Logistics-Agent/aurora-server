using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RegulatoryCompliance.Domain.Entities;
using RegulatoryCompliance.Domain.Enums;
using RegulatoryCompliance.Infrastructure.Persistences;
using Shared.Constants;
using Shared.Security;

namespace RegulatoryCompliance.Application.Ingestion;

public sealed class RegulatoryIngestionService(
    RegulatoryComplianceDbContext dbContext,
    IRegulatoryChunker chunker,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : IRegulatoryIngestionService
{
    public const int MaximumContentBytes = 1_048_576;
    public const string TenantIngestionPermission = PermissionConstants.Documents.Ingest;
    public const string PlatformIngestionPermission = PermissionConstants.Compliance.PlatformIngest;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/plain", "text/markdown"
    };

    public async Task<RegulatoryIngestionResult> IngestAsync(
        RegulatoryIngestionInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        Authorize(input.Visibility);
        ValidateMetadata(input);

        var contentBytes = input.Content.ToArray();
        if (contentBytes.Length == 0 || contentBytes.Length > MaximumContentBytes)
            throw new ArgumentOutOfRangeException(nameof(input.Content), $"Content must be 1-{MaximumContentBytes} bytes.");
        if (input.SizeBytes != contentBytes.Length)
            throw new ArgumentException("SizeBytes does not match the uploaded content.", nameof(input.SizeBytes));
        var actualHash = Convert.ToHexString(SHA256.HashData(contentBytes)).ToLowerInvariant();
        if (!actualHash.Equals(input.ContentSha256, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("ContentSha256 does not match the uploaded content.", nameof(input.ContentSha256));

        string text;
        try
        {
            text = StrictUtf8.GetString(contentBytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new ArgumentException("Content must be valid UTF-8 text.", nameof(input.Content), exception);
        }
        var drafts = chunker.Chunk(text);

        var scopeKey = input.Visibility == SourceVisibility.Platform
            ? Guid.Empty
            : currentUser.TenantId!.Value;
        var receivedAt = timeProvider.GetUtcNow();
        var document = await dbContext.RegulatoryDocuments
            .Include(item => item.Versions)
            .ThenInclude(version => version.Chunks)
            .SingleOrDefaultAsync(item =>
                    item.ScopeKey == scopeKey &&
                    item.CanonicalSourceUri == input.CanonicalSourceUri.Trim() &&
                    item.JurisdictionCode == input.JurisdictionCode.Trim().ToUpper() &&
                    item.LanguageCode == input.LanguageCode.Trim().ToLower(),
                cancellationToken);

        var replay = document?.Versions.SingleOrDefault(version =>
            version.IngestionKey == input.IdempotencyKey.Trim());
        if (replay is not null)
        {
            if (replay.ContentSha256 != actualHash ||
                replay.VersionLabel != input.VersionLabel.Trim())
                throw new InvalidOperationException("The idempotency key was already used with different content.");
            return new RegulatoryIngestionResult(
                document!.Id, replay.Id, replay.IngestionStatus, replay.ChunkCount, true, receivedAt);
        }

        document ??= input.Visibility == SourceVisibility.Platform
            ? RegulatoryDocument.CreatePlatform(
                input.Authority, input.Title, input.CanonicalSourceUri, input.JurisdictionCode,
                input.RegulationType, input.LanguageCode, receivedAt)
            : RegulatoryDocument.CreateTenant(
                scopeKey, input.Authority, input.Title, input.CanonicalSourceUri,
                input.JurisdictionCode, input.RegulationType, input.LanguageCode, receivedAt);
        if (dbContext.Entry(document).State == EntityState.Detached)
            dbContext.RegulatoryDocuments.Add(document);

        var version = document.AddVersion(
            input.IdempotencyKey,
            input.VersionLabel,
            input.PublishedAt,
            input.EffectiveFrom,
            input.EffectiveTo,
            actualHash,
            input.ContentReference,
            input.FileName,
            input.MimeType,
            input.SizeBytes,
            receivedAt,
            document.Versions.OrderByDescending(item => item.EffectiveFrom).FirstOrDefault()?.Id);
        version.StartIngestion(receivedAt);
        foreach (var draft in drafts)
            version.AddChunk(
                draft.Sequence, draft.SectionLabel, draft.PageLabel, draft.Text, draft.TokenCount,
                draft.StartOffset, draft.EndOffset, draft.ContentSha256, receivedAt);
        version.CompleteIngestion(receivedAt);
        if (dbContext.Entry(version).State == EntityState.Detached)
            dbContext.RegulatoryDocumentVersions.Add(version);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new RegulatoryIngestionResult(
                document.Id, version.Id, version.IngestionStatus, version.ChunkCount, false, receivedAt);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            dbContext.ChangeTracker.Clear();
            var winner = await dbContext.RegulatoryDocumentVersions
                .Include(item => item.Chunks)
                .SingleOrDefaultAsync(item =>
                        item.ScopeKey == scopeKey &&
                        item.IngestionKey == input.IdempotencyKey.Trim(),
                    cancellationToken);
            if (winner is null)
                throw;
            if (winner.ContentSha256 != actualHash || winner.VersionLabel != input.VersionLabel.Trim())
                throw new InvalidOperationException(
                    "The idempotency key was already used with different content.", exception);
            return new RegulatoryIngestionResult(
                winner.RegulatoryDocumentId,
                winner.Id,
                winner.IngestionStatus,
                winner.ChunkCount,
                true,
                receivedAt);
        }
    }

    public async Task<RegulatoryIngestionResult> CreatePendingOcrAsync(
        RegulatoryPendingOcrInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        Authorize(input.Visibility);
        ValidatePendingMetadata(input);

        var scopeKey = input.Visibility == SourceVisibility.Platform
            ? Guid.Empty
            : currentUser.TenantId!.Value;
        var now = timeProvider.GetUtcNow();
        var existing = await dbContext.RegulatoryDocumentVersions
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(version =>
                version.ScopeKey == scopeKey && version.IngestionKey == input.IdempotencyKey.Trim(),
                cancellationToken);
        if (existing is not null)
            return ReplayPending(existing, input, now);

        var document = await dbContext.RegulatoryDocuments
            .IgnoreQueryFilters()
            .Include(item => item.Versions)
            .SingleOrDefaultAsync(item =>
                item.ScopeKey == scopeKey &&
                item.CanonicalSourceUri == input.CanonicalSourceUri.Trim() &&
                item.JurisdictionCode == input.JurisdictionCode.Trim().ToUpperInvariant() &&
                item.LanguageCode == input.LanguageCode.Trim().ToLowerInvariant(),
                cancellationToken);
        document ??= input.Visibility == SourceVisibility.Platform
            ? RegulatoryDocument.CreatePlatform(input.Authority, input.Title, input.CanonicalSourceUri,
                input.JurisdictionCode, input.RegulationType, input.LanguageCode, now)
            : RegulatoryDocument.CreateTenant(scopeKey, input.Authority, input.Title, input.CanonicalSourceUri,
                input.JurisdictionCode, input.RegulationType, input.LanguageCode, now);
        if (dbContext.Entry(document).State == EntityState.Detached)
            dbContext.RegulatoryDocuments.Add(document);

        var version = document.AddVersion(
            input.IdempotencyKey, input.VersionLabel, input.PublishedAt, input.EffectiveFrom,
            input.EffectiveTo, input.ContentSha256, input.ContentReference, input.FileName,
            input.MimeType, input.SizeBytes, now,
            document.Versions.OrderByDescending(item => item.EffectiveFrom).FirstOrDefault()?.Id);
        version.MarkPendingOcr(now);
        if (dbContext.Entry(version).State == EntityState.Detached)
            dbContext.RegulatoryDocumentVersions.Add(version);

        await dbContext.SaveChangesAsync(cancellationToken);
        return new RegulatoryIngestionResult(
            document.Id, version.Id, version.IngestionStatus, 0, false, now);
    }

    private void Authorize(SourceVisibility visibility)
    {
        if (!Enum.IsDefined(visibility))
            throw new ArgumentOutOfRangeException(nameof(visibility));

        if (visibility == SourceVisibility.Tenant &&
            (!currentUser.TenantId.HasValue || currentUser.TenantId == Guid.Empty))
            throw new InvalidOperationException("Tenant context is required for tenant source ingestion.");

        if (currentUser.IsSystemAdmin() || currentUser.HasPermission(PlatformIngestionPermission))
            return;

        if (visibility == SourceVisibility.Tenant && currentUser.IsTenantAdmin())
            return;

        var permission = visibility == SourceVisibility.Platform
            ? PlatformIngestionPermission
            : TenantIngestionPermission;

        if (!currentUser.HasPermission(permission) &&
            !currentUser.HasPermission(PermissionConstants.Documents.Manage) &&
            !currentUser.HasPermission(PermissionConstants.Documents.Ingest))
        {
            throw new UnauthorizedAccessException("Regulatory source ingestion permission is required.");
        }

    }

    private static void ValidateMetadata(RegulatoryIngestionInput input)
    {
        if (string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Length > 150)
            throw new ArgumentException("IdempotencyKey is required.", nameof(input.IdempotencyKey));
        if (!Uri.TryCreate(input.CanonicalSourceUri, UriKind.Absolute, out var sourceUri) ||
            sourceUri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(sourceUri.UserInfo))
            throw new ArgumentException("CanonicalSourceUri must be an HTTPS provenance URI.", nameof(input.CanonicalSourceUri));
        if (Path.IsPathRooted(input.ContentReference) ||
            input.ContentReference.Contains("..", StringComparison.Ordinal) ||
            input.ContentReference.Contains("://", StringComparison.Ordinal) ||
            !input.ContentReference.StartsWith("regulatory/", StringComparison.Ordinal))
            throw new ArgumentException("ContentReference must be an approved regulatory storage key.", nameof(input.ContentReference));
        if (!AllowedMimeTypes.Contains(input.MimeType))
            throw new ArgumentException("Only UTF-8 text/plain and text/markdown content is accepted.", nameof(input.MimeType));
        if (!Enum.IsDefined(input.RegulationType))
            throw new ArgumentOutOfRangeException(nameof(input.RegulationType));
        if (input.PublishedAt == default || input.EffectiveFrom == default)
            throw new ArgumentException("PublishedAt and EffectiveFrom are required.");
        if (input.EffectiveTo.HasValue && input.EffectiveTo <= input.EffectiveFrom)
            throw new ArgumentOutOfRangeException(nameof(input.EffectiveTo));
    }

    private void ValidatePendingMetadata(RegulatoryPendingOcrInput input)
    {
        if (string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Length > 150)
            throw new ArgumentException("IdempotencyKey is required.", nameof(input.IdempotencyKey));
        if (!Uri.TryCreate(input.CanonicalSourceUri, UriKind.Absolute, out var sourceUri) ||
            sourceUri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(sourceUri.UserInfo))
            throw new ArgumentException("CanonicalSourceUri must be an HTTPS provenance URI.", nameof(input.CanonicalSourceUri));
        if (!Enum.IsDefined(input.RegulationType))
            throw new ArgumentOutOfRangeException(nameof(input.RegulationType));
        if (input.PublishedAt == default || input.EffectiveFrom == default)
            throw new ArgumentException("PublishedAt and EffectiveFrom are required.");
        if (input.EffectiveTo.HasValue && input.EffectiveTo <= input.EffectiveFrom)
            throw new ArgumentOutOfRangeException(nameof(input.EffectiveTo));
        ValidatePendingFile(input.ContentReference, input.FileName, input.MimeType,
            input.SizeBytes, input.ContentSha256, input.Visibility, currentUser.TenantId);
    }

    private static void ValidatePendingFile(
        string contentReference,
        string fileName,
        string mimeType,
        long sizeBytes,
        string contentSha256,
        SourceVisibility visibility,
        Guid? tenantId)
    {
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 255)
            throw new ArgumentException("FileName is required.", nameof(fileName));
        if (string.IsNullOrWhiteSpace(contentReference) ||
            Path.IsPathRooted(contentReference) ||
            contentReference.Contains("..", StringComparison.Ordinal) ||
            contentReference.Contains("://", StringComparison.Ordinal) ||
            (visibility == SourceVisibility.Tenant &&
                (!tenantId.HasValue || !contentReference.StartsWith($"tenants/{tenantId}/documents/", StringComparison.Ordinal))) ||
            (visibility == SourceVisibility.Platform && !contentReference.StartsWith("platform/", StringComparison.Ordinal)))
            throw new ArgumentException("ContentReference must be an approved upload storage key.", nameof(contentReference));
        if (sizeBytes <= 0 || sizeBytes > 100 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(sizeBytes));
        if (contentSha256.Length != 64 || contentSha256.Any(value => !Uri.IsHexDigit(value)))
            throw new ArgumentException("ContentSha256 must be a 64-character hexadecimal hash.", nameof(contentSha256));
        var allowedMimeTypes = new[] { "text/plain", "text/markdown", "application/pdf", "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/octet-stream" };
        if (!allowedMimeTypes.Contains(mimeType, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("The uploaded MIME type is not supported for OCR corpus ingestion.", nameof(mimeType));
    }

    private static RegulatoryIngestionResult ReplayPending(
        RegulatoryDocumentVersion existing,
        RegulatoryPendingOcrInput input,
        DateTimeOffset now)
    {
        if (existing.ContentSha256 != input.ContentSha256 ||
            existing.VersionLabel != input.VersionLabel.Trim() ||
            existing.FileName != input.FileName.Trim())
            throw new InvalidOperationException("The idempotency key was already used with different content.");
        return new RegulatoryIngestionResult(
            existing.RegulatoryDocumentId, existing.Id, existing.IngestionStatus,
            existing.ChunkCount, true, now);
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };
}

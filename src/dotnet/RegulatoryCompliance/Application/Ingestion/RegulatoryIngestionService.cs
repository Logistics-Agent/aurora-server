using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RegulatoryCompliance.Domain.Entities;
using RegulatoryCompliance.Domain.Enums;
using RegulatoryCompliance.Infrastructure.Persistences;
using Shared.Security;

namespace RegulatoryCompliance.Application.Ingestion;

public sealed class RegulatoryIngestionService(
    RegulatoryComplianceDbContext dbContext,
    IRegulatoryChunker chunker,
    ICurrentUserService currentUser,
    TimeProvider timeProvider) : IRegulatoryIngestionService
{
    public const int MaximumContentBytes = 1_048_576;
    public const string TenantIngestionPermission = "regulatory-compliance.sources.ingest";
    public const string PlatformIngestionPermission = "regulatory-compliance.sources.ingest-platform";
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

    private void Authorize(SourceVisibility visibility)
    {
        if (!Enum.IsDefined(visibility))
            throw new ArgumentOutOfRangeException(nameof(visibility));
        var permission = visibility == SourceVisibility.Platform
            ? PlatformIngestionPermission
            : TenantIngestionPermission;
        if (!currentUser.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Regulatory source ingestion permission is required.");
        if (visibility == SourceVisibility.Tenant &&
            (!currentUser.TenantId.HasValue || currentUser.TenantId == Guid.Empty))
            throw new InvalidOperationException("Tenant context is required for tenant source ingestion.");
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

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };
}

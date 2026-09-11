namespace DocumentOcr.Application.Storage;

public interface IDocumentInputStorage
{
    Task<SignedWriteTarget> CreateSignedWriteTargetAsync(
        Guid tenantId,
        Guid uploadId,
        string objectKey,
        string fileName,
        string mimeType,
        long maximumSizeBytes,
        DateTimeOffset expiresAt,
        string? contentSha256,
        CancellationToken cancellationToken = default);

    Task<DocumentObjectMetadata?> HeadAsync(
        Guid tenantId,
        string objectKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid tenantId,
        string objectKey,
        CancellationToken cancellationToken = default);
}

public sealed record SignedWriteTarget(
    string Url,
    IReadOnlyDictionary<string, string> RequiredHeaders,
    DateTimeOffset ExpiresAt,
    long MaximumSizeBytes);

public sealed record DocumentObjectMetadata(
    string ObjectKey,
    string ContentType,
    long SizeBytes,
    string? ContentSha256);

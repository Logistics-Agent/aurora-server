namespace DocumentOcr.Application.Storage;

public interface IArtifactStorageService
{
    Task<ArtifactStorageResult> StoreArtifactAsync(
        Guid tenantId,
        Guid jobId,
        string fileName,
        string contentType,
        byte[] data,
        CancellationToken cancellationToken = default);

    Task<byte[]?> ReadArtifactAsync(
        string artifactReference,
        CancellationToken cancellationToken = default);
}

public sealed record ArtifactStorageResult(
    string ArtifactReference,
    string ContentSha256,
    long SizeBytes,
    DateTimeOffset StoredAt);

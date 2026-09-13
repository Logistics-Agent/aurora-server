using Amazon.S3;
using Amazon.S3.Model;
using DocumentOcr.Application.Storage;
using Microsoft.Extensions.Configuration;

namespace DocumentOcr.Infrastructure.Storage;

public sealed class S3ArtifactStorageService(
    IAmazonS3 client,
    IConfiguration configuration) : IArtifactStorageService
{
    private readonly string _bucketName = configuration["Storage:S3:Bucket"]
        ?? throw new InvalidOperationException("Storage:S3:Bucket is required when S3 artifact storage is enabled.");

    public async Task<ArtifactStorageResult> StoreArtifactAsync(
        Guid tenantId,
        Guid jobId,
        string fileName,
        string contentType,
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (jobId == Guid.Empty) throw new ArgumentException("JobId is required.", nameof(jobId));
        ArgumentNullException.ThrowIfNull(data);

        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName) || safeFileName is "." or "..")
            throw new ArgumentException("File name is invalid.", nameof(fileName));

        var objectKey = $"tenants/{tenantId}/documents/{jobId}/artifacts/{safeFileName}";
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            InputStream = new MemoryStream(data, writable: false),
            ContentType = contentType,
            AutoCloseStream = true
        }, cancellationToken);

        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(data)).ToLowerInvariant();
        return new ArtifactStorageResult(
            $"ocr-artifacts/tenants/{tenantId}/documents/{jobId}/artifacts/{safeFileName}",
            hash,
            data.Length,
            DateTimeOffset.UtcNow);
    }

    public async Task<byte[]?> ReadArtifactAsync(
        string artifactReference,
        CancellationToken cancellationToken = default)
    {
        var objectKey = ToObjectKey(artifactReference);
        if (objectKey is null)
            return null;

        try
        {
            using var response = await client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            }, cancellationToken);
            await using var stream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(stream, cancellationToken);
            return stream.ToArray();
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private static string? ToObjectKey(string artifactReference)
    {
        const string prefix = "ocr-artifacts/";
        if (string.IsNullOrWhiteSpace(artifactReference) ||
            !artifactReference.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var relative = artifactReference[prefix.Length..];
        if (relative.Contains("..", StringComparison.Ordinal) ||
            relative.Contains('\\') ||
            relative.Split('/').Any(string.IsNullOrEmpty))
            return null;

        return relative.StartsWith("tenants/", StringComparison.OrdinalIgnoreCase)
            ? relative
            : $"artifacts/{relative}";
    }
}

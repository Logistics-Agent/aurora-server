using Amazon.S3;
using Amazon.S3.Model;
using DocumentOcr.Application.Storage;
using Microsoft.Extensions.Configuration;

namespace DocumentOcr.Infrastructure.Storage;

public sealed class S3DocumentInputStorage(IAmazonS3 client, IConfiguration configuration) : IDocumentInputStorage
{
    private readonly string _bucketName = configuration["Storage:S3:Bucket"]
        ?? throw new InvalidOperationException("Storage:S3:Bucket is required when S3 input storage is enabled.");

    public Task<SignedWriteTarget> CreateSignedWriteTargetAsync(
        Guid tenantId,
        Guid uploadId,
        string objectKey,
        string fileName,
        string mimeType,
        long maximumSizeBytes,
        DateTimeOffset expiresAt,
        string? contentSha256,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(tenantId, uploadId, objectKey);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = mimeType
        };
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Protocol = Protocol.HTTPS,
            Expires = expiresAt.UtcDateTime,
            ContentType = mimeType
        };
        if (contentSha256 is not null)
        {
            request.Headers["x-amz-meta-content-sha256"] = contentSha256;
            headers["x-amz-meta-content-sha256"] = contentSha256;
        }

        return Task.FromResult(new SignedWriteTarget(
            client.GetPreSignedURL(request), headers, expiresAt, maximumSizeBytes));
    }

    public async Task<DocumentObjectMetadata?> HeadAsync(
        Guid tenantId,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateKeyPrefix(tenantId, objectKey);
        try
        {
            var response = await client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            }, cancellationToken);
            var sha = response.Metadata["x-amz-meta-content-sha256"];
            return new DocumentObjectMetadata(objectKey, response.Headers.ContentType, response.ContentLength, sha);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(
        Guid tenantId,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateKeyPrefix(tenantId, objectKey);
        await client.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = _bucketName,
            Key = objectKey
        }, cancellationToken);
    }

    private static void ValidateKey(Guid tenantId, Guid uploadId, string objectKey)
    {
        if (uploadId == Guid.Empty)
            throw new ArgumentException("UploadId is required.", nameof(uploadId));
        ValidateKeyPrefix(tenantId, objectKey);
        if (!objectKey.StartsWith($"objects/{tenantId}/{uploadId}/", StringComparison.Ordinal))
            throw new ArgumentException("Object key does not match the upload session.", nameof(objectKey));
    }

    private static void ValidateKeyPrefix(Guid tenantId, string objectKey)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(objectKey) ||
            !objectKey.StartsWith($"objects/{tenantId}/", StringComparison.Ordinal) ||
            objectKey.Contains("..", StringComparison.Ordinal) ||
            objectKey.Contains('\\') ||
            objectKey.Split('/').Any(string.IsNullOrEmpty))
            throw new ArgumentException("Object key is not tenant safe.", nameof(objectKey));
    }
}

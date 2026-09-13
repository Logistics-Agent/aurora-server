using Amazon.S3;
using Amazon.S3.Model;
using DocumentOcr.Application.Storage;
using Microsoft.Extensions.Configuration;
using System.Globalization;

namespace DocumentOcr.Infrastructure.Storage;

public sealed class S3DocumentInputStorage(IAmazonS3 client, IConfiguration configuration) : IDocumentInputStorage, IDocumentDownloadStorage
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
        if (maximumSizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumSizeBytes));

        // Browser PUTs emit Content-Length automatically for a Blob. Signing the
        // declared length makes the presigned URL reject an oversized body before
        // S3/R2 stores it. Post-upload inspection remains the authoritative defense.
        var contentLength = maximumSizeBytes.ToString(CultureInfo.InvariantCulture);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Protocol = Protocol.HTTPS,
            Expires = expiresAt.UtcDateTime,
            ContentType = mimeType
        };
        request.Headers["Content-Length"] = contentLength;
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
            using var response = await client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = objectKey
            }, cancellationToken);
            return await DocumentObjectInspector.InspectAsync(
                objectKey, response.ResponseStream, cancellationToken, response.Headers.ContentType);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task<SignedReadTarget> CreateSignedReadTargetAsync(
        Guid tenantId,
        string objectKey,
        string fileName,
        string contentType,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default)
    {
        ValidateKeyPrefix(tenantId, objectKey);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", nameof(fileName));
        if (string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Content type is required.", nameof(contentType));

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = objectKey,
            Verb = HttpVerb.GET,
            Protocol = Protocol.HTTPS,
            Expires = expiresAt.UtcDateTime
        };
        request.ResponseHeaderOverrides.ContentDisposition =
            $"attachment; filename=\"{Uri.EscapeDataString(Path.GetFileName(fileName))}\"";
        request.ResponseHeaderOverrides.ContentType = contentType;

        return Task.FromResult(new SignedReadTarget(
            client.GetPreSignedURL(request), expiresAt));
    }

    public Task WriteAsync(
        Guid tenantId,
        string objectKey,
        Stream content,
        long maximumSizeBytes,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The HTTP upload bridge is available only with FileSystem input storage.");

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
        if (!objectKey.StartsWith($"tenants/{tenantId}/documents/{uploadId}/", StringComparison.Ordinal) &&
            !objectKey.StartsWith($"objects/{tenantId}/{uploadId}/", StringComparison.Ordinal))
            throw new ArgumentException("Object key does not match the upload session.", nameof(objectKey));
    }

    private static void ValidateKeyPrefix(Guid tenantId, string objectKey)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(objectKey) ||
            (!objectKey.StartsWith($"tenants/{tenantId}/documents/", StringComparison.Ordinal) &&
             !objectKey.StartsWith($"objects/{tenantId}/", StringComparison.Ordinal)) ||
            objectKey.Contains("..", StringComparison.Ordinal) ||
            objectKey.Contains('\\') ||
            objectKey.Split('/').Any(string.IsNullOrEmpty))
            throw new ArgumentException("Object key is not tenant safe.", nameof(objectKey));
    }
}

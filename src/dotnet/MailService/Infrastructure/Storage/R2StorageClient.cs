using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using MailService.Application.Interfaces;
using MailService.Domain.Enums;

namespace MailService.Infrastructure.Storage;

public class R2StorageClient : IR2StorageClient
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly ILogger<R2StorageClient> _logger;

    public R2StorageClient(IAmazonS3 s3Client, string bucketName = "aurora-mail-dev", ILogger<R2StorageClient>? logger = null)
    {
        _s3Client = s3Client;
        _bucketName = bucketName;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<R2StorageClient>.Instance;
    }

    public async Task<string> UploadRawEmlAsync(Guid tenantId, string messageId, EmailDirection direction, byte[] emlBytes, CancellationToken cancellationToken = default)
    {
        string dirStr = direction == EmailDirection.Inbound ? "inbound" : "outbound";
        var now = DateTime.UtcNow;
        string key = $"tenants/{tenantId}/{dirStr}/{now:yyyy}/{now:MM}/{now:dd}/{messageId}/raw.eml";

        try
        {
            using var ms = new MemoryStream(emlBytes);
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = ms,
                ContentType = "message/rfc822"
            };

            await _s3Client.PutObjectAsync(putRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "R2 storage upload raw EML failed for key {Key}", key);
        }

        return key;
    }

    public async Task<string> UploadAttachmentAsync(Guid tenantId, string messageId, EmailDirection direction, string filename, byte[] content, CancellationToken cancellationToken = default)
    {
        string dirStr = direction == EmailDirection.Inbound ? "inbound" : "outbound";
        var now = DateTime.UtcNow;
        string key = $"tenants/{tenantId}/{dirStr}/{now:yyyy}/{now:MM}/{now:dd}/{messageId}/attachments/{filename}";

        try
        {
            using var ms = new MemoryStream(content);
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                InputStream = ms
            };

            await _s3Client.PutObjectAsync(putRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "R2 storage upload attachment failed for key {Key}", key);
        }

        return key;
    }

    public async Task<string> UploadJsonMetadataAsync(Guid tenantId, string messageId, EmailDirection direction, string keySuffix, string jsonContent, CancellationToken cancellationToken = default)
    {
        string dirStr = direction == EmailDirection.Inbound ? "inbound" : "outbound";
        var now = DateTime.UtcNow;
        string key = $"tenants/{tenantId}/{dirStr}/{now:yyyy}/{now:MM}/{now:dd}/{messageId}/{keySuffix}";

        try
        {
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = key,
                ContentBody = jsonContent,
                ContentType = "application/json"
            };

            await _s3Client.PutObjectAsync(putRequest, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "R2 storage upload JSON metadata failed for key {Key}", key);
        }

        return key;
    }

    public async Task<string> GeneratePresignedUrlAsync(string objectKey, int expirySeconds = 3600, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        try
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                Expires = DateTime.UtcNow.AddSeconds(Math.Min(expirySeconds, 3600)),
                Verb = HttpVerb.GET,
                Protocol = Protocol.HTTPS
            };

            return _s3Client.GetPreSignedURL(request);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "R2 presigned URL generation failed for {Key}", objectKey);
            return $"https://r2.aurora.local/{_bucketName}/{objectKey}";
        }
    }
}

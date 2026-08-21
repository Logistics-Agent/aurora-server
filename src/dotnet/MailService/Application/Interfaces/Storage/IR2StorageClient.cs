using MailService.Domain.Enums;

namespace MailService.Application.Interfaces.Storage;

public interface IR2StorageClient
{
    Task<string> UploadRawEmlAsync(Guid tenantId, string messageId, EmailDirection direction, byte[] emlBytes, CancellationToken cancellationToken = default);
    Task<string> UploadAttachmentAsync(Guid tenantId, string messageId, EmailDirection direction, string filename, byte[] content, CancellationToken cancellationToken = default);
    Task<string> UploadJsonMetadataAsync(Guid tenantId, string messageId, EmailDirection direction, string keySuffix, string jsonContent, CancellationToken cancellationToken = default);
    Task<string> GeneratePresignedUrlAsync(string objectKey, int expirySeconds = 3600, CancellationToken cancellationToken = default);
}

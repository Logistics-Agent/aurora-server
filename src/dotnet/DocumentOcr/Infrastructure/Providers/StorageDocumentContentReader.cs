using DocumentOcr.Application.Providers;
using DocumentOcr.Application.Storage;

namespace DocumentOcr.Infrastructure.Providers;

public sealed class StorageDocumentContentReader(
    IDocumentInputStorage storage,
    DocumentInputPolicy policy) : IDocumentContentReader
{
    public async Task<DocumentContent> ReadAsync(
        DocumentContentRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        policy.ValidateMetadata(
            request.StorageReference,
            request.FileName,
            request.MimeType,
            request.DeclaredSizeBytes);

        var content = await storage.ReadContentAsync(
            request.TenantId,
            request.StorageReference,
            request.FileName,
            request.MimeType,
            policy.MaximumSizeBytes,
            cancellationToken);
        policy.ValidateContent(content);
        return content;
    }
}

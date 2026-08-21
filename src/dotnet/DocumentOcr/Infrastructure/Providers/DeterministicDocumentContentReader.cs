using System.Text;
using DocumentOcr.Application.Providers;

namespace DocumentOcr.Infrastructure.Providers;

public sealed class DeterministicDocumentContentReader(DocumentInputPolicy policy) : IDocumentContentReader
{
    private static readonly byte[] Content = Encoding.UTF8.GetBytes("deterministic document content");

    public Task<DocumentContent> ReadAsync(
        DocumentContentRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.TenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(request));
        policy.ValidateMetadata(
            request.StorageReference,
            request.FileName,
            request.MimeType,
            request.DeclaredSizeBytes);
        var content = DocumentContent.Create(Content, request.MimeType, 1);
        policy.ValidateContent(content);
        return Task.FromResult(content);
    }
}

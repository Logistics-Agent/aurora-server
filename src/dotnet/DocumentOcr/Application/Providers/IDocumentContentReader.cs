namespace DocumentOcr.Application.Providers;

public interface IDocumentContentReader
{
    Task<DocumentContent> ReadAsync(
        DocumentContentRequest request,
        CancellationToken cancellationToken);
}

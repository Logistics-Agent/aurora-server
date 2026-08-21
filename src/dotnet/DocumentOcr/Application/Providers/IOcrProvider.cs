namespace DocumentOcr.Application.Providers;

public interface IOcrProvider
{
    string Name { get; }
    Task<OcrProviderResult> ExtractAsync(
        OcrProviderRequest request,
        CancellationToken cancellationToken);
}

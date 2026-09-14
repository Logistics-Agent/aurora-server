using System.Text;
using DocumentOcr.Application.Providers;
using DocumentOcr.Domain.Enums;
using DocumentOcr.Infrastructure.Providers;

namespace DocumentOcr.Tests;

public sealed class MarkdownOcrProviderTests
{
    [Fact]
    public async Task ExtractsUploadedMarkdownAsFullTextWithoutCallingAiProvider()
    {
        var fallback = new ThrowingOcrProvider();
        var provider = new MarkdownOcrProvider(fallback);
        var request = new OcrProviderRequest(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "rule.md",
            "text/markdown",
            OcrDocumentType.Other,
            DocumentContent.Create(Encoding.UTF8.GetBytes("# Rule\n\nKeep the shipment on hold."), "text/markdown", 1),
            OcrExtractionMode.Both);

        var result = await provider.ExtractAsync(request, CancellationToken.None);

        Assert.Equal("# Rule\n\nKeep the shipment on hold.", result.FullTextContent);
        Assert.Equal(OcrExtractionMode.Both, result.ExtractionMode);
        Assert.Equal("markdown", provider.Name);
        Assert.DoesNotContain(result.Fields, field => field.Name == "fallback");
    }

    [Fact]
    public async Task DelegatesNonMarkdownDocumentsToTheConfiguredProvider()
    {
        var fallback = new DeterministicOcrProvider();
        var provider = new MarkdownOcrProvider(fallback);
        var request = new OcrProviderRequest(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "invoice.pdf",
            "application/pdf",
            OcrDocumentType.CommercialInvoice,
            DocumentContent.Create([1, 2, 3], "application/pdf", 1));

        var result = await provider.ExtractAsync(request, CancellationToken.None);

        Assert.Null(result.FullTextContent);
        Assert.Contains(result.Fields, field => field.Name == "documentNumber");
    }

    private sealed class ThrowingOcrProvider : IOcrProvider
    {
        public string Name => "fallback";

        public Task<OcrProviderResult> ExtractAsync(
            OcrProviderRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The AI provider should not be called for Markdown.");
    }
}

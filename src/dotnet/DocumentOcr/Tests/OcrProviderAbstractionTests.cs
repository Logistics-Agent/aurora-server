using DocumentOcr.Application.Providers;
using DocumentOcr.Domain.Enums;
using DocumentOcr.Infrastructure.Providers;

namespace DocumentOcr.Tests;

public sealed class OcrProviderAbstractionTests
{
    private readonly DocumentInputPolicy _policy = new(new DocumentProcessingOptions());

    [Fact]
    public void PolicyAcceptsApprovedObjectReferenceAndSupportedDocument()
    {
        _policy.ValidateMetadata(
            "objects/tenant/invoice.pdf",
            "invoice.pdf",
            "application/pdf",
            1_024);
        _policy.ValidateContent(DocumentContent.Create([1, 2, 3], "application/pdf", 1));
    }

    [Theory]
    [InlineData("https://example.test/document.pdf")]
    [InlineData("/tmp/document.pdf")]
    [InlineData("objects/../document.pdf")]
    [InlineData("objects\\tenant\\document.pdf")]
    public void PolicyRejectsUnsafeStorageReferences(string storageReference)
    {
        Assert.Throws<ArgumentException>(() => _policy.ValidateMetadata(
            storageReference,
            "invoice.pdf",
            "application/pdf",
            1_024));
    }

    [Theory]
    [InlineData("invoice.exe", "application/pdf")]
    [InlineData("invoice.pdf", "application/octet-stream")]
    public void PolicyRejectsUnsupportedExtensionOrMimeType(string fileName, string mimeType)
    {
        Assert.Throws<ArgumentException>(() => _policy.ValidateMetadata(
            "objects/tenant/document",
            fileName,
            mimeType,
            1_024));
    }

    [Fact]
    public void PolicyRejectsOversizedDocumentsAndPageCounts()
    {
        var options = new DocumentProcessingOptions { MaxDocumentBytes = 3, MaxPages = 1 };
        var policy = new DocumentInputPolicy(options);

        Assert.Throws<ArgumentOutOfRangeException>(() => policy.ValidateMetadata(
            "objects/tenant/invoice.pdf", "invoice.pdf", "application/pdf", 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => policy.ValidateContent(
            DocumentContent.Create([1, 2, 3], "application/pdf", 2)));
    }

    [Fact]
    public async Task DeterministicProviderReturnsNormalizedProviderResult()
    {
        IOcrProvider provider = new DeterministicOcrProvider();
        var request = CreateProviderRequest();

        var result = await provider.ExtractAsync(request, CancellationToken.None);

        Assert.Equal("deterministic", provider.Name);
        Assert.Equal(OcrDocumentType.CommercialInvoice, result.DetectedDocumentType);
        Assert.Contains(result.Fields, field => field.Name == "documentNumber" && field.Confidence == 0.99m);
        Assert.NotEmpty(result.ProviderRequestId);
    }

    [Theory]
    [InlineData(OcrProviderFailureKind.Transient)]
    [InlineData(OcrProviderFailureKind.Permanent)]
    [InlineData(OcrProviderFailureKind.InvalidDocument)]
    [InlineData(OcrProviderFailureKind.UnsupportedFormat)]
    public async Task DeterministicProviderPreservesFailureClassification(OcrProviderFailureKind kind)
    {
        IOcrProvider provider = new DeterministicOcrProvider(kind);

        var exception = await Assert.ThrowsAsync<OcrProviderException>(() =>
            provider.ExtractAsync(CreateProviderRequest(), CancellationToken.None));

        Assert.Equal(kind, exception.Kind);
    }

    [Fact]
    public async Task DeterministicProviderHonorsCancellation()
    {
        IOcrProvider provider = new DeterministicOcrProvider();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.ExtractAsync(CreateProviderRequest(), cancellation.Token));
    }

    [Fact]
    public void ProviderResultRejectsInvalidConfidenceAndOversizedDiagnostics()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OcrExtractedField.Create("field", "value", 1.01m));
        Assert.Throws<ArgumentOutOfRangeException>(() => OcrProviderResult.Create(
            OcrDocumentType.Other,
            [OcrExtractedField.Create("field", "value", 0.5m)],
            "request-1",
            null,
            null,
            new string('x', 2_001)));
    }

    [Fact]
    public async Task DeterministicContentReaderDoesNotReadClientPathsOrUrls()
    {
        IDocumentContentReader reader = new DeterministicDocumentContentReader(_policy);
        var request = new DocumentContentRequest(
            Guid.CreateVersion7(),
            "objects/tenant/invoice.pdf",
            "invoice.pdf",
            "application/pdf",
            1_024);

        var content = await reader.ReadAsync(request, CancellationToken.None);

        Assert.False(content.Bytes.IsEmpty);
        Assert.Equal("application/pdf", content.MimeType);
    }

    private static OcrProviderRequest CreateProviderRequest()
    {
        return new OcrProviderRequest(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "invoice.pdf",
            "application/pdf",
            OcrDocumentType.CommercialInvoice,
            DocumentContent.Create([1, 2, 3], "application/pdf", 1));
    }
}

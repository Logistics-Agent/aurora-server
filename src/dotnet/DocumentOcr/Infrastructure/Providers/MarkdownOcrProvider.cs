using System.Text;
using DocumentOcr.Application.Providers;
using DocumentOcr.Domain.Enums;

namespace DocumentOcr.Infrastructure.Providers;

public sealed class MarkdownOcrProvider(IOcrProvider fallbackProvider) : IOcrProvider
{
    public string Name => "markdown";

    public Task<OcrProviderResult> ExtractAsync(
        OcrProviderRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(request.MimeType, "text/markdown", StringComparison.OrdinalIgnoreCase))
            return fallbackProvider.ExtractAsync(request, cancellationToken);

        string fullText;
        try
        {
            fullText = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(request.Content.Bytes.Span);
        }
        catch (DecoderFallbackException exception)
        {
            throw new OcrProviderException(
                OcrProviderFailureKind.InvalidDocument,
                "INVALID_MARKDOWN_ENCODING",
                $"Markdown content is not valid UTF-8: {exception.Message}");
        }

        if (string.IsNullOrWhiteSpace(fullText))
        {
            throw new OcrProviderException(
                OcrProviderFailureKind.InvalidDocument,
                "EMPTY_MARKDOWN_DOCUMENT",
                "Markdown content is empty.");
        }

        var detectedType = request.DocumentTypeHint == OcrDocumentType.Unspecified
            ? OcrDocumentType.Other
            : request.DocumentTypeHint;
        var result = OcrProviderResult.Create(
            detectedType,
            [OcrExtractedField.Create("full_text_length", fullText.Length.ToString(), 0.99m)],
            $"markdown-{request.JobId:N}",
            null,
            null,
            "Provider: markdown",
            fullText,
            request.ExtractionMode);
        return Task.FromResult(result);
    }
}

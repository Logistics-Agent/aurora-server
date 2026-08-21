using DocumentOcr.Application.Providers;
using DocumentOcr.Domain.Enums;

namespace DocumentOcr.Infrastructure.Providers;

public sealed class DeterministicOcrProvider(OcrProviderFailureKind? failureKind = null) : IOcrProvider
{
    public string Name => "deterministic";

    public Task<OcrProviderResult> ExtractAsync(
        OcrProviderRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.JobId == Guid.Empty || request.TenantId == Guid.Empty)
            throw new ArgumentException("JobId and TenantId are required.", nameof(request));
        if (failureKind == OcrProviderFailureKind.Cancelled)
            throw new OperationCanceledException("Deterministic provider cancellation.", cancellationToken);
        if (failureKind is not null)
            throw new OcrProviderException(failureKind.Value, "deterministic_failure", "Deterministic provider failure.");

        var detectedType = request.DocumentTypeHint == OcrDocumentType.Unspecified
            ? OcrDocumentType.Other
            : request.DocumentTypeHint;
        var result = OcrProviderResult.Create(
            detectedType,
            [
                OcrExtractedField.Create("documentNumber", "DOC-001", 0.99m),
                OcrExtractedField.Create("documentDate", "2026-07-22", 0.97m)
            ],
            $"det-{request.JobId:N}",
            $"text/{request.JobId:N}",
            $"layout/{request.JobId:N}",
            null);
        return Task.FromResult(result);
    }
}

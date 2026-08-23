namespace DocumentOcr.Application.Providers;

public sealed class DocumentProcessingOptions
{
    public const string SectionName = "DocumentProcessing";

    public long MaxDocumentBytes { get; init; } = 10 * 1024 * 1024;
    public int MaxPages { get; init; } = 50;
    public decimal ReviewConfidenceThreshold { get; init; } = 0.85m;
    public string Provider { get; init; } = "AiGovernance";
    public string[] SupportedMimeTypes { get; init; } =
    [
        "application/pdf", "image/jpeg", "image/png", "image/tiff"
    ];
    public string[] SupportedExtensions { get; init; } =
    [
        ".pdf", ".jpg", ".jpeg", ".png", ".tif", ".tiff"
    ];

    public void Validate()
    {
        if (MaxDocumentBytes <= 0)
            throw new InvalidOperationException("DocumentProcessing:MaxDocumentBytes must be positive.");
        if (MaxPages <= 0)
            throw new InvalidOperationException("DocumentProcessing:MaxPages must be positive.");
        if (ReviewConfidenceThreshold is < 0 or > 1)
            throw new InvalidOperationException("DocumentProcessing:ReviewConfidenceThreshold must be between 0 and 1.");
        if (!string.Equals(Provider, "Deterministic", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(Provider, "AiGovernance", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("DocumentProcessing:Provider must be 'AiGovernance' or 'Deterministic'.");
        if (SupportedMimeTypes.Length == 0 || SupportedExtensions.Length == 0)
            throw new InvalidOperationException("DocumentProcessing supported types cannot be empty.");
    }
}

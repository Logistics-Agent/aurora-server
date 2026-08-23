using DocumentOcr.Domain.Enums;

namespace DocumentOcr.Application.Providers;

public sealed record DocumentContentRequest(
    Guid TenantId,
    string StorageReference,
    string FileName,
    string MimeType,
    long DeclaredSizeBytes);

public sealed record OcrProviderRequest(
    Guid JobId,
    Guid TenantId,
    string FileName,
    string MimeType,
    OcrDocumentType DocumentTypeHint,
    DocumentContent Content,
    OcrExtractionMode ExtractionMode = OcrExtractionMode.Structured,
    string? StorageReference = null);

public sealed class DocumentContent
{
    private DocumentContent(byte[] bytes, string mimeType, int pageCount)
    {
        Bytes = bytes;
        MimeType = mimeType;
        PageCount = pageCount;
    }

    public ReadOnlyMemory<byte> Bytes { get; }
    public string MimeType { get; }
    public int PageCount { get; }

    public static DocumentContent Create(byte[] bytes, string mimeType, int pageCount)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0)
            throw new ArgumentException("Document content is required.", nameof(bytes));
        if (string.IsNullOrWhiteSpace(mimeType))
            throw new ArgumentException("MimeType is required.", nameof(mimeType));
        if (pageCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageCount));
        return new DocumentContent(bytes.ToArray(), mimeType.Trim().ToLowerInvariant(), pageCount);
    }
}

public sealed record OcrExtractedField
{
    private OcrExtractedField(string name, string value, decimal confidence)
    {
        Name = name;
        Value = value;
        Confidence = confidence;
    }

    public string Name { get; }
    public string Value { get; }
    public decimal Confidence { get; }

    public static OcrExtractedField Create(string name, string value, decimal confidence)
    {
        return new OcrExtractedField(
            RequiredText(name, nameof(name), 100),
            RequiredText(value, nameof(value), 4_000),
            ValidConfidence(confidence, nameof(confidence)));
    }

    internal static string RequiredText(string? value, string parameterName, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        if (normalized.Length > maxLength)
            throw new ArgumentOutOfRangeException(parameterName, $"{parameterName} cannot exceed {maxLength} characters.");
        return normalized;
    }

    internal static string? OptionalText(string? value, string parameterName, int maxLength)
    {
        return string.IsNullOrWhiteSpace(value) ? null : RequiredText(value, parameterName, maxLength);
    }

    internal static decimal ValidConfidence(decimal confidence, string parameterName)
    {
        if (confidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(parameterName, $"{parameterName} must be between 0 and 1.");
        return confidence;
    }
}

public sealed class OcrProviderResult
{
    private OcrProviderResult(
        OcrDocumentType detectedDocumentType,
        IReadOnlyList<OcrExtractedField> fields,
        string providerRequestId,
        string? textReference,
        string? layoutReference,
        string? diagnostics,
        string? fullTextContent = null,
        OcrExtractionMode extractionMode = OcrExtractionMode.Structured)
    {
        DetectedDocumentType = detectedDocumentType;
        Fields = fields;
        ProviderRequestId = providerRequestId;
        TextReference = textReference;
        LayoutReference = layoutReference;
        Diagnostics = diagnostics;
        FullTextContent = fullTextContent;
        ExtractionMode = extractionMode;
    }

    public OcrDocumentType DetectedDocumentType { get; }
    public IReadOnlyList<OcrExtractedField> Fields { get; }
    public string ProviderRequestId { get; }
    public string? TextReference { get; }
    public string? LayoutReference { get; }
    public string? Diagnostics { get; }
    public string? FullTextContent { get; }
    public OcrExtractionMode ExtractionMode { get; }

    public static OcrProviderResult Create(
        OcrDocumentType detectedDocumentType,
        IReadOnlyCollection<OcrExtractedField> fields,
        string providerRequestId,
        string? textReference,
        string? layoutReference,
        string? diagnostics,
        string? fullTextContent = null,
        OcrExtractionMode extractionMode = OcrExtractionMode.Structured)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if (detectedDocumentType == OcrDocumentType.Unspecified || !Enum.IsDefined(detectedDocumentType))
            throw new ArgumentOutOfRangeException(nameof(detectedDocumentType));
        if (fields.Count == 0 && string.IsNullOrWhiteSpace(fullTextContent))
            throw new ArgumentException("Provider result must contain fields or full text content.");

        return new OcrProviderResult(
            detectedDocumentType,
            fields.ToArray(),
            OcrExtractedField.RequiredText(providerRequestId, nameof(providerRequestId), 200),
            OcrExtractedField.OptionalText(textReference, nameof(textReference), 1_000),
            OcrExtractedField.OptionalText(layoutReference, nameof(layoutReference), 1_000),
            OcrExtractedField.OptionalText(diagnostics, nameof(diagnostics), 2_000),
            fullTextContent,
            extractionMode);
    }
}

public enum OcrProviderFailureKind
{
    Transient = 1,
    Permanent = 2,
    InvalidDocument = 3,
    UnsupportedFormat = 4,
    Cancelled = 5
}

public sealed class OcrProviderException : Exception
{
    public OcrProviderException(OcrProviderFailureKind kind, string code, string message)
        : base(OcrExtractedField.RequiredText(message, nameof(message), 2_000))
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        Kind = kind;
        Code = OcrExtractedField.RequiredText(code, nameof(code), 100);
    }

    public OcrProviderFailureKind Kind { get; }
    public string Code { get; }
}

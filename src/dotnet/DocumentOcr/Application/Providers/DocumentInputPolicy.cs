namespace DocumentOcr.Application.Providers;

public sealed class DocumentInputPolicy
{
    private readonly DocumentProcessingOptions _options;
    private readonly HashSet<string> _mimeTypes;
    private readonly HashSet<string> _extensions;

    public DocumentInputPolicy(DocumentProcessingOptions options)
    {
        options.Validate();
        _options = options;
        _mimeTypes = options.SupportedMimeTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _extensions = options.SupportedExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public void ValidateMetadata(
        string storageReference,
        string fileName,
        string mimeType,
        long declaredSizeBytes)
    {
        ValidateStorageReference(storageReference);
        var normalizedFileName = RequiredText(fileName, nameof(fileName), 255);
        var normalizedMimeType = RequiredText(mimeType, nameof(mimeType), 150);
        if (!_extensions.Contains(Path.GetExtension(normalizedFileName)))
            throw new ArgumentException("File extension is not supported.", nameof(fileName));
        if (!_mimeTypes.Contains(normalizedMimeType))
            throw new ArgumentException("MIME type is not supported.", nameof(mimeType));
        if (declaredSizeBytes <= 0 || declaredSizeBytes > _options.MaxDocumentBytes)
            throw new ArgumentOutOfRangeException(nameof(declaredSizeBytes), "Document size is outside the allowed range.");
    }

    public void ValidateContent(DocumentContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Bytes.Length > _options.MaxDocumentBytes)
            throw new ArgumentOutOfRangeException(nameof(content), "Document content exceeds the byte limit.");
        if (content.PageCount > _options.MaxPages)
            throw new ArgumentOutOfRangeException(nameof(content), "Document content exceeds the page limit.");
        if (!_mimeTypes.Contains(content.MimeType))
            throw new ArgumentException("Document content MIME type is not supported.", nameof(content));
    }

    private static void ValidateStorageReference(string storageReference)
    {
        var value = RequiredText(storageReference, nameof(storageReference), 1_000);
        if (!value.StartsWith("objects/", StringComparison.Ordinal) ||
            value.Contains("://", StringComparison.Ordinal) ||
            value.Contains('\\') ||
            Path.IsPathRooted(value) ||
            value.Split('/').Any(segment => segment is "" or "." or ".."))
        {
            throw new ArgumentException("Storage reference must be an approved object key.", nameof(storageReference));
        }
    }

    private static string RequiredText(string? value, string parameterName, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        if (normalized.Length > maxLength)
            throw new ArgumentOutOfRangeException(parameterName);
        return normalized;
    }
}

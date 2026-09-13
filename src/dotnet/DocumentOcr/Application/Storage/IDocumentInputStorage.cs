using System.Text;

namespace DocumentOcr.Application.Storage;

public interface IDocumentInputStorage
{
    Task<SignedWriteTarget> CreateSignedWriteTargetAsync(
        Guid tenantId,
        Guid uploadId,
        string objectKey,
        string fileName,
        string mimeType,
        long maximumSizeBytes,
        DateTimeOffset expiresAt,
        string? contentSha256,
        CancellationToken cancellationToken = default);

    Task<DocumentObjectMetadata?> HeadAsync(
        Guid tenantId,
        string objectKey,
        CancellationToken cancellationToken = default);

    Task WriteAsync(
        Guid tenantId,
        string objectKey,
        Stream content,
        long maximumSizeBytes,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid tenantId,
        string objectKey,
        CancellationToken cancellationToken = default);
}

public interface IDocumentDownloadStorage
{
    Task<SignedReadTarget> CreateSignedReadTargetAsync(
        Guid tenantId,
        string objectKey,
        string fileName,
        string contentType,
        DateTimeOffset expiresAt,
        CancellationToken cancellationToken = default);
}

public sealed record SignedWriteTarget(
    string Url,
    IReadOnlyDictionary<string, string> RequiredHeaders,
    DateTimeOffset ExpiresAt,
    long MaximumSizeBytes);

public sealed record SignedReadTarget(
    string Url,
    DateTimeOffset ExpiresAt);

public sealed record DocumentObjectMetadata(
    string ObjectKey,
    string ContentType,
    long SizeBytes,
    string? ContentSha256);

public sealed class DocumentInputTooLargeException : Exception;

public static class DocumentObjectInspector
{
    public const long MaximumVerifiedBytes = 10 * 1024 * 1024;

    public static async Task<DocumentObjectMetadata> InspectAsync(
        string objectKey,
        Stream content,
        CancellationToken cancellationToken = default,
        string? observedContentType = null)
    {
        using var hash = System.Security.Cryptography.IncrementalHash.CreateHash(
            System.Security.Cryptography.HashAlgorithmName.SHA256);
        var header = new byte[8];
        var headerLength = 0;
        var buffer = new byte[81_920];
        var isMarkdownCandidate = string.Equals(Path.GetExtension(objectKey), ".md", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(observedContentType, "text/markdown", StringComparison.OrdinalIgnoreCase);
        await using var textBuffer = isMarkdownCandidate ? new MemoryStream() : null;
        long sizeBytes = 0;

        while (true)
        {
            var read = await content.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                break;
            sizeBytes += read;
            if (sizeBytes > MaximumVerifiedBytes)
            {
                return new DocumentObjectMetadata(
                    objectKey, "application/octet-stream", sizeBytes, null);
            }

            hash.AppendData(buffer, 0, read);
            if (textBuffer is not null)
                await textBuffer.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            var remainingHeader = header.Length - headerLength;
            if (remainingHeader > 0)
            {
                var copied = Math.Min(remainingHeader, read);
                Buffer.BlockCopy(buffer, 0, header, headerLength, copied);
                headerLength += copied;
            }
        }

        var detectedMimeType = DetectMimeType(header.AsSpan(0, headerLength));
        if (detectedMimeType == "application/octet-stream" && textBuffer is not null && IsValidUtf8(textBuffer))
            detectedMimeType = "text/markdown";

        return new DocumentObjectMetadata(
            objectKey,
            detectedMimeType,
            sizeBytes,
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

    private static bool IsValidUtf8(MemoryStream content)
    {
        try
        {
            var text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(content.ToArray());
            return text.Length > 0 && text.All(character =>
                !char.IsControl(character) || character is '\r' or '\n' or '\t');
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static string DetectMimeType(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith("%PDF-"u8))
            return "application/pdf";
        if (header.Length >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return "image/jpeg";
        if (header.SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return "image/png";
        if (header.Length >= 4 &&
            ((header[0] == 0x49 && header[1] == 0x49 && header[2] == 0x2A && header[3] == 0x00) ||
             (header[0] == 0x4D && header[1] == 0x4D && header[2] == 0x00 && header[3] == 0x2A)))
        {
            return "image/tiff";
        }

        return "application/octet-stream";
    }
}

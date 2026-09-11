using System.Security.Cryptography;
using DocumentOcr.Application.Storage;
using Microsoft.Extensions.Configuration;

namespace DocumentOcr.Infrastructure.Storage;

public sealed class FileSystemDocumentInputStorage : IDocumentInputStorage
{
    private readonly string _baseDirectory;

    public FileSystemDocumentInputStorage(IConfiguration configuration)
    {
        var configuredPath = configuration["Storage:InputPath"];
        _baseDirectory = Path.GetFullPath(!string.IsNullOrWhiteSpace(configuredPath)
            ? configuredPath
            : Path.Combine(AppContext.BaseDirectory, "storage", "inputs"));
        Directory.CreateDirectory(_baseDirectory);
    }

    public Task<SignedWriteTarget> CreateSignedWriteTargetAsync(
        Guid tenantId,
        Guid uploadId,
        string objectKey,
        string fileName,
        string mimeType,
        long maximumSizeBytes,
        DateTimeOffset expiresAt,
        string? contentSha256,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(tenantId, uploadId, objectKey);
        var path = GetPath(tenantId, objectKey);
        var directory = Path.GetDirectoryName(path);
        if (directory is not null)
            Directory.CreateDirectory(directory);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = mimeType
        };
        if (contentSha256 is not null)
            headers["x-content-sha256"] = contentSha256;

        return Task.FromResult(new SignedWriteTarget(
            new Uri(path).AbsoluteUri,
            headers,
            expiresAt,
            maximumSizeBytes));
    }

    public Task<DocumentObjectMetadata?> HeadAsync(
        Guid tenantId,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateKeyPrefix(tenantId, objectKey);
        var path = GetPath(tenantId, objectKey);
        if (!File.Exists(path))
            return Task.FromResult<DocumentObjectMetadata?>(null);

        var info = new FileInfo(path);
        return Task.FromResult<DocumentObjectMetadata?>(new DocumentObjectMetadata(
            objectKey,
            ContentTypeFor(Path.GetExtension(info.Name)),
            info.Length,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant()));
    }

    public Task DeleteAsync(
        Guid tenantId,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        ValidateKeyPrefix(tenantId, objectKey);
        var path = GetPath(tenantId, objectKey);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private string GetPath(Guid tenantId, string objectKey)
    {
        ValidateKeyPrefix(tenantId, objectKey);
        var relative = objectKey.Replace('/', Path.DirectorySeparatorChar);
        var path = Path.GetFullPath(Path.Combine(_baseDirectory, relative));
        var root = _baseDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? _baseDirectory
            : _baseDirectory + Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.Ordinal))
            throw new ArgumentException("Object key escapes the input storage root.", nameof(objectKey));
        return path;
    }

    private static void ValidateKey(Guid tenantId, Guid uploadId, string objectKey)
    {
        if (uploadId == Guid.Empty)
            throw new ArgumentException("UploadId is required.", nameof(uploadId));
        ValidateKeyPrefix(tenantId, objectKey);
        if (!objectKey.StartsWith($"objects/{tenantId}/{uploadId}/", StringComparison.Ordinal))
            throw new ArgumentException("Object key does not match the upload session.", nameof(objectKey));
    }

    private static void ValidateKeyPrefix(Guid tenantId, string objectKey)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(objectKey) ||
            !objectKey.StartsWith($"objects/{tenantId}/", StringComparison.Ordinal) ||
            objectKey.Contains("..", StringComparison.Ordinal) ||
            objectKey.Contains('\\') ||
            objectKey.Split('/').Any(string.IsNullOrEmpty))
            throw new ArgumentException("Object key is not tenant safe.", nameof(objectKey));
    }

    private static string ContentTypeFor(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".tif" or ".tiff" => "image/tiff",
        _ => "application/octet-stream"
    };
}

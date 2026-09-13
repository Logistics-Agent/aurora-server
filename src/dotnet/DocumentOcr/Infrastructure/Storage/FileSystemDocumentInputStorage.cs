using System.Security.Cryptography;
using DocumentOcr.Application.Storage;
using DocumentOcr.Application.Uploads;
using Microsoft.Extensions.Configuration;

namespace DocumentOcr.Infrastructure.Storage;

public sealed class FileSystemDocumentInputStorage : IDocumentInputStorage
{
    private readonly string _baseDirectory;
    private readonly Uri _publicBaseUri;
    private readonly DocumentUploadBridgeTokenService _tokenService;

    public FileSystemDocumentInputStorage(IConfiguration configuration)
    {
        var configuredPath = configuration["Storage:InputPath"];
        _baseDirectory = Path.GetFullPath(!string.IsNullOrWhiteSpace(configuredPath)
            ? configuredPath
            : Path.Combine(AppContext.BaseDirectory, "storage", "inputs"));
        var bridgeOptions = DocumentUploadBridgeOptions.FromConfiguration(configuration);
        _publicBaseUri = bridgeOptions.GetPublicBaseUri();
        _tokenService = new DocumentUploadBridgeTokenService(bridgeOptions);
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
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Content-Type"] = mimeType
        };

        return Task.FromResult(new SignedWriteTarget(
            CreateBridgeTargetUrl(tenantId, uploadId, objectKey, expiresAt),
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

        return InspectAsync(path, objectKey, cancellationToken);
    }

    public async Task WriteAsync(
        Guid tenantId,
        string objectKey,
        Stream content,
        long maximumSizeBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ValidateKeyPrefix(tenantId, objectKey);
        var path = GetPath(tenantId, objectKey);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $".{Guid.NewGuid():N}.upload");

        try
        {
            await using var destination = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81_920, useAsync: true);
            var buffer = new byte[81_920];
            long written = 0;
            while (true)
            {
                var read = await content.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                    break;
                written += read;
                if (written > maximumSizeBytes)
                    throw new DocumentInputTooLargeException();
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }

            await destination.FlushAsync(cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
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
        if (!objectKey.StartsWith($"tenants/{tenantId}/documents/{uploadId}/", StringComparison.Ordinal) &&
            !objectKey.StartsWith($"objects/{tenantId}/{uploadId}/", StringComparison.Ordinal))
            throw new ArgumentException("Object key does not match the upload session.", nameof(objectKey));
    }

    private static void ValidateKeyPrefix(Guid tenantId, string objectKey)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(objectKey) ||
            (!objectKey.StartsWith($"tenants/{tenantId}/documents/", StringComparison.Ordinal) &&
             !objectKey.StartsWith($"objects/{tenantId}/", StringComparison.Ordinal)) ||
            objectKey.Contains("..", StringComparison.Ordinal) ||
            objectKey.Contains('\\') ||
            objectKey.Split('/').Any(string.IsNullOrEmpty))
            throw new ArgumentException("Object key is not tenant safe.", nameof(objectKey));
    }

    private static async Task<DocumentObjectMetadata?> InspectAsync(
        string path,
        string objectKey,
        CancellationToken cancellationToken)
    {
        await using var content = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920, useAsync: true);
        return await DocumentObjectInspector.InspectAsync(objectKey, content, cancellationToken);
    }

    private string CreateBridgeTargetUrl(
        Guid tenantId,
        Guid uploadId,
        string objectKey,
        DateTimeOffset expiresAt)
    {
        var token = _tokenService.CreateToken(tenantId, uploadId, objectKey, expiresAt);
        var relativePath = $"api/internal/document-uploads/{tenantId:N}/{uploadId:N}?token={Uri.EscapeDataString(token)}";
        return new Uri(_publicBaseUri, relativePath).AbsoluteUri;
    }
}

using System.Security.Cryptography;
using DocumentOcr.Application.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DocumentOcr.Infrastructure.Storage;

public sealed class FileSystemArtifactStorageService : IArtifactStorageService
{
    private readonly string _baseDirectory;
    private readonly ILogger<FileSystemArtifactStorageService> _logger;

    public FileSystemArtifactStorageService(
        IConfiguration configuration,
        ILogger<FileSystemArtifactStorageService>? logger = null)
    {
        _logger = logger ?? NullLogger<FileSystemArtifactStorageService>.Instance;
        var configuredPath = configuration["Storage:ArtifactPath"];
        _baseDirectory = !string.IsNullOrWhiteSpace(configuredPath)
            ? configuredPath
            : Path.Combine(AppContext.BaseDirectory, "storage", "artifacts");
        Directory.CreateDirectory(_baseDirectory);
    }

    public async Task<ArtifactStorageResult> StoreArtifactAsync(
        Guid tenantId,
        Guid jobId,
        string fileName,
        string contentType,
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (jobId == Guid.Empty) throw new ArgumentException("JobId is required.", nameof(jobId));

        var relativePath = Path.Combine(tenantId.ToString(), jobId.ToString(), fileName);
        var fullPath = Path.Combine(_baseDirectory, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllBytesAsync(fullPath, data, cancellationToken);

        var sha256 = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        var artifactRef = $"ocr-artifacts/{tenantId}/{jobId}/{fileName}";

        _logger.LogInformation("Artifact stored: {Ref}, size: {Size} bytes, sha256: {Hash}",
            artifactRef, data.Length, sha256);

        return new ArtifactStorageResult(
            artifactRef,
            sha256,
            data.Length,
            DateTimeOffset.UtcNow);
    }

    public async Task<byte[]?> ReadArtifactAsync(
        string artifactReference,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artifactReference)) return null;

        // Strip prefix "ocr-artifacts/"
        var relative = artifactReference.StartsWith("ocr-artifacts/", StringComparison.OrdinalIgnoreCase)
            ? artifactReference["ocr-artifacts/".Length..]
            : artifactReference;

        var fullPath = Path.Combine(_baseDirectory, relative);
        if (!File.Exists(fullPath))
        {
            _logger.LogWarning("Artifact file not found: {Path}", fullPath);
            return null;
        }

        return await File.ReadAllBytesAsync(fullPath, cancellationToken);
    }
}

using Microsoft.Extensions.Configuration;

namespace DocumentOcr.Application.Uploads;

public enum DocumentInputStorageProvider
{
    FileSystem,
    S3
}

public sealed record S3DocumentInputStorageSettings(
    string Bucket,
    string ServiceUrl,
    string Region,
    string AccessKey,
    string SecretKey);

public static class DocumentInputStorageConfiguration
{
    public static DocumentInputStorageProvider GetProvider(IConfiguration configuration) =>
        (configuration["Storage:InputProvider"] ?? "FileSystem").Trim() switch
        {
            var provider when provider.Equals("FileSystem", StringComparison.OrdinalIgnoreCase) =>
                DocumentInputStorageProvider.FileSystem,
            var provider when provider.Equals("S3", StringComparison.OrdinalIgnoreCase) =>
                DocumentInputStorageProvider.S3,
            _ => throw new InvalidOperationException(
                "Storage:InputProvider must be either 'FileSystem' or 'S3'.")
        };

    public static S3DocumentInputStorageSettings GetS3Settings(IConfiguration configuration)
    {
        var settings = new S3DocumentInputStorageSettings(
            Required(configuration, "Storage:S3:Bucket"),
            Required(configuration, "Storage:S3:ServiceUrl"),
            Required(configuration, "Storage:S3:Region"),
            Required(configuration, "Storage:S3:AccessKey"),
            Required(configuration, "Storage:S3:SecretKey"));
        if (!Uri.TryCreate(settings.ServiceUrl, UriKind.Absolute, out _))
            throw new InvalidOperationException("Storage:S3:ServiceUrl must be an absolute URL.");
        return settings;
    }

    private static string Required(IConfiguration configuration, string key) =>
        string.IsNullOrWhiteSpace(configuration[key])
            ? throw new InvalidOperationException($"{key} is required for S3 input storage.")
            : configuration[key]!;
}

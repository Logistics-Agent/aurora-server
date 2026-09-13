using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace DocumentOcr.Application.Uploads;

public sealed record DocumentUploadBridgeCorsOptions(IReadOnlyList<string> AllowedOrigins)
{
    public static DocumentUploadBridgeCorsOptions FromConfiguration(IConfiguration configuration)
    {
        var configuredOrigins = configuration["Storage:InputBridge:AllowedOrigins"]
            ?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            ?? [];
        return new DocumentUploadBridgeCorsOptions(configuredOrigins);
    }

    public string[] GetValidatedOrigins()
    {
        if (AllowedOrigins.Count == 0)
            throw new InvalidOperationException("Storage:InputBridge:AllowedOrigins must not be empty.");

        return AllowedOrigins.Select(NormalizeOrigin).Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string NormalizeOrigin(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment) ||
            uri.AbsolutePath is not "/")
        {
            throw new InvalidOperationException(
                "Storage:InputBridge:AllowedOrigins must contain absolute HTTP(S) origins without paths.");
        }

        return uri.GetLeftPart(UriPartial.Authority);
    }
}

public static class DocumentUploadBridgeCorsPolicy
{
    public const string Name = "DocumentUploadBridge";

    public static void Configure(CorsOptions options, DocumentUploadBridgeCorsOptions bridgeOptions)
    {
        var origins = bridgeOptions.GetValidatedOrigins();
        options.AddPolicy(Name, policy => policy
            .WithOrigins(origins)
            .WithMethods(HttpMethods.Put, HttpMethods.Options)
            .WithHeaders("Content-Type"));
    }
}

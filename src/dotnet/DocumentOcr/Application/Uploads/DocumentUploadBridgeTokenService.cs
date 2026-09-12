using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace DocumentOcr.Application.Uploads;

public sealed record DocumentUploadBridgeOptions(string PublicBaseUrl, string SigningKey)
{
    public static DocumentUploadBridgeOptions FromConfiguration(IConfiguration configuration) => new(
        configuration["Storage:InputBridge:PublicBaseUrl"] ?? string.Empty,
        configuration["Storage:InputBridge:SigningKey"] ?? string.Empty);

    public Uri GetPublicBaseUri()
    {
        if (!Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException(
                "Storage:InputBridge:PublicBaseUrl must be an absolute HTTP(S) URL.");
        }

        return uri.ToString().EndsWith("/", StringComparison.Ordinal) ? uri : new Uri($"{uri}/");
    }

    public byte[] GetSigningKey()
    {
        var key = Encoding.UTF8.GetBytes(SigningKey);
        if (key.Length < 32)
            throw new InvalidOperationException("Storage:InputBridge:SigningKey must be at least 32 bytes.");
        return key;
    }
}

public enum UploadBridgeTokenValidation
{
    Valid,
    Invalid,
    Expired
}

public sealed record UploadBridgeTokenPayload(
    Guid TenantId,
    Guid UploadId,
    string ObjectKey,
    long ExpiresAtUnixSeconds,
    string Nonce);

public sealed class DocumentUploadBridgeTokenService(DocumentUploadBridgeOptions options)
{
    private readonly byte[] _signingKey = options.GetSigningKey();

    public string CreateToken(Guid tenantId, Guid uploadId, string objectKey, DateTimeOffset expiresAt)
    {
        var payload = new UploadBridgeTokenPayload(
            tenantId,
            uploadId,
            objectKey,
            expiresAt.ToUnixTimeSeconds(),
            Guid.NewGuid().ToString("N"));
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        var signature = HMACSHA256.HashData(_signingKey, payloadBytes);
        return $"{ToBase64Url(payloadBytes)}.{ToBase64Url(signature)}";
    }

    public UploadBridgeTokenValidation Validate(
        string? token,
        Guid tenantId,
        Guid uploadId,
        DateTimeOffset now,
        out UploadBridgeTokenPayload? payload)
    {
        payload = null;
        var parts = token?.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts is not [var encodedPayload, var encodedSignature])
            return UploadBridgeTokenValidation.Invalid;

        try
        {
            var payloadBytes = FromBase64Url(encodedPayload);
            var expectedSignature = HMACSHA256.HashData(_signingKey, payloadBytes);
            var signature = FromBase64Url(encodedSignature);
            if (!CryptographicOperations.FixedTimeEquals(expectedSignature, signature))
                return UploadBridgeTokenValidation.Invalid;

            payload = JsonSerializer.Deserialize<UploadBridgeTokenPayload>(payloadBytes);
            if (payload is null || payload.TenantId != tenantId || payload.UploadId != uploadId ||
                payload.TenantId == Guid.Empty || payload.UploadId == Guid.Empty ||
                string.IsNullOrWhiteSpace(payload.ObjectKey))
            {
                return UploadBridgeTokenValidation.Invalid;
            }

            return payload.ExpiresAtUnixSeconds <= now.ToUnixTimeSeconds()
                ? UploadBridgeTokenValidation.Expired
                : UploadBridgeTokenValidation.Valid;
        }
        catch (FormatException)
        {
            return UploadBridgeTokenValidation.Invalid;
        }
        catch (JsonException)
        {
            return UploadBridgeTokenValidation.Invalid;
        }
    }

    private static string ToBase64Url(byte[] value) => Convert.ToBase64String(value)
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}

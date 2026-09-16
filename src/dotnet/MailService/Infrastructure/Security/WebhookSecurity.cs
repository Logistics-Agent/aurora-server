using System;
using System.Security.Cryptography;
using System.Text;

namespace MailService.Infrastructure.Security;

public static class WebhookSecurity
{
    public static bool VerifyStalwartHmac(
        byte[] rawBodyBytes, 
        string? headerSignature, 
        string secret)
    {
        if (rawBodyBytes == null || rawBodyBytes.Length == 0)
            return false;

        if (string.IsNullOrWhiteSpace(headerSignature) || string.IsNullOrWhiteSpace(secret))
            return false;

        var cleanedSignature = headerSignature.Trim();
        if (cleanedSignature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            cleanedSignature = cleanedSignature[7..].Trim();
        }

        byte[] expectedBytes;
        try
        {
            if (cleanedSignature.Length == 64 && IsHexString(cleanedSignature))
            {
                expectedBytes = Convert.FromHexString(cleanedSignature);
            }
            else
            {
                expectedBytes = Convert.FromBase64String(cleanedSignature);
            }
        }
        catch (FormatException)
        {
            return false;
        }

        // Compute HMAC-SHA256 on entire raw body byte array
        byte[] keyBytes = Encoding.UTF8.GetBytes(secret);
        using var hmac = new HMACSHA256(keyBytes);
        byte[] computedHash = hmac.ComputeHash(rawBodyBytes);

        // Constant-Time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(computedHash, expectedBytes);
    }

    public static bool VerifyCloudflareHmac(
        byte[] rawBodyBytes,
        string? timestampStr,
        string? headerSignature,
        string secret,
        TimeSpan? maxTimestampDrift = null)
    {
        if (rawBodyBytes == null || rawBodyBytes.Length == 0)
            return false;

        if (string.IsNullOrWhiteSpace(timestampStr) || string.IsNullOrWhiteSpace(headerSignature) || string.IsNullOrWhiteSpace(secret))
            return false;

        // 1. Verify timestamp drift (reject replay / stale requests, default 5 minutes)
        if (!long.TryParse(timestampStr.Trim(), out long unixSeconds))
            return false;

        var requestTime = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        var drift = DateTimeOffset.UtcNow - requestTime;
        var maxDrift = maxTimestampDrift ?? TimeSpan.FromMinutes(5);

        if (Math.Abs(drift.TotalSeconds) > maxDrift.TotalSeconds)
            return false;

        // 2. Clean and parse expected signature
        var cleanedSignature = headerSignature.Trim();
        if (cleanedSignature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            cleanedSignature = cleanedSignature[7..].Trim();
        }

        byte[] expectedBytes;
        try
        {
            if (cleanedSignature.Length == 64 && IsHexString(cleanedSignature))
            {
                expectedBytes = Convert.FromHexString(cleanedSignature);
            }
            else
            {
                expectedBytes = Convert.FromBase64String(cleanedSignature);
            }
        }
        catch (FormatException)
        {
            return false;
        }

        // 3. Compute HMAC-SHA256 over (timestamp + "." + rawBody)
        byte[] keyBytes = Encoding.UTF8.GetBytes(secret);
        using var hmac = new HMACSHA256(keyBytes);

        byte[] prefixBytes = Encoding.UTF8.GetBytes(timestampStr.Trim() + ".");
        byte[] combined = new byte[prefixBytes.Length + rawBodyBytes.Length];
        Buffer.BlockCopy(prefixBytes, 0, combined, 0, prefixBytes.Length);
        Buffer.BlockCopy(rawBodyBytes, 0, combined, prefixBytes.Length, rawBodyBytes.Length);

        byte[] computedHash = hmac.ComputeHash(combined);

        // 4. Constant-Time comparison
        return CryptographicOperations.FixedTimeEquals(computedHash, expectedBytes);
    }

    private static bool IsHexString(string input)
    {
        foreach (var c in input)
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return false;
        }
        return true;
    }
}

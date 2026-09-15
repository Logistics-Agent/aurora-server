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

using System;
using System.Security.Cryptography;
using System.Text;

namespace MailService.Infrastructure.Security;

public static class WebhookSecurity
{
    public static bool VerifyStalwartHmac(
        byte[] rawBodyBytes, 
        string? base64HeaderSignature, 
        string secret)
    {
        if (rawBodyBytes == null || rawBodyBytes.Length == 0)
            return false;

        if (string.IsNullOrWhiteSpace(base64HeaderSignature) || string.IsNullOrWhiteSpace(secret))
            return false;

        // 1. Base64 decode the header signature
        byte[] expectedBytes;
        try
        {
            var cleanedSignature = base64HeaderSignature.Trim();
            if (cleanedSignature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            {
                cleanedSignature = cleanedSignature[7..];
            }
            expectedBytes = Convert.FromBase64String(cleanedSignature);
        }
        catch (FormatException)
        {
            return false;
        }

        // 2. Compute HMAC-SHA256 on entire raw body byte array
        byte[] keyBytes = Encoding.UTF8.GetBytes(secret);
        using var hmac = new HMACSHA256(keyBytes);
        byte[] computedHash = hmac.ComputeHash(rawBodyBytes);

        // 3. Constant-Time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(computedHash, expectedBytes);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MimeKit;
using MailService.Application.Interfaces.Security;

namespace MailService.Infrastructure.Security.Dkim;

public enum DkimStatus
{
    Pass,
    Fail,
    None,
    TempError,
    PermError
}

public record DkimVerificationResult(
    DkimStatus Status,
    string? Selector = null,
    string? Domain = null,
    string? Details = null
)
{
    public bool IsPass => Status == DkimStatus.Pass;

    public static DkimVerificationResult Pass(string selector, string domain) =>
        new(DkimStatus.Pass, selector, domain, "Cryptographic signature verified successfully.");

    public static DkimVerificationResult Fail(string? selector, string? domain, string reason) =>
        new(DkimStatus.Fail, selector, domain, reason);

    public static DkimVerificationResult None(string explanation) =>
        new(DkimStatus.None, null, null, explanation);

    public static DkimVerificationResult PermError(string error) =>
        new(DkimStatus.PermError, null, null, error);

    public static DkimVerificationResult TempError(string error) =>
        new(DkimStatus.TempError, null, null, error);
}

public class DkimVerifier
{
    /// <summary>
    /// Cryptographically verifies DKIM signature (RFC 6376) on the EML bytes using RSA public key from DNS TXT record.
    /// Detects message body and header tampering.
    /// </summary>
    public DkimVerificationResult Verify(byte[] emlBytes, string? dkimPublicKey)
    {
        if (emlBytes == null || emlBytes.Length == 0)
            return DkimVerificationResult.None("No EML content provided");

        if (string.IsNullOrWhiteSpace(dkimPublicKey))
            return DkimVerificationResult.None("No DKIM public key record found");

        try
        {
            using var stream = new MemoryStream(emlBytes);
            var message = MimeMessage.Load(stream);

            // Find DKIM-Signature header
            var dkimHeader = message.Headers.FirstOrDefault(h => h.Field.Equals("DKIM-Signature", StringComparison.OrdinalIgnoreCase));
            if (dkimHeader == null)
                return DkimVerificationResult.None("No DKIM-Signature header present");

            // Parse DKIM-Signature tags
            var dkimTags = ParseDkimTags(dkimHeader.Value);

            if (!dkimTags.TryGetValue("s", out var selector) || string.IsNullOrWhiteSpace(selector))
                selector = "default";

            if (!dkimTags.TryGetValue("d", out var domain) || string.IsNullOrWhiteSpace(domain))
                return DkimVerificationResult.PermError("DKIM-Signature missing required domain tag (d=)");

            if (!dkimTags.TryGetValue("a", out var algorithm) || !algorithm.Equals("rsa-sha256", StringComparison.OrdinalIgnoreCase))
                return DkimVerificationResult.PermError($"Unsupported or missing DKIM algorithm (a={algorithm})");

            if (!dkimTags.TryGetValue("b", out var signatureBase64) || string.IsNullOrWhiteSpace(signatureBase64))
                return DkimVerificationResult.PermError("DKIM-Signature missing signature tag (b=)");

            dkimTags.TryGetValue("bh", out var expectedBh);
            dkimTags.TryGetValue("h", out var signedHeaders);

            // Parse DKIM DNS Public Key (p=)
            var pubKeyTags = ParseDkimTags(dkimPublicKey);
            if (!pubKeyTags.TryGetValue("p", out var pValue) || string.IsNullOrWhiteSpace(pValue))
                return DkimVerificationResult.PermError("DKIM DNS record missing public key (p=)");

            // Parse RSA Public Key
            byte[] keyBytes = Convert.FromBase64String(pValue.Replace(" ", "").Replace("\r", "").Replace("\n", ""));
            using var rsa = RSA.Create();
            try
            {
                rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
            }
            catch (Exception ex)
            {
                return DkimVerificationResult.PermError($"Invalid RSA public key format: {ex.Message}");
            }

            // Verify Body Hash (bh=)
            if (!string.IsNullOrEmpty(expectedBh))
            {
                string bodyContent = message.TextBody ?? message.HtmlBody ?? string.Empty;
                byte[] canonicalBody = CanonicalizeBodyRelaxed(bodyContent);
                using var sha256 = SHA256.Create();
                byte[] computedHash = sha256.ComputeHash(canonicalBody);
                string computedBh = Convert.ToBase64String(computedHash);

                if (!string.Equals(expectedBh.Trim(), computedBh.Trim(), StringComparison.Ordinal))
                {
                    return DkimVerificationResult.Fail(selector, domain,
                        $"Body hash verification failed (expected: {expectedBh}, computed: {computedBh}). Message body was modified/tampered.");
                }
            }

            // Verify Header Signature (b=)
            byte[] headerData = BuildCanonicalizedHeaderData(message, signedHeaders, dkimHeader);
            byte[] signatureBytes = Convert.FromBase64String(signatureBase64.Replace(" ", "").Replace("\r", "").Replace("\n", ""));

            bool signatureValid = rsa.VerifyData(headerData, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (!signatureValid)
            {
                return DkimVerificationResult.Fail(selector, domain,
                    "Cryptographic RSA signature verification failed. Headers or signing key mismatch (tampering detected).");
            }

            return DkimVerificationResult.Pass(selector, domain);
        }
        catch (Exception ex)
        {
            return DkimVerificationResult.Fail(null, null, $"DKIM verification exception: {ex.Message}");
        }
    }

    private static Dictionary<string, string> ParseDkimTags(string tagString)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // Unfold and clean tag string
        string unfolded = Regex.Replace(tagString, @"\r?\n[ \t]+", " ");
        foreach (var part in unfolded.Split(';'))
        {
            var kv = part.Trim().Split('=', 2);
            if (kv.Length == 2)
            {
                dict[kv[0].Trim()] = kv[1].Trim();
            }
        }
        return dict;
    }

    private static byte[] CanonicalizeBodyRelaxed(string body)
    {
        // Relaxed body canonicalization: reduce multiple spaces to one, remove trailing whitespace and blank lines
        var lines = body.Replace("\r\n", "\n").Split('\n');
        var canonicalLines = lines.Select(line => Regex.Replace(line.TrimEnd(), @"[ \t]+", " ")).ToList();

        // Remove trailing empty lines
        while (canonicalLines.Count > 0 && string.IsNullOrEmpty(canonicalLines[^1]))
        {
            canonicalLines.RemoveAt(canonicalLines.Count - 1);
        }

        string result = string.Join("\r\n", canonicalLines);
        if (result.Length > 0) result += "\r\n";
        return Encoding.UTF8.GetBytes(result);
    }

    private static string CanonicalizeHeaderValueRelaxed(string value)
    {
        // RFC 6376 Section 3.4.2 Relaxed Header Canonicalization
        // Unfold multi-line folded headers into single space and collapse internal WSP
        string unfolded = Regex.Replace(value, @"\r?\n[ \t]+", " ");
        string collapsed = Regex.Replace(unfolded.Trim(), @"[ \t]+", " ");
        return collapsed;
    }

    public static byte[] BuildCanonicalizedHeaderData(MimeMessage message, string? signedHeaderList, Header dkimHeader)

    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(signedHeaderList))
        {
            var headerNames = signedHeaderList.Split(':');
            foreach (var hName in headerNames)
            {
                var h = message.Headers.FirstOrDefault(x => x.Field.Equals(hName.Trim(), StringComparison.OrdinalIgnoreCase));
                if (h != null)
                {
                    string canonicalVal = CanonicalizeHeaderValueRelaxed(h.Value);
                    sb.Append($"{h.Field.ToLowerInvariant().Trim()}:{canonicalVal}\r\n");
                }
            }
        }

        // Append DKIM-Signature header without the b= signature value itself
        string dkimHeaderRelaxed = Regex.Replace(dkimHeader.Value, @"b=[^;]+", "b=");
        string canonicalDkim = CanonicalizeHeaderValueRelaxed(dkimHeaderRelaxed);
        sb.Append($"dkim-signature:{canonicalDkim}");

        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}

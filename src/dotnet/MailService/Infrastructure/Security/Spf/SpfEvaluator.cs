using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MailService.Application.Interfaces.Security;

namespace MailService.Infrastructure.Security.Spf;

public enum SpfStatus
{
    Pass,
    Fail,
    SoftFail,
    Neutral,
    None,
    TempError,
    PermError
}

public record SpfEvaluationResult(
    SpfStatus Status,
    string Mechanism,
    string? Explanation = null,
    int DnsLookupCount = 0
)
{
    public static SpfEvaluationResult Pass(string mechanism, int lookups) =>
        new(SpfStatus.Pass, mechanism, DnsLookupCount: lookups);

    public static SpfEvaluationResult Fail(string mechanism, int lookups) =>
        new(SpfStatus.Fail, mechanism, DnsLookupCount: lookups);

    public static SpfEvaluationResult SoftFail(string mechanism, int lookups) =>
        new(SpfStatus.SoftFail, mechanism, DnsLookupCount: lookups);

    public static SpfEvaluationResult Neutral(string mechanism, int lookups) =>
        new(SpfStatus.Neutral, mechanism, DnsLookupCount: lookups);

    public static SpfEvaluationResult None(string explanation) =>
        new(SpfStatus.None, "none", Explanation: explanation);

    public static SpfEvaluationResult PermError(string explanation, int lookups = 0) =>
        new(SpfStatus.PermError, "error", Explanation: explanation, DnsLookupCount: lookups);

    public static SpfEvaluationResult TempError(string explanation, int lookups = 0) =>
        new(SpfStatus.TempError, "error", Explanation: explanation, DnsLookupCount: lookups);
}

public class SpfEvaluator
{
    public const int MaxDnsLookups = 10;

    /// <summary>
    /// Synchronous evaluator for basic records (ip4/ip6/all)
    /// </summary>
    public SpfEvaluationResult Evaluate(string senderDomain, string? spfRecord, string clientIp)
    {
        int lookupCount = 1;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return EvaluateInternal(senderDomain, spfRecord, clientIp, null, ref lookupCount, visited);
    }

    /// <summary>
    /// Asynchronous evaluator supporting recursive include: mechanisms with RFC 7208 10-lookup protection
    /// </summary>
    public async Task<SpfEvaluationResult> EvaluateAsync(
        string senderDomain,
        string? spfRecord,
        string clientIp,
        IDnsLookupService dnsLookup,
        CancellationToken cancellationToken = default)
    {
        int lookupCount = 1;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return await EvaluateInternalAsync(senderDomain, spfRecord, clientIp, dnsLookup, lookupCount, visited, cancellationToken);
    }

    private SpfEvaluationResult EvaluateInternal(
        string domain,
        string? spfRecord,
        string clientIp,
        IDnsLookupService? dnsLookup,
        ref int lookupCount,
        HashSet<string> visitedDomains)
    {
        if (string.IsNullOrWhiteSpace(spfRecord))
            return SpfEvaluationResult.None("No SPF record published");

        if (visitedDomains.Contains(domain))
            return SpfEvaluationResult.PermError($"Circular SPF include detected for domain {domain}", lookupCount);

        visitedDomains.Add(domain);

        var tokens = spfRecord.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0 || !tokens[0].Equals("v=spf1", StringComparison.OrdinalIgnoreCase))
            return SpfEvaluationResult.None("Invalid SPF record header");

        if (!IPAddress.TryParse(clientIp, out var parsedIp))
            return SpfEvaluationResult.PermError($"Invalid client IP: {clientIp}", lookupCount);

        foreach (var rawToken in tokens.Skip(1))
        {
            var (qualifier, mechanism) = ParseQualifierAndMechanism(rawToken);

            if (mechanism.StartsWith("ip4:", StringComparison.OrdinalIgnoreCase))
            {
                string ipRange = mechanism[4..];
                if (IsIpMatch(parsedIp, ipRange))
                    return ResultFromQualifier(qualifier, rawToken, lookupCount);
            }
            else if (mechanism.StartsWith("ip6:", StringComparison.OrdinalIgnoreCase))
            {
                string ipRange = mechanism[4..];
                if (IsIpMatch(parsedIp, ipRange))
                    return ResultFromQualifier(qualifier, rawToken, lookupCount);
            }
            else if (mechanism.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return ResultFromQualifier(qualifier, rawToken, lookupCount);
            }
        }

        return SpfEvaluationResult.Neutral("No mechanism matched", lookupCount);
    }

    private async Task<SpfEvaluationResult> EvaluateInternalAsync(
        string domain,
        string? spfRecord,
        string clientIp,
        IDnsLookupService dnsLookup,
        int currentLookups,
        HashSet<string> visitedDomains,
        CancellationToken cancellationToken)
    {
        if (currentLookups > MaxDnsLookups)
            return SpfEvaluationResult.PermError($"SPF DNS lookup limit ({MaxDnsLookups}) exceeded", currentLookups);

        if (string.IsNullOrWhiteSpace(spfRecord))
            return SpfEvaluationResult.None("No SPF record published");

        if (visitedDomains.Contains(domain))
            return SpfEvaluationResult.PermError($"Circular SPF include detected for domain {domain}", currentLookups);

        visitedDomains.Add(domain);

        var tokens = spfRecord.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0 || !tokens[0].Equals("v=spf1", StringComparison.OrdinalIgnoreCase))
            return SpfEvaluationResult.None("Invalid SPF record header");

        if (!IPAddress.TryParse(clientIp, out var parsedIp))
            return SpfEvaluationResult.PermError($"Invalid client IP: {clientIp}", currentLookups);

        foreach (var rawToken in tokens.Skip(1))
        {
            var (qualifier, mechanism) = ParseQualifierAndMechanism(rawToken);

            if (mechanism.StartsWith("ip4:", StringComparison.OrdinalIgnoreCase))
            {
                string ipRange = mechanism[4..];
                if (IsIpMatch(parsedIp, ipRange))
                    return ResultFromQualifier(qualifier, rawToken, currentLookups);
            }
            else if (mechanism.StartsWith("ip6:", StringComparison.OrdinalIgnoreCase))
            {
                string ipRange = mechanism[4..];
                if (IsIpMatch(parsedIp, ipRange))
                    return ResultFromQualifier(qualifier, rawToken, currentLookups);
            }
            else if (mechanism.StartsWith("include:", StringComparison.OrdinalIgnoreCase))
            {
                string includedDomain = mechanism[8..];
                currentLookups++;
                if (currentLookups > MaxDnsLookups)
                    return SpfEvaluationResult.PermError($"SPF DNS lookup limit ({MaxDnsLookups}) exceeded on include {includedDomain}", currentLookups);

                string? includedSpf = await dnsLookup.GetSpfRecordAsync(includedDomain, cancellationToken);
                var includeResult = await EvaluateInternalAsync(
                    includedDomain,
                    includedSpf,
                    clientIp,
                    dnsLookup,
                    currentLookups,
                    new HashSet<string>(visitedDomains, StringComparer.OrdinalIgnoreCase),
                    cancellationToken);

                if (includeResult.Status == SpfStatus.Pass)
                    return ResultFromQualifier(qualifier, rawToken, includeResult.DnsLookupCount);
                if (includeResult.Status == SpfStatus.PermError)
                    return includeResult;
            }
            else if (mechanism.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return ResultFromQualifier(qualifier, rawToken, currentLookups);
            }
        }

        return SpfEvaluationResult.Neutral("No mechanism matched", currentLookups);
    }

    private static (char Qualifier, string Mechanism) ParseQualifierAndMechanism(string token)
    {
        char first = token[0];
        if (first is '+' or '-' or '~' or '?')
        {
            return (first, token[1..]);
        }
        return ('+', token);
    }

    private static SpfEvaluationResult ResultFromQualifier(char qualifier, string mechanism, int lookups) => qualifier switch
    {
        '+' => SpfEvaluationResult.Pass(mechanism, lookups),
        '-' => SpfEvaluationResult.Fail(mechanism, lookups),
        '~' => SpfEvaluationResult.SoftFail(mechanism, lookups),
        '?' => SpfEvaluationResult.Neutral(mechanism, lookups),
        _ => SpfEvaluationResult.Neutral(mechanism, lookups)
    };

    private static bool IsIpMatch(IPAddress clientIp, string mechanism)
    {
        if (mechanism.Contains('/'))
        {
            var parts = mechanism.Split('/');
            if (IPAddress.TryParse(parts[0], out var networkIp) && int.TryParse(parts[1], out int prefixLength))
            {
                return IsInSubnet(clientIp, networkIp, prefixLength);
            }
        }
        else if (IPAddress.TryParse(mechanism, out var matchIp))
        {
            return clientIp.Equals(matchIp);
        }
        return false;
    }

    private static bool IsInSubnet(IPAddress address, IPAddress subnet, int prefixLength)
    {
        var addressBytes = address.GetAddressBytes();
        var subnetBytes = subnet.GetAddressBytes();
        if (addressBytes.Length != subnetBytes.Length) return false;

        int fullBytes = prefixLength / 8;
        int remainingBits = prefixLength % 8;

        for (int i = 0; i < fullBytes; i++)
        {
            if (addressBytes[i] != subnetBytes[i]) return false;
        }

        if (remainingBits > 0 && fullBytes < addressBytes.Length)
        {
            byte mask = (byte)(0xFF << (8 - remainingBits));
            if ((addressBytes[fullBytes] & mask) != (subnetBytes[fullBytes] & mask))
            {
                return false;
            }
        }

        return true;
    }
}

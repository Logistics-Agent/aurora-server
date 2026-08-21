using System.Net;

namespace MailService.Infrastructure.Security.Spf;

public class SpfEvaluator
{
    public string Evaluate(string senderDomain, string? spfRecord, string clientIp)
    {
        if (string.IsNullOrWhiteSpace(spfRecord))
        {
            return "None";
        }

        var tokens = spfRecord.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0 || !tokens[0].Equals("v=spf1", StringComparison.OrdinalIgnoreCase))
        {
            return "None";
        }

        // Evaluate IP against mechanisms
        if (IPAddress.TryParse(clientIp, out var parsedIp))
        {
            foreach (var token in tokens.Skip(1))
            {
                if (token.StartsWith("ip4:", StringComparison.OrdinalIgnoreCase))
                {
                    string ipRange = token.Substring(4);
                    if (IsIpMatch(parsedIp, ipRange)) return "Pass";
                }
                else if (token.StartsWith("ip6:", StringComparison.OrdinalIgnoreCase))
                {
                    string ipRange = token.Substring(4);
                    if (IsIpMatch(parsedIp, ipRange)) return "Pass";
                }
                else if (token.Equals("+all", StringComparison.OrdinalIgnoreCase))
                {
                    return "Pass";
                }
                else if (token.Equals("-all", StringComparison.OrdinalIgnoreCase))
                {
                    return "Fail";
                }
                else if (token.Equals("~all", StringComparison.OrdinalIgnoreCase))
                {
                    return "SoftFail";
                }
                else if (token.Equals("?all", StringComparison.OrdinalIgnoreCase))
                {
                    return "Neutral";
                }
            }
        }

        if (spfRecord.Contains("-all")) return "Fail";
        if (spfRecord.Contains("~all")) return "SoftFail";
        return "Neutral";
    }

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

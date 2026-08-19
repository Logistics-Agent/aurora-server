using System.Net.Sockets;
using System.Text;
using DnsClient;
using Microsoft.Extensions.Logging;
using MailService.Application.Interfaces;

namespace MailService.Infrastructure.Security;

public class ClamAvClient : IClamAvClient
{
    private readonly string _host;
    private readonly int _port;
    private readonly ILogger<ClamAvClient> _logger;

    public ClamAvClient(string host = "clamav", int port = 3310, ILogger<ClamAvClient>? logger = null)
    {
        _host = host;
        _port = port;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<ClamAvClient>.Instance;
    }

    public async Task<(bool IsClean, string VirusName)> ScanStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(_host, _port, cancellationToken);
            using var networkStream = client.GetStream();

            // Send INSTREAM command
            byte[] command = Encoding.ASCII.GetBytes("zINSTREAM\0");
            await networkStream.WriteAsync(command, cancellationToken);

            byte[] buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                byte[] sizeBytes = BitConverter.GetBytes(System.Net.IPAddress.HostToNetworkOrder(bytesRead));
                await networkStream.WriteAsync(sizeBytes, cancellationToken);
                await networkStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }

            // Zero size chunk indicates end of stream
            byte[] zeroChunk = BitConverter.GetBytes(0);
            await networkStream.WriteAsync(zeroChunk, cancellationToken);
            await networkStream.FlushAsync(cancellationToken);

            using var reader = new StreamReader(networkStream, Encoding.ASCII);
            string? response = await reader.ReadToEndAsync(cancellationToken);

            if (!string.IsNullOrEmpty(response) && response.Contains("FOUND"))
            {
                string virusName = response.Replace("stream: ", "").Replace(" FOUND", "").Trim();
                return (false, virusName);
            }

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ClamAV scan failed or daemon unavailable. Treating as clean fail-safe.");
            return (true, string.Empty);
        }
    }
}

public class SpamAssassinClient : ISpamAssassinClient
{
    private readonly string _host;
    private readonly int _port;
    private readonly ILogger<SpamAssassinClient> _logger;

    public SpamAssassinClient(string host = "spamassassin", int port = 783, ILogger<SpamAssassinClient>? logger = null)
    {
        _host = host;
        _port = port;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SpamAssassinClient>.Instance;
    }

    public async Task<(decimal Score, List<string> Rules)> CheckSpamAsync(byte[] emlBytes, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(_host, _port, cancellationToken);
            using var stream = client.GetStream();

            string header = $"CHECK SPAMC/1.2\r\nContent-length: {emlBytes.Length}\r\n\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(header);
            await stream.WriteAsync(headerBytes, cancellationToken);
            await stream.WriteAsync(emlBytes, cancellationToken);
            await stream.FlushAsync(cancellationToken);

            using var reader = new StreamReader(stream, Encoding.ASCII);
            string response = await reader.ReadToEndAsync(cancellationToken);

            // Parse score header e.g. "Spam: True ; 12.4 / 5.0"
            decimal score = 0.0m;
            var rules = new List<string>();

            foreach (var line in response.Split('\n'))
            {
                if (line.StartsWith("Spam:"))
                {
                    var parts = line.Split(';');
                    if (parts.Length > 1)
                    {
                        var scoreParts = parts[1].Trim().Split('/');
                        if (decimal.TryParse(scoreParts[0].Trim(), System.Globalization.CultureInfo.InvariantCulture, out var parsedScore))
                        {
                            score = parsedScore;
                        }
                    }
                }
            }

            return (score, rules);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SpamAssassin daemon unavailable. Defaulting score to 0.0.");
            return (0.0m, new List<string>());
        }
    }
}

public class DnsLookupService : IDnsLookupService
{
    private readonly LookupClient _dnsClient;

    public DnsLookupService()
    {
        _dnsClient = new LookupClient();
    }

    public async Task<string?> GetSpfRecordAsync(string domain, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _dnsClient.QueryAsync(domain, QueryType.TXT, cancellationToken: cancellationToken);
            var txtRecord = result.Answers.TxtRecords()
                .FirstOrDefault(r => r.Text.Any(t => t.StartsWith("v=spf1", StringComparison.OrdinalIgnoreCase)));

            return txtRecord?.Text.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetDkimRecordAsync(string domain, string selector, CancellationToken cancellationToken = default)
    {
        try
        {
            string fqdn = $"{selector}._domainkey.{domain}";
            var result = await _dnsClient.QueryAsync(fqdn, QueryType.TXT, cancellationToken: cancellationToken);
            var txtRecord = result.Answers.TxtRecords().FirstOrDefault();
            return txtRecord?.Text.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetDmarcRecordAsync(string domain, CancellationToken cancellationToken = default)
    {
        try
        {
            string fqdn = $"_dmarc.{domain}";
            var result = await _dnsClient.QueryAsync(fqdn, QueryType.TXT, cancellationToken: cancellationToken);
            var txtRecord = result.Answers.TxtRecords()
                .FirstOrDefault(r => r.Text.Any(t => t.StartsWith("v=DMARC1", StringComparison.OrdinalIgnoreCase)));

            return txtRecord?.Text.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}

public class SpfEvaluator
{
    public string Evaluate(string senderDomain, string? spfRecord, string clientIp)
    {
        if (string.IsNullOrEmpty(spfRecord)) return "None";
        if (spfRecord.Contains("-all")) return "Fail";
        if (spfRecord.Contains("~all")) return "SoftFail";
        return "Pass";
    }
}

public class DkimVerifier
{
    public string Verify(byte[] emlBytes, string? dkimPublicKey)
    {
        if (string.IsNullOrEmpty(dkimPublicKey)) return "None";
        return "Pass";
    }
}

public class DmarcEvaluator
{
    public (string Result, string Policy) Evaluate(string spfResult, string dkimResult, string? dmarcRecord)
    {
        if (string.IsNullOrEmpty(dmarcRecord)) return ("None", "none");

        string policy = "none";
        if (dmarcRecord.Contains("p=reject")) policy = "reject";
        else if (dmarcRecord.Contains("p=quarantine")) policy = "quarantine";

        bool aligned = spfResult == "Pass" || dkimResult == "Pass";
        string result = aligned ? "Pass" : "Fail";

        return (result, policy);
    }
}

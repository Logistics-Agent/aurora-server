using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MailService.Application.Interfaces.Security;

namespace MailService.Infrastructure.Security.Spam;

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

    public async Task<SpamAssassinScanResult> CheckSpamAsync(byte[] emlBytes, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10)); // 10s timeout

            await client.ConnectAsync(_host, _port, cts.Token);
            using var stream = client.GetStream();

            string header = $"CHECK SPAMC/1.2\r\nContent-length: {emlBytes.Length}\r\n\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(header);
            await stream.WriteAsync(headerBytes, cancellationToken);
            await stream.WriteAsync(emlBytes, cancellationToken);
            await stream.FlushAsync(cancellationToken);

            using var reader = new StreamReader(stream, Encoding.ASCII);
            string response = await reader.ReadToEndAsync(cancellationToken);

            sw.Stop();

            // Parse SPAMC response: "Spam: True ; 12.4 / 5.0"
            decimal score = 0.0m;
            decimal threshold = 5.0m;
            var rules = new List<string>();

            foreach (var line in response.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("Spam:", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split(';');
                    if (parts.Length > 1)
                    {
                        var scoreParts = parts[1].Trim().Split('/');
                        if (scoreParts.Length > 0 && decimal.TryParse(scoreParts[0].Trim(), System.Globalization.CultureInfo.InvariantCulture, out var parsedScore))
                        {
                            score = parsedScore;
                        }
                        if (scoreParts.Length > 1 && decimal.TryParse(scoreParts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture, out var parsedThreshold))
                        {
                            threshold = parsedThreshold;
                        }
                    }
                }
            }

            return SpamAssassinScanResult.ScoredResult(score, threshold, rules, (int)sw.ElapsedMilliseconds);
        }
        catch (SocketException ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "SpamAssassin daemon unavailable at {Host}:{Port}", _host, _port);
            return SpamAssassinScanResult.Unavailable($"SpamAssassin connection failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "SpamAssassin scan error: {Message}", ex.Message);
            return SpamAssassinScanResult.Error(ex.Message);
        }
    }
}

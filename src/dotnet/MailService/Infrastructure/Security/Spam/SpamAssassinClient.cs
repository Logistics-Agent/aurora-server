using System.Net.Sockets;
using System.Text;
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

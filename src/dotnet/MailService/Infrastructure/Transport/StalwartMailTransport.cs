using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MailService.Application.Interfaces.Stalwart;
using MailService.Application.Interfaces.Transport;
using MailService.Application.Options;

namespace MailService.Infrastructure.Transport;

public class StalwartMailTransport : IMailTransport
{
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly bool _useTls;
    private readonly string? _username;
    private readonly string? _password;
    private readonly ILogger<StalwartMailTransport> _logger;

    public string ProviderName => "Stalwart";

    public StalwartMailTransport(
        IConfiguration configuration,
        IOptions<MailServiceOptions>? mailOptions,
        ILogger<StalwartMailTransport> logger)
    {
        _logger = logger;

        var opt = mailOptions?.Value;
        _smtpHost = !string.IsNullOrWhiteSpace(opt?.StalwartSmtpHost) 
            ? opt.StalwartSmtpHost 
            : configuration["Stalwart:SmtpHost"] ?? "stalwart";

        _smtpPort = opt?.StalwartSmtpPort > 0 
            ? opt.StalwartSmtpPort 
            : (int.TryParse(configuration["Stalwart:SmtpPort"], out int port) ? port : 25);

        _useTls = bool.TryParse(configuration["Stalwart:UseTls"], out bool tls) && tls;

        _username = !string.IsNullOrWhiteSpace(opt?.StalwartSmtpUser) 
            ? opt.StalwartSmtpUser 
            : configuration["Stalwart:SmtpUser"];

        _password = !string.IsNullOrWhiteSpace(opt?.StalwartSmtpPassword) 
            ? opt.StalwartSmtpPassword 
            : configuration["Stalwart:SmtpPassword"];
    }

    public async Task<SmtpDeliveryResult> DeliverAsync(
        string senderAddress,
        IReadOnlyList<string> recipientAddresses,
        string subject,
        string bodyText,
        string bodyHtml,
        IReadOnlyList<(string Filename, string ContentType, byte[] Content)> attachments,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new MimeMessage();

            // Safe sender address parsing
            if (MailboxAddress.TryParse(senderAddress, out var senderMailbox))
            {
                message.From.Add(senderMailbox);
            }
            else if (!string.IsNullOrWhiteSpace(senderAddress))
            {
                message.From.Add(new MailboxAddress(senderAddress, senderAddress));
            }
            else
            {
                string defaultSender = $"noreply@{_smtpHost}";
                message.From.Add(new MailboxAddress(defaultSender, defaultSender));
            }

            foreach (var recipient in recipientAddresses)
            {
                if (MailboxAddress.TryParse(recipient, out var mailbox))
                {
                    message.To.Add(mailbox);
                }
                else if (!string.IsNullOrWhiteSpace(recipient))
                {
                    message.To.Add(new MailboxAddress(recipient, recipient));
                }
            }

            if (message.To.Count == 0)
            {
                return SmtpDeliveryResult.Permanent(501, "No valid recipient email addresses provided.");
            }

            message.Subject = subject ?? string.Empty;
            message.Date = DateTimeOffset.UtcNow;

            string fromDomain = "e-verland.site";
            if (senderAddress != null && senderAddress.Contains('@'))
            {
                var parts = senderAddress.Split('@');
                if (parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[^1]))
                {
                    fromDomain = parts[^1].Trim(' ', '>', '"', '\'');
                }
            }
            message.MessageId = $"{Guid.NewGuid():N}@{fromDomain}";

            var builder = new BodyBuilder();
            if (!string.IsNullOrEmpty(bodyText))
            {
                builder.TextBody = bodyText;
            }
            if (!string.IsNullOrEmpty(bodyHtml))
            {
                builder.HtmlBody = bodyHtml;
            }

            if (attachments != null)
            {
                foreach (var (filename, contentType, content) in attachments)
                {
                    if (content != null && content.Length > 0)
                    {
                        ContentType ct;
                        if (!ContentType.TryParse(contentType ?? string.Empty, out ct!))
                        {
                            ct = new ContentType("application", "octet-stream");
                        }
                        builder.Attachments.Add(filename ?? "attachment", content, ct);
                    }
                }
            }

            message.Body = builder.ToMessageBody();

            using var smtpClient = new SmtpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            smtpClient.ServerCertificateValidationCallback = (s, c, h, e) => true;

            var secureOptions = _useTls || _smtpPort == 587 
                ? SecureSocketOptions.StartTls 
                : SecureSocketOptions.None;

            await smtpClient.ConnectAsync(_smtpHost, _smtpPort, secureOptions, cts.Token);

            if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
            {
                await smtpClient.AuthenticateAsync(_username, _password, cts.Token);
            }

            string response = await smtpClient.SendAsync(message, cts.Token);
            await smtpClient.DisconnectAsync(true, cts.Token);

            string? queueId = ExtractQueueId(response);

            _logger.LogInformation("SMTP delivery succeeded via provider Stalwart ({Host}:{Port}). Recipients: {Recipients}, ProviderMessageId: {ProviderMessageId}, Response: {Response}, Status: Success",
                _smtpHost, _smtpPort, string.Join(", ", recipientAddresses), queueId ?? response, response);

            return SmtpDeliveryResult.Success(response, queueId);
        }
        catch (SmtpCommandException ex)
        {
            int code = (int)ex.StatusCode;
            _logger.LogWarning(ex, "Stalwart SMTP command error {StatusCode}: {Message}", code, ex.Message);

            if (code >= 400 && code < 500)
            {
                return SmtpDeliveryResult.Transient(code, ex.Message);
            }

            return SmtpDeliveryResult.Permanent(code, ex.Message);
        }
        catch (SmtpProtocolException ex)
        {
            _logger.LogError(ex, "Stalwart SMTP protocol exception during delivery: {Message}", ex.Message);
            return SmtpDeliveryResult.Uncertain($"SMTP protocol failure: {ex.Message}");
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "Stalwart SMTP socket connection exception to {Host}:{Port}: {Message}", _smtpHost, _smtpPort, ex.Message);
            return SmtpDeliveryResult.Transient(421, $"SMTP socket connection failure: {ex.Message}");
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Stalwart SMTP delivery timeout: {Message}", ex.Message);
            return SmtpDeliveryResult.Uncertain($"SMTP delivery timeout: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected Stalwart SMTP delivery exception: {Message}", ex.Message);
            return SmtpDeliveryResult.Uncertain($"Unexpected delivery failure: {ex.Message}");
        }
    }

    private static string? ExtractQueueId(string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return null;

        var match = Regex.Match(response, @"queued\s+as\s+([A-Za-z0-9\-_]+)", RegexOptions.IgnoreCase);
        if (match.Success) return match.Groups[1].Value;

        match = Regex.Match(response, @"id=([A-Za-z0-9\-_]+)", RegexOptions.IgnoreCase);
        if (match.Success) return match.Groups[1].Value;

        return null;
    }
}

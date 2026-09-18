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

public class BrevoMailTransport : IMailTransport
{
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly string? _username;
    private readonly string? _password;
    private readonly ILogger<BrevoMailTransport> _logger;

    public string ProviderName => "Brevo";

    public BrevoMailTransport(
        IConfiguration configuration,
        IOptions<BrevoOptions>? brevoOptions,
        ILogger<BrevoMailTransport> logger)
    {
        _logger = logger;

        var opt = brevoOptions?.Value;
        _smtpHost = !string.IsNullOrWhiteSpace(opt?.SmtpHost) 
            ? opt.SmtpHost 
            : configuration["Brevo:SmtpHost"] ?? "smtp-relay.brevo.com";

        _smtpPort = opt?.SmtpPort > 0 
            ? opt.SmtpPort 
            : (int.TryParse(configuration["Brevo:SmtpPort"], out int bPort) ? bPort : 587);

        _username = !string.IsNullOrWhiteSpace(opt?.SmtpUsername) 
            ? opt.SmtpUsername 
            : configuration["Brevo:SmtpUsername"] ?? configuration["Brevo:SmtpUser"];

        _password = !string.IsNullOrWhiteSpace(opt?.SmtpPassword) 
            ? opt.SmtpPassword 
            : configuration["Brevo:SmtpPassword"] ?? configuration["Brevo:SmtpKey"];
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
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(senderAddress));

        foreach (var recipient in recipientAddresses)
        {
            if (MailboxAddress.TryParse(recipient, out var mailbox))
            {
                message.To.Add(mailbox);
            }
        }

        message.Subject = subject ?? string.Empty;
        message.Date = DateTimeOffset.UtcNow;
        message.MessageId = $"{Guid.NewGuid():N}@{senderAddress.Split('@')[^1]}";

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
                    builder.Attachments.Add(filename, content, ContentType.Parse(string.IsNullOrEmpty(contentType) ? "application/octet-stream" : contentType));
                }
            }
        }

        message.Body = builder.ToMessageBody();

        using var smtpClient = new SmtpClient();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(30)); // 30s SMTP operation timeout

            // DEMO / Staging TLS fallback
            smtpClient.ServerCertificateValidationCallback = (s, c, h, e) => true;

            var secureOptions = _smtpPort == 465 
                ? SecureSocketOptions.SslOnConnect 
                : SecureSocketOptions.StartTls;

            await smtpClient.ConnectAsync(_smtpHost, _smtpPort, secureOptions, cts.Token);

            if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
            {
                await smtpClient.AuthenticateAsync(_username, _password, cts.Token);
            }

            string response = await smtpClient.SendAsync(message, cts.Token);
            await smtpClient.DisconnectAsync(true, cts.Token);

            string? queueId = ExtractQueueId(response);

            _logger.LogInformation("SMTP delivery succeeded via provider Brevo ({Host}:{Port}). Recipients: {Recipients}, ProviderMessageId: {ProviderMessageId}, Response: {Response}, Status: Success",
                _smtpHost, _smtpPort, string.Join(", ", recipientAddresses), queueId ?? response, response);

            return SmtpDeliveryResult.Success(response, queueId);
        }
        catch (SmtpCommandException ex)
        {
            int code = (int)ex.StatusCode;
            _logger.LogWarning(ex, "Brevo SMTP command error {StatusCode}: {Message}", code, ex.Message);

            if (code >= 400 && code < 500)
            {
                return SmtpDeliveryResult.Transient(code, ex.Message);
            }

            return SmtpDeliveryResult.Permanent(code, ex.Message);
        }
        catch (SmtpProtocolException ex)
        {
            _logger.LogError(ex, "Brevo SMTP protocol exception during delivery: {Message}", ex.Message);
            return SmtpDeliveryResult.Uncertain($"SMTP protocol failure: {ex.Message}");
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "Brevo SMTP socket connection exception to {Host}:{Port}: {Message}", _smtpHost, _smtpPort, ex.Message);
            return SmtpDeliveryResult.Transient(421, $"SMTP socket connection failure: {ex.Message}");
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "Brevo SMTP delivery timeout: {Message}", ex.Message);
            return SmtpDeliveryResult.Uncertain($"SMTP delivery timeout: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected Brevo SMTP delivery exception: {Message}", ex.Message);
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

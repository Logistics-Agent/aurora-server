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
using MimeKit;
using MailService.Application.Interfaces.Stalwart;

namespace MailService.Infrastructure.Stalwart;

public class MailKitSmtpDeliveryService : ISmtpDeliveryService
{
    private readonly string _smtpHost;
    private readonly int _smtpPort;
    private readonly bool _useTls;
    private readonly string? _username;
    private readonly string? _password;
    private readonly ILogger<MailKitSmtpDeliveryService> _logger;

    public MailKitSmtpDeliveryService(IConfiguration configuration, ILogger<MailKitSmtpDeliveryService> logger)
    {
        _smtpHost = configuration["Stalwart:SmtpHost"] ?? "stalwart";
        _smtpPort = int.TryParse(configuration["Stalwart:SmtpPort"], out int port) ? port : 25;
        _useTls = bool.TryParse(configuration["Stalwart:UseTls"], out bool tls) && tls;
        _username = configuration["Stalwart:SmtpUser"];
        _password = configuration["Stalwart:SmtpPassword"];
        _logger = logger;
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

            var secureOptions = _useTls ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None;
            await smtpClient.ConnectAsync(_smtpHost, _smtpPort, secureOptions, cts.Token);

            if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
            {
                await smtpClient.AuthenticateAsync(_username, _password, cts.Token);
            }

            string response = await smtpClient.SendAsync(message, cts.Token);
            await smtpClient.DisconnectAsync(true, cts.Token);

            // Extract Stalwart / RFC 2821 Queue ID if present (e.g. "250 2.0.0 Ok: queued as 4V9dZg6m8bz9")
            string? queueId = ExtractQueueId(response);

            _logger.LogInformation("SMTP delivery succeeded to {Host}:{Port}. Response: {Response}, QueueId: {QueueId}",
                _smtpHost, _smtpPort, response, queueId);

            return SmtpDeliveryResult.Success(response, queueId);
        }
        catch (SmtpCommandException ex)
        {
            int code = (int)ex.StatusCode;
            _logger.LogWarning(ex, "SMTP command error {StatusCode}: {Message}", code, ex.Message);

            if (code >= 400 && code < 500)
            {
                // 4xx Transient failure
                return SmtpDeliveryResult.Transient(code, ex.Message);
            }

            // 5xx Permanent failure
            return SmtpDeliveryResult.Permanent(code, ex.Message);
        }
        catch (SmtpProtocolException ex)
        {
            _logger.LogError(ex, "SMTP protocol exception during delivery: {Message}", ex.Message);
            return SmtpDeliveryResult.Uncertain($"SMTP protocol failure: {ex.Message}");
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex, "SMTP socket connection exception to {Host}:{Port}: {Message}", _smtpHost, _smtpPort, ex.Message);
            return SmtpDeliveryResult.Transient(421, $"SMTP socket connection failure: {ex.Message}");
        }
        catch (TimeoutException ex)
        {
            _logger.LogError(ex, "SMTP delivery timeout: {Message}", ex.Message);
            return SmtpDeliveryResult.Uncertain($"SMTP delivery timeout: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected SMTP delivery exception: {Message}", ex.Message);
            return SmtpDeliveryResult.Uncertain($"Unexpected delivery failure: {ex.Message}");
        }
    }

    private static string? ExtractQueueId(string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return null;

        // Common patterns: "queued as XXXXX", "id=XXXXX", "Ok: queued as 4V9dZg"
        var match = Regex.Match(response, @"queued\s+as\s+([A-Za-z0-9\-_]+)", RegexOptions.IgnoreCase);
        if (match.Success) return match.Groups[1].Value;

        match = Regex.Match(response, @"id=([A-Za-z0-9\-_]+)", RegexOptions.IgnoreCase);
        if (match.Success) return match.Groups[1].Value;

        return null;
    }
}

using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using Notification.Application.Delivery;
using Notification.Domain.Enums;

namespace Notification.Infrastructure.Providers;

public sealed class SmtpEmailOptions
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public bool EnableSsl { get; init; } = true;
    public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = "Aurora";
    public string? Username { get; init; }
    public string? Password { get; init; }
}

public sealed class SmtpEmailNotificationProvider(
    IOptions<SmtpEmailOptions> options) : IEmailNotificationProvider
{
    private readonly SmtpEmailOptions _options = options.Value;

    public NotificationChannel Channel => NotificationChannel.Email;

    public async Task<NotificationDeliveryResult> DeliverAsync(
        NotificationDeliveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(_options.Host)
            || string.IsNullOrWhiteSpace(_options.FromAddress))
        {
            return NotificationDeliveryResult.Failure(
                "SMTP delivery is not configured.",
                false);
        }

        if (string.IsNullOrWhiteSpace(request.RecipientAddress))
            return NotificationDeliveryResult.Failure("Email recipient is required.", false);

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_options.FromAddress, _options.FromName),
                Subject = request.Title,
                Body = request.Body,
                IsBodyHtml = false
            };
            message.To.Add(request.RecipientAddress);

            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.EnableSsl
            };

            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                client.Credentials = new NetworkCredential(
                    _options.Username,
                    _options.Password);
            }

            await client.SendMailAsync(message, cancellationToken);
            return NotificationDeliveryResult.Success(
                $"smtp:{request.NotificationId}");
        }
        catch (SmtpException exception)
        {
            return NotificationDeliveryResult.Failure(
                exception.Message,
                IsTransient(exception.StatusCode));
        }
        catch (FormatException exception)
        {
            return NotificationDeliveryResult.Failure(exception.Message, false);
        }
    }

    private static bool IsTransient(SmtpStatusCode statusCode) =>
        statusCode is SmtpStatusCode.GeneralFailure
            or SmtpStatusCode.MailboxBusy
            or SmtpStatusCode.LocalErrorInProcessing
            or SmtpStatusCode.ServiceNotAvailable
            or SmtpStatusCode.TransactionFailed;
}

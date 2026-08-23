using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MailService.Application.Interfaces.Stalwart;

public enum SmtpDeliveryStatus
{
    Success,             // 250 OK
    TransientFailure,    // 4xx (e.g. 421, 450, 451, 452)
    PermanentFailure,    // 5xx (e.g. 550, 554, 501)
    UncertainFailure     // Timeout / connection abort during or after DATA command
}

public record SmtpDeliveryResult(
    SmtpDeliveryStatus Status,
    int StatusCode,
    string StatusMessage,
    string? QueueId = null
)
{
    public bool IsSuccess => Status == SmtpDeliveryStatus.Success;

    public static SmtpDeliveryResult Success(string statusMessage, string? queueId = null) =>
        new(SmtpDeliveryStatus.Success, 250, statusMessage, queueId);

    public static SmtpDeliveryResult Transient(int statusCode, string message) =>
        new(SmtpDeliveryStatus.TransientFailure, statusCode, message);

    public static SmtpDeliveryResult Permanent(int statusCode, string message) =>
        new(SmtpDeliveryStatus.PermanentFailure, statusCode, message);

    public static SmtpDeliveryResult Uncertain(string message) =>
        new(SmtpDeliveryStatus.UncertainFailure, 0, message);
}

public interface ISmtpDeliveryService
{
    Task<SmtpDeliveryResult> DeliverAsync(
        string senderAddress,
        IReadOnlyList<string> recipientAddresses,
        string subject,
        string bodyText,
        string bodyHtml,
        IReadOnlyList<(string Filename, string ContentType, byte[] Content)> attachments,
        CancellationToken cancellationToken = default);
}

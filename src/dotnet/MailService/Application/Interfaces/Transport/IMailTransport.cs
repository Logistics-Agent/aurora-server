using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MailService.Application.Interfaces.Stalwart;

namespace MailService.Application.Interfaces.Transport;

public interface IMailTransport
{
    string ProviderName { get; }

    Task<SmtpDeliveryResult> DeliverAsync(
        string senderAddress,
        IReadOnlyList<string> recipientAddresses,
        string subject,
        string bodyText,
        string bodyHtml,
        IReadOnlyList<(string Filename, string ContentType, byte[] Content)> attachments,
        CancellationToken cancellationToken = default);
}

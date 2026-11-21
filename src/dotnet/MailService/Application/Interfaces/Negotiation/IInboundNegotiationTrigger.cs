using System;
using System.Threading;
using System.Threading.Tasks;
using MailService.Domain.Enums;

namespace MailService.Application.Interfaces.Negotiation;

public sealed record InboundNegotiationContext(
    Guid TenantId,
    string SenderAddress,
    string Subject,
    string BodyText,
    string MessageId,
    Guid ThreadId,
    EmailCategory Category);

public interface IInboundNegotiationTrigger
{
    Task TryTriggerAsync(InboundNegotiationContext context, CancellationToken cancellationToken = default);
}

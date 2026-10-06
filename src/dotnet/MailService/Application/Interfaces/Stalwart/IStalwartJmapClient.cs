using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shared.Events;

namespace MailService.Application.Interfaces.Stalwart;

public record JmapEmailDto
{
    public string Id { get; init; } = string.Empty;
    public string BlobId { get; init; } = string.Empty;
    public string ThreadId { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string From { get; init; } = string.Empty;
    public List<string> To { get; init; } = new();
    public string? BodyText { get; init; }
    public string? BodyHtml { get; init; }
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
}

public interface IStalwartJmapClient
{
    Task<string> ResolveJmapAccountIdAsync(string accountEmail, CancellationToken cancellationToken = default);
    Task<JmapEmailDto> FetchEmailByWebhookDataAsync(
        MailboxResolutionResult mailbox, 
        InboundEmailWebhookReceivedEvent evt, 
        CancellationToken cancellationToken = default);
    Task<JmapEmailDto?> GetEmailDirectAsync(string jmapAccountId, string emailId, CancellationToken cancellationToken = default);
}

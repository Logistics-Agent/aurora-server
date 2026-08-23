using System;
using System.Collections.Generic;
using MassTransit;

namespace Shared.Events;

[EntityName("inbound_email_received_event")]
public record InboundEmailReceivedEvent
{
    public Guid TenantId { get; init; }
    public Guid MessageId { get; init; }
    public string SenderEmail { get; init; } = string.Empty;
    public List<string> RecipientEmails { get; init; } = [];
    public string Subject { get; init; } = string.Empty;
    public string Classification { get; init; } = string.Empty;
    public DateTime ReceivedAt { get; init; } = DateTime.UtcNow;
}

[EntityName("inbound_email_quarantined_event")]
public record InboundEmailQuarantinedEvent
{
    public Guid TenantId { get; init; }
    public Guid MessageId { get; init; }
    public string SenderEmail { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string ThreatLevel { get; init; } = string.Empty;
    public DateTime QuarantinedAt { get; init; } = DateTime.UtcNow;
}

[EntityName("outbound_email_sent_event")]
public record OutboundEmailSentEvent
{
    public Guid TenantId { get; init; }
    public Guid MessageId { get; init; }
    public Guid? DraftId { get; init; }
    public string SenderEmail { get; init; } = string.Empty;
    public List<string> RecipientEmails { get; init; } = [];
    public string Subject { get; init; } = string.Empty;
    public string StalwartQueueId { get; init; } = string.Empty;
    public DateTime SentAt { get; init; } = DateTime.UtcNow;
}

[EntityName("outbound_email_rejected_event")]
public record OutboundEmailRejectedEvent
{
    public Guid TenantId { get; init; }
    public Guid MessageId { get; init; }
    public string SenderEmail { get; init; } = string.Empty;
    public List<string> RecipientEmails { get; init; } = [];
    public string Subject { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public DateTime RejectedAt { get; init; } = DateTime.UtcNow;
}

[EntityName("send_system_email_command")]
public record SendSystemEmailCommand
{
    public Guid TenantId { get; init; }
    public string SenderEmail { get; init; } = string.Empty;
    public List<string> RecipientEmails { get; init; } = [];
    public string Subject { get; init; } = string.Empty;
    public string BodyText { get; init; } = string.Empty;
    public string BodyHtml { get; init; } = string.Empty;
}

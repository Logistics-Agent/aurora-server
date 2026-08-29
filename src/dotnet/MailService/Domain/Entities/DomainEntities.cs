using Shared.Entity;
using MailService.Domain.Enums;
using ThreadPriority = MailService.Domain.Enums.ThreadPriority;

namespace MailService.Domain.Entities;

public class Domain : TenantAuditableEntity
{
    public string DomainName { get; set; } = string.Empty;
    public DomainStatus Status { get; set; } = DomainStatus.Active;
    public int MaxMailboxCount { get; set; } = 100;
    public int RetentionDays { get; set; } = 365;
    public string? DkimSelector { get; set; } = "aurora-2025";
    public string? DkimTxtRecord { get; set; }
    public string? PreviousDkimSelector { get; set; }
    public DateTimeOffset? DkimOverlapUntil { get; set; }

    // Security & Rate Limit Thresholds
    public decimal SpamTagThreshold { get; set; } = 5.0m;
    public decimal SpamRejectThreshold { get; set; } = 10.0m;
    public decimal PhishingQuarantineThreshold { get; set; } = 0.7m;
    public decimal HeaderForgeryThreshold { get; set; } = 25.0m;
    public int InboundRateLimitPerMinute { get; set; } = 100;
    public int OutboundRateLimitPerHour { get; set; } = 200;

    public ICollection<Mailbox> Mailboxes { get; set; } = new List<Mailbox>();
    public ICollection<Alias> Aliases { get; set; } = new List<Alias>();
}

public class Mailbox : TenantAuditableEntity
{
    public Guid DomainId { get; set; }
    public Domain? Domain { get; set; }
    public string LocalPart { get; set; } = string.Empty;
    public string FullAddress { get; set; } = string.Empty;
    public MailboxStatus Status { get; set; } = MailboxStatus.Active;
    public Guid? UserId { get; set; }
    public string? SourceEventId { get; set; }
}

public class Alias : TenantAuditableEntity
{
    public Guid DomainId { get; set; }
    public Domain? Domain { get; set; }
    public string AliasAddress { get; set; } = string.Empty;
    public List<string> Targets { get; set; } = new();
}

public class EmailThread : TenantAuditableEntity
{
    public Guid MailboxId { get; set; }
    public Mailbox? Mailbox { get; set; }
    public string Subject { get; set; } = string.Empty;
    public List<string> Participants { get; set; } = new();
    public DateTimeOffset LastMessageAt { get; set; } = DateTimeOffset.UtcNow;
    public int MessageCount { get; set; } = 1;
    public int DraftCount { get; set; } = 0;
    public bool HasUnread { get; set; } = false;
    public string? Snippet { get; set; }
    public string? ExternalThreadId { get; set; }

    // MVP Responsibility & Assignment
    public Guid? PrimaryAssigneeUserId { get; set; }
    public DateTimeOffset? AssignedAt { get; set; }
    public ThreadStatus Status { get; set; } = ThreadStatus.Unassigned;
    public ThreadPriority Priority { get; set; } = ThreadPriority.Normal;
    public uint Version { get; set; } = 1;

    public ICollection<ProcessedMessage> Messages { get; set; } = new List<ProcessedMessage>();
    public ICollection<EmailDraft> Drafts { get; set; } = new List<EmailDraft>();
    public ICollection<ThreadAssignmentHistory> AssignmentHistories { get; set; } = new List<ThreadAssignmentHistory>();
}

public class EmailDraft : TenantAuditableEntity
{
    public Guid DraftRootId { get; set; }
    public Guid? ParentRevisionId { get; set; }
    public EmailDraft? ParentRevision { get; set; }
    public int RevisionNumber { get; set; }
    public bool IsLatestRevision { get; set; } = true;
    public DraftSource Source { get; set; } = DraftSource.Manual;
    public DraftStatus Status { get; set; } = DraftStatus.Draft;
    public Guid MailboxId { get; set; }
    public Guid? AssignedStaffId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty; // SHA-256 of normalized Subject+Body

    // Threading & Source Linkage (Human-in-the-Loop MVP)
    public Guid? ThreadId { get; set; }
    public EmailThread? Thread { get; set; }
    public string? ReplyToMessageId { get; set; }
    public string? SourceType { get; set; } // e.g. "NEGOTIATION", "MANUAL"
    public string? SourceId { get; set; }   // e.g. negotiation session ID
    public string? IdempotencyKey { get; set; }
    public List<string> ToRecipients { get; set; } = new();
}

public class ProcessedMessage : TenantAuditableEntity
{
    public string MessageId { get; set; } = string.Empty; // RFC 5322 Message-ID
    public Guid PipelineExecutionId { get; set; }
    public EmailDirection Direction { get; set; }
    public string SenderAddress { get; set; } = string.Empty;
    public List<string> RecipientAddresses { get; set; } = new();
    public string? Subject { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset ProcessedAt { get; set; }
    public EmailCategory EmailCategory { get; set; } = EmailCategory.Unknown;
    public PipelineStatus PipelineStatus { get; set; } = PipelineStatus.Pending;
    public decimal SpamScore { get; set; }
    public decimal PhishingScore { get; set; }
    public bool IsQuarantined { get; set; }
    public string? R2RawEmlPath { get; set; }
    public Guid? AuditId { get; set; }
    public int RetryCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? FinalFailureAt { get; set; }
    public string? StalwartQueueId { get; set; }
    public DraftSource DraftSource { get; set; } = DraftSource.Manual;
    public Guid? FinalDraftRevisionId { get; set; }
    public EmailDraft? FinalDraftRevision { get; set; }

    // Threading & Body Content
    public Guid? ThreadId { get; set; }
    public EmailThread? Thread { get; set; }
    public string? InReplyTo { get; set; }
    public string? References { get; set; }
    public string? BodyText { get; set; }
    public string? BodyHtml { get; set; }
    public Guid? MailboxId { get; set; }
    public Guid? SentByUserId { get; set; } // Authenticated human actor for outbound

    public ICollection<SecurityCheckResult> SecurityCheckResults { get; set; } = new List<SecurityCheckResult>();
}

public class ThreadAssignmentHistory : TenantAuditableEntity
{
    public Guid ThreadId { get; set; }
    public EmailThread? Thread { get; set; }
    public Guid? FromUserId { get; set; }
    public Guid? ToUserId { get; set; }
    public ThreadAssignmentAction Action { get; set; }
    public Guid ActorUserId { get; set; }
    public string? Reason { get; set; }
}

public class SecurityCheckResult : TenantAuditableEntity
{
    public Guid ProcessedMessageId { get; set; }
    public ProcessedMessage? ProcessedMessage { get; set; }
    public SecurityCheckStage Stage { get; set; }
    public string Result { get; set; } = string.Empty; // Pass/Fail/Skip/Error
    public string? DetailJson { get; set; } // structured json
    public int DurationMs { get; set; }
}

public class QuarantineRecord : TenantAuditableEntity
{
    public Guid ProcessedMessageId { get; set; }
    public ProcessedMessage? ProcessedMessage { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string QuarantineReason { get; set; } = string.Empty;
    public DateTimeOffset QuarantinedAt { get; set; }
    public QuarantineStatus Status { get; set; } = QuarantineStatus.Pending;
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset? AutoDeleteAfter { get; set; }
}

public class AuditRecord : TenantAuditableEntity
{
    public Guid ActorId { get; set; }
    public ActorType ActorType { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? ClientIp { get; set; }
    public string Result { get; set; } = string.Empty; // Success / Failure
    public string? DetailJson { get; set; }
    public string? R2AuditPath { get; set; }
}

public class OutboxMessage : BaseEntity
{
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
    public int RetryCount { get; set; }
    public string? Error { get; set; }
}


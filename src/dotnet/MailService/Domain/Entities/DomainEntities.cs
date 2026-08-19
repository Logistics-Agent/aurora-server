using Shared.Entity;
using MailService.Domain.Enums;

namespace MailService.Domain.Entities;

public class Domain : TenantAuditableEntity
{
    public string DomainName {; set; } = string.Empty;
    public DomainStatus Status {; set; } = DomainStatus.Active;
    public int MaxMailboxCount {; set; } = 100;
    public int RetentionDays {; set; } = 365;
    public string? DkimSelector {; set; } = "aurora-2025";
    public string? DkimTxtRecord {; set; }
    public string? PreviousDkimSelector {; set; }
    public DateTimeOffset? DkimOverlapUntil {; set; }

    // Security & Rate Limit Thresholds
    public decimal SpamTagThreshold {; set; } = 5.0m;
    public decimal SpamRejectThreshold {; set; } = 10.0m;
    public decimal PhishingQuarantineThreshold {; set; } = 0.7m;
    public decimal HeaderForgeryThreshold {; set; } = 25.0m;
    public int InboundRateLimitPerMinute {; set; } = 100;
    public int OutboundRateLimitPerHour {; set; } = 200;

    public ICollection<Mailbox> Mailboxes {; set; } = new List<Mailbox>();
    public ICollection<Alias> Aliases {; set; } = new List<Alias>();
}

public class Mailbox : TenantAuditableEntity
{
    public Guid DomainId {; set; }
    public Domain? Domain {; set; }
    public string LocalPart {; set; } = string.Empty;
    public string FullAddress {; set; } = string.Empty;
    public MailboxStatus Status {; set; } = MailboxStatus.Active;
    public Guid? UserId {; set; }
    public string? SourceEventId {; set; }
}

public class Alias : TenantAuditableEntity
{
    public Guid DomainId {; set; }
    public Domain? Domain {; set; }
    public string AliasAddress {; set; } = string.Empty;
    public List<string> Targets {; set; } = new();
}

public class EmailDraft : TenantAuditableEntity
{
    public Guid DraftRootId {; set; }
    public Guid? ParentRevisionId {; set; }
    public EmailDraft? ParentRevision {; set; }
    public int RevisionNumber {; set; }
    public bool IsLatestRevision {; set; } = true;
    public DraftSource Source {; set; } = DraftSource.Manual;
    public DraftStatus Status {; set; } = DraftStatus.Draft;
    public Guid MailboxId {; set; }
    public Guid? AssignedStaffId {; set; }
    public string Subject {; set; } = string.Empty;
    public string Body {; set; } = string.Empty;
    public string ContentHash {; set; } = string.Empty; // SHA-256 of normalized Subject+Body
}

public class ProcessedMessage : TenantAuditableEntity
{
    public string MessageId {; set; } = string.Empty; // RFC 5322 Message-ID
    public Guid PipelineExecutionId {; set; }
    public EmailDirection Direction {; set; }
    public string SenderAddress {; set; } = string.Empty;
    public List<string> RecipientAddresses {; set; } = new();
    public string? Subject {; set; }
    public DateTimeOffset ReceivedAt {; set; }
    public DateTimeOffset ProcessedAt {; set; }
    public EmailCategory EmailCategory {; set; } = EmailCategory.Unknown;
    public PipelineStatus PipelineStatus {; set; } = PipelineStatus.Pending;
    public decimal SpamScore {; set; }
    public decimal PhishingScore {; set; }
    public bool IsQuarantined {; set; }
    public string? R2RawEmlPath {; set; }
    public Guid? AuditId {; set; }
    public int RetryCount {; set; }
    public string? LastError {; set; }
    public DateTimeOffset? FinalFailureAt {; set; }
    public string? StalwartQueueId {; set; }
    public DraftSource DraftSource {; set; } = DraftSource.Manual;
    public Guid? FinalDraftRevisionId {; set; }
    public EmailDraft? FinalDraftRevision {; set; }

    public ICollection<SecurityCheckResult> SecurityCheckResults {; set; } = new List<SecurityCheckResult>();
}

public class SecurityCheckResult : TenantAuditableEntity
{
    public Guid ProcessedMessageId {; set; }
    public ProcessedMessage? ProcessedMessage {; set; }
    public SecurityCheckStage Stage {; set; }
    public string Result {; set; } = string.Empty; // Pass/Fail/Skip/Error
    public string? DetailJson {; set; } // structured json
    public int DurationMs {; set; }
}

public class QuarantineRecord : TenantAuditableEntity
{
    public Guid ProcessedMessageId {; set; }
    public ProcessedMessage? ProcessedMessage {; set; }
    public string MessageId {; set; } = string.Empty;
    public string QuarantineReason {; set; } = string.Empty;
    public DateTimeOffset QuarantinedAt {; set; }
    public QuarantineStatus Status {; set; } = QuarantineStatus.Pending;
    public Guid? ReviewedBy {; set; }
    public DateTimeOffset? ReviewedAt {; set; }
    public DateTimeOffset? AutoDeleteAfter {; set; }
}

public class AuditRecord : TenantAuditableEntity
{
    public Guid ActorId {; set; }
    public ActorType ActorType {; set; }
    public string Action {; set; } = string.Empty;
    public string ResourceType {; set; } = string.Empty;
    public Guid ResourceId {; set; }
    public DateTimeOffset Timestamp {; set; }
    public string? ClientIp {; set; }
    public string Result {; set; } = string.Empty; // Success / Failure
    public string? DetailJson {; set; }
    public string? R2AuditPath {; set; }
}

public class OutboxMessage : BaseEntity
{
    public string EventType {; set; } = string.Empty;
    public string Payload {; set; } = string.Empty;
    public DateTimeOffset CreatedAt {; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt {; set; }
    public int RetryCount {; set; }
    public string? Error {; set; }
}

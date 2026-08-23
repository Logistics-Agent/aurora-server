namespace MailService.Domain.Events;

public record DomainProvisioned(
    Guid DomainId,
    Guid TenantId,
    string DomainName,
    DateTimeOffset ProvisionedAt);

public record MailboxProvisioned(
    Guid MailboxId,
    Guid TenantId,
    string MailboxAddress,
    DateTimeOffset ProvisionedAt,
    string? SourceEventId);

public record DraftCreated(
    Guid DraftId,
    Guid DraftRootId,
    int RevisionNumber,
    Guid TenantId,
    Guid MailboxId,
    Guid? AssignedStaffId,
    DateTimeOffset CreatedAt);

public record MessageQuarantined(
    Guid QuarantineId,
    Guid TenantId,
    string MessageId,
    string Reason,
    DateTimeOffset QuarantinedAt);

public record MessageReleased(
    Guid QuarantineId,
    Guid TenantId,
    string MessageId,
    Guid ReviewedBy,
    DateTimeOffset ReleasedAt);

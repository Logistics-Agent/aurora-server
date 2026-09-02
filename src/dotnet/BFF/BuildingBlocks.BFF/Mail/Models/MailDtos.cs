using System;
using System.Collections.Generic;

namespace BuildingBlocks.BFF.Mail.Models;

// ─── Domain & Mailbox Provisioning DTOs ──────────────────────────────────────

public record ProvisionDomainRequest(
    string DomainName,
    int MaxMailboxCount = 100,
    int RetentionDays = 365);

public record ProvisionDomainResponse(
    string DomainId,
    string DomainName,
    string DkimSelector,
    string DkimTxtRecord,
    DateTimeOffset ProvisionedAt);

public record CreateMailboxRequest(
    string DomainId,
    string LocalPart,
    string? UserId = null);

public record CreateMailboxResponse(
    string MailboxId,
    string FullAddress,
    DateTimeOffset CreatedAt);

public record CreateAliasRequest(
    string DomainId,
    string AliasAddress,
    List<string> TargetAddresses);

public record CreateAliasResponse(
    string AliasId,
    DateTimeOffset CreatedAt);

public record ResetPasswordResponse(
    bool Acknowledged,
    string Message);

// ─── Draft Management DTOs ──────────────────────────────────────────────────

public record CreateDraftRequest(
    string MailboxId,
    string? AssignedStaffId,
    string Subject,
    string Body,
    string? SourceType = null,
    string? SourceId = null,
    string? IdempotencyKey = null,
    List<string>? To = null,
    string? ThreadId = null,
    string? ReplyToMessageId = null);

public record CreateNegotiationMailDraftRequest(
    string MailboxId,
    string? IdempotencyKey = null);

public record DraftResponse(
    string DraftId,
    string DraftRootId,
    int RevisionNumber,
    bool IsLatestRevision,
    string Source,
    string Status,
    string MailboxId,
    string? AssignedStaffId,
    string Subject,
    string Body,
    string ContentHash,
    DateTimeOffset CreatedAt,
    string? SourceType = null,
    string? SourceId = null,
    IReadOnlyList<string>? To = null,
    string? ThreadId = null,
    string? ReplyToMessageId = null,
    bool IsExisting = false);

public record DraftListResponse(
    IReadOnlyList<DraftResponse> Drafts,
    string? NextPageToken);

// ─── Thread Management DTOs (Gmail-Like Threading) ──────────────────────────

public record ThreadMessageResponse(
    string MessageId,
    string Direction,
    string SenderAddress,
    IReadOnlyList<string> RecipientAddresses,
    string Subject,
    string BodyText,
    string BodyPreview,
    string ReplyToMessageId,
    DateTimeOffset ReceivedAt,
    DateTimeOffset SentAt);

public record ThreadAssignmentHistoryResponse(
    string Id,
    string ThreadId,
    string FromUserId,
    string ToUserId,
    string Action,
    string ActorUserId,
    string Reason,
    DateTimeOffset CreatedAt);

public record ThreadAssignmentHistoryListResponse(
    string ThreadId,
    IReadOnlyList<ThreadAssignmentHistoryResponse> History);

public record ClaimThreadResponse(
    bool Success,
    string ThreadId,
    string PrimaryAssigneeUserId,
    DateTimeOffset AssignedAt,
    string Status);

public record ReassignThreadRequest(
    string TargetUserId,
    string? Reason = null);

public record ReassignThreadResponse(
    bool Success,
    string ThreadId,
    string PrimaryAssigneeUserId,
    DateTimeOffset AssignedAt,
    string Status);

public record UnassignThreadRequest(
    string? Reason = null);

public record UnassignThreadResponse(
    bool Success,
    string ThreadId,
    string Status);

public record ThreadResponse(
    string ThreadId,
    string MailboxId,
    string Subject,
    IReadOnlyList<string> Participants,
    IReadOnlyList<ThreadMessageResponse> Messages,
    IReadOnlyList<DraftResponse> Drafts,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? PrimaryAssigneeUserId = null,
    DateTimeOffset? AssignedAt = null,
    string? Status = null,
    string? Priority = null,
    IReadOnlyList<ThreadAssignmentHistoryResponse>? AssignmentHistory = null);

public record ThreadSummaryResponse(
    string ThreadId,
    string MailboxId,
    string Subject,
    IReadOnlyList<string> Participants,
    DateTimeOffset LastMessageAt,
    int MessageCount,
    int DraftCount,
    bool HasUnread,
    string Snippet,
    string? PrimaryAssigneeUserId = null,
    DateTimeOffset? AssignedAt = null,
    string? Status = null,
    string? Priority = null);

public record ThreadListResponse(
    IReadOnlyList<ThreadSummaryResponse> Threads,
    string? NextPageToken,
    bool HasMore = false);

// ─── Outbound Mail Submission DTOs ──────────────────────────────────────────

public record OutboundAttachmentDto(
    string Filename,
    string ContentType,
    string ContentBase64);

public record SubmitOutboundMessageRequest(
    string SenderAddress,
    List<string> RecipientAddresses,
    string Subject,
    string BodyText,
    string BodyHtml,
    List<OutboundAttachmentDto>? Attachments = null,
    string? IdempotencyKey = null,
    string? DraftRootId = null,
    string? ThreadId = null,
    string? ReplyToMessageId = null);

public record SubmitOutboundMessageResponse(
    string ProcessedMessageId,
    string StalwartQueueId,
    DateTimeOffset SubmittedAt);

// ─── Processed Messages DTOs ────────────────────────────────────────────────

public record SecurityCheckResultDto(
    string Stage,
    string Result,
    string DetailJson,
    int DurationMs);

public record ProcessedMessageResponse(
    string ProcessedMessageId,
    string MessageId,
    string Direction,
    string SenderAddress,
    IReadOnlyList<string> RecipientAddresses,
    string Subject,
    DateTimeOffset ReceivedAt,
    DateTimeOffset ProcessedAt,
    string EmailCategory,
    string PipelineStatus,
    double SpamScore,
    double PhishingScore,
    bool IsQuarantined,
    string R2RawEmlPath,
    IReadOnlyList<SecurityCheckResultDto> SecurityChecks);

public record ProcessedMessageListResponse(
    IReadOnlyList<ProcessedMessageResponse> Messages,
    string? NextPageToken);

// ─── Quarantine DTOs ────────────────────────────────────────────────────────

public record QuarantineRecordResponse(
    string QuarantineId,
    string ProcessedMessageId,
    string MessageId,
    string QuarantineReason,
    DateTimeOffset QuarantinedAt,
    string Status,
    string? ReviewedBy,
    DateTimeOffset? ReviewedAt);

public record QuarantineListResponse(
    IReadOnlyList<QuarantineRecordResponse> Records,
    string? NextPageToken);

public record ReleaseQuarantineResponse(
    bool Success,
    DateTimeOffset ReleasedAt);

public record DeleteQuarantineResponse(
    bool Success);

// ─── Audit DTOs ─────────────────────────────────────────────────────────────

public record AuditRecordResponse(
    string AuditId,
    string ActorId,
    string ActorType,
    string Action,
    string ResourceType,
    string ResourceId,
    DateTimeOffset Timestamp,
    string Result,
    string DetailJson);

public record AuditListResponse(
    IReadOnlyList<AuditRecordResponse> Records,
    string? NextPageToken);

// ─── Dead Letter Operations DTOs ────────────────────────────────────────────

public record RequeueDeadLetterResponse(
    bool Success,
    string Message);

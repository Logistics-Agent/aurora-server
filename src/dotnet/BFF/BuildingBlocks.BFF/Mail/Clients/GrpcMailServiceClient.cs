using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using BffModels = BuildingBlocks.BFF.Mail.Models;
using GrpcModels = MailService.GrpcServices;

namespace BuildingBlocks.BFF.Mail.Clients;

public class GrpcMailServiceClient : IMailServiceClient
{
    private readonly GrpcModels.MailManagement.MailManagementClient _managementClient;
    private readonly GrpcModels.MailSecurity.MailSecurityClient _securityClient;

    public GrpcMailServiceClient(
        GrpcModels.MailManagement.MailManagementClient managementClient,
        GrpcModels.MailSecurity.MailSecurityClient securityClient)
    {
        _managementClient = managementClient;
        _securityClient = securityClient;
    }

    public async Task<BffModels.ProvisionDomainResponse> ProvisionDomainAsync(BffModels.ProvisionDomainRequest request, CancellationToken cancellationToken = default)
    {
        var protoReq = new GrpcModels.ProvisionDomainRequest
        {
            DomainName = request.DomainName,
            MaxMailboxCount = request.MaxMailboxCount,
            RetentionDays = request.RetentionDays
        };

        var response = await _managementClient.ProvisionDomainAsync(protoReq, cancellationToken: cancellationToken);

        return new BffModels.ProvisionDomainResponse(
            response.DomainId,
            response.DomainName,
            response.DkimSelector,
            response.DkimTxtRecord,
            response.ProvisionedAt.ToDateTimeOffset());
    }

    public async Task<BffModels.CreateMailboxResponse> CreateMailboxAsync(BffModels.CreateMailboxRequest request, CancellationToken cancellationToken = default)
    {
        var protoReq = new GrpcModels.CreateMailboxRequest
        {
            DomainId = request.DomainId,
            LocalPart = request.LocalPart,
            UserId = request.UserId ?? string.Empty
        };

        var response = await _managementClient.CreateMailboxAsync(protoReq, cancellationToken: cancellationToken);

        return new BffModels.CreateMailboxResponse(
            response.MailboxId,
            response.FullAddress,
            response.CreatedAt.ToDateTimeOffset());
    }

    public async Task<BffModels.CreateAliasResponse> CreateAliasAsync(BffModels.CreateAliasRequest request, CancellationToken cancellationToken = default)
    {
        var protoReq = new GrpcModels.CreateAliasRequest
        {
            DomainId = request.DomainId,
            AliasAddress = request.AliasAddress
        };
        protoReq.TargetAddresses.AddRange(request.TargetAddresses);

        var response = await _managementClient.CreateAliasAsync(protoReq, cancellationToken: cancellationToken);

        return new BffModels.CreateAliasResponse(
            response.AliasId,
            response.CreatedAt.ToDateTimeOffset());
    }

    public async Task<BffModels.ResetPasswordResponse> ResetPasswordAsync(string mailboxId, CancellationToken cancellationToken = default)
    {
        var protoReq = new GrpcModels.ResetPasswordRequest
        {
            MailboxId = mailboxId
        };

        var response = await _managementClient.ResetPasswordAsync(protoReq, cancellationToken: cancellationToken);

        return new BffModels.ResetPasswordResponse(
            response.Acknowledged,
            response.Message);
    }

    public async Task<BffModels.AuditListResponse> GetAuditRecordsAsync(string? resourceType, string? resourceId, int pageSize, string? nextPageToken, CancellationToken cancellationToken = default)
    {
        var protoReq = new GrpcModels.GetAuditRecordsRequest
        {
            ResourceType = resourceType ?? string.Empty,
            ResourceId = resourceId ?? string.Empty,
            PageSize = pageSize,
            NextPageToken = nextPageToken ?? string.Empty
        };

        var response = await _managementClient.GetAuditRecordsAsync(protoReq, cancellationToken: cancellationToken);

        var records = response.Records.Select(r => new BffModels.AuditRecordResponse(
            r.AuditId,
            r.ActorId,
            r.ActorType,
            r.Action,
            r.ResourceType,
            r.ResourceId,
            r.Timestamp.ToDateTimeOffset(),
            r.Result,
            r.DetailJson)).ToList();

        return new BffModels.AuditListResponse(records, response.NextPageToken);
    }

    public async Task<BffModels.RequeueDeadLetterResponse> RequeueDeadLetterAsync(string processedMessageId, CancellationToken cancellationToken = default)
    {
        var protoReq = new GrpcModels.RequeueDeadLetterRequest
        {
            ProcessedMessageId = processedMessageId
        };

        var response = await _managementClient.RequeueDeadLetterAsync(protoReq, cancellationToken: cancellationToken);

        return new BffModels.RequeueDeadLetterResponse(
            response.Success,
            response.Message);
    }

    public async Task<BffModels.DraftResponse> CreateDraftAsync(BffModels.CreateDraftRequest request, CancellationToken cancellationToken = default)
    {
        var protoReq = new GrpcModels.CreateDraftMessageRequest
        {
            MailboxId = request.MailboxId,
            AssignedStaffId = request.AssignedStaffId ?? string.Empty,
            Subject = request.Subject,
            Body = request.Body,
            Source = "Manual",
            SourceType = request.SourceType ?? "MANUAL",
            SourceId = request.SourceId ?? string.Empty,
            IdempotencyKey = request.IdempotencyKey ?? string.Empty,
            ThreadId = request.ThreadId ?? string.Empty,
            ReplyToMessageId = request.ReplyToMessageId ?? string.Empty,
        };

        if (request.To != null)
        {
            protoReq.ToRecipients.AddRange(request.To);
        }

        var createResp = await _securityClient.CreateDraftMessageAsync(protoReq, cancellationToken: cancellationToken);

        var draft = await _securityClient.GetDraftAsync(new GrpcModels.GetDraftRequest { DraftId = createResp.DraftId }, cancellationToken: cancellationToken);

        var response = MapDraftResponse(draft);
        return response with { IsExisting = createResp.IsExisting };
    }

    public async Task<BffModels.DraftListResponse> ListDraftsAsync(string? mailboxId, string? status, int pageSize, string? nextPageToken, CancellationToken cancellationToken = default)
    {
        var protoReq = new GrpcModels.ListDraftsRequest
        {
            MailboxId = mailboxId ?? string.Empty,
            Status = status ?? string.Empty,
            PageSize = pageSize,
            NextPageToken = nextPageToken ?? string.Empty
        };

        var response = await _securityClient.ListDraftsAsync(protoReq, cancellationToken: cancellationToken);

        var drafts = response.Drafts.Select(MapDraftResponse).ToList();
        return new BffModels.DraftListResponse(drafts, response.NextPageToken);
    }

    public async Task<BffModels.DraftResponse> GetDraftAsync(string draftId, CancellationToken cancellationToken = default)
    {
        var protoReq = new GrpcModels.GetDraftRequest { DraftId = draftId };
        var draft = await _securityClient.GetDraftAsync(protoReq, cancellationToken: cancellationToken);
        return MapDraftResponse(draft);
    }

    public async Task<BffModels.SubmitOutboundMessageResponse> SubmitOutboundMessageAsync(BffModels.SubmitOutboundMessageRequest request, CancellationToken cancellationToken = default)
    {
        var protoReq = new GrpcModels.SubmitOutboundMessageRequest
        {
            SenderAddress = request.SenderAddress,
            Subject = request.Subject,
            BodyText = request.BodyText,
            BodyHtml = request.BodyHtml,
            IdempotencyKey = request.IdempotencyKey ?? string.Empty,
            DraftRootId = request.DraftRootId,
            ThreadId = request.ThreadId,
            ReplyToMessageId = request.ReplyToMessageId
        };
        protoReq.RecipientAddresses.AddRange(request.RecipientAddresses);

        if (request.Attachments != null)
        {
            foreach (var att in request.Attachments)
            {
                var attDto = new GrpcModels.AttachmentDto
                {
                    Filename = att.Filename,
                    ContentType = att.ContentType,
                    Content = ByteString.CopyFrom(Convert.FromBase64String(att.ContentBase64))
                };
                protoReq.Attachments.Add(attDto);
            }
        }

        var response = await _securityClient.SubmitOutboundMessageAsync(protoReq, cancellationToken: cancellationToken);

        return new BffModels.SubmitOutboundMessageResponse(
            response.ProcessedMessageId,
            response.StalwartQueueId,
            response.SubmittedAt.ToDateTimeOffset());
    }

    public async Task<BffModels.ProcessedMessageResponse> GetProcessedMessageAsync(string processedMessageId, CancellationToken cancellationToken = default)
    {
        var protoReq = new GrpcModels.GetProcessedMessageRequest { ProcessedMessageId = processedMessageId };
        var msg = await _securityClient.GetProcessedMessageAsync(protoReq, cancellationToken: cancellationToken);
        return MapProcessedMessageResponse(msg);
    }

    public async Task<BffModels.ProcessedMessageListResponse> ListProcessedMessagesAsync(string? direction, string? emailCategory, string? pipelineStatus, int pageSize, string? nextPageToken, CancellationToken cancellationToken = default)
    {
        var protoReq = new GrpcModels.ListProcessedMessagesRequest
        {
            Direction = direction ?? string.Empty,
            EmailCategory = emailCategory ?? string.Empty,
            PipelineStatus = pipelineStatus ?? string.Empty,
            PageSize = pageSize,
            NextPageToken = nextPageToken ?? string.Empty
        };

        var response = await _securityClient.ListProcessedMessagesAsync(protoReq, cancellationToken: cancellationToken);

        var messages = response.Messages.Select(MapProcessedMessageResponse).ToList();
        return new BffModels.ProcessedMessageListResponse(messages, response.NextPageToken);
    }

    public async Task<BffModels.QuarantineRecordResponse> GetQuarantineRecordAsync(string quarantineId, CancellationToken cancellationToken = default)
    {
        var protoReq = new GrpcModels.GetQuarantineRecordRequest { QuarantineId = quarantineId };
        var rec = await _securityClient.GetQuarantineRecordAsync(protoReq, cancellationToken: cancellationToken);
        return MapQuarantineRecordResponse(rec);
    }

    public async Task<BffModels.QuarantineListResponse> ListQuarantineRecordsAsync(string? status, int pageSize, string? nextPageToken, CancellationToken cancellationToken = default)
    {
        var protoReq = new GrpcModels.ListQuarantineRecordsRequest
        {
            Status = status ?? string.Empty,
            PageSize = pageSize,
            NextPageToken = nextPageToken ?? string.Empty
        };

        var response = await _securityClient.ListQuarantineRecordsAsync(protoReq, cancellationToken: cancellationToken);
        var records = response.Records.Select(MapQuarantineRecordResponse).ToList();
        return new BffModels.QuarantineListResponse(records, response.NextPageToken);
    }

    public async Task<BffModels.ReleaseQuarantineResponse> ReleaseQuarantineAsync(string quarantineId, CancellationToken cancellationToken = default)
    {
        var protoReq = new GrpcModels.ReleaseQuarantineRequest { QuarantineId = quarantineId };
        var response = await _securityClient.ReleaseQuarantineAsync(protoReq, cancellationToken: cancellationToken);
        return new BffModels.ReleaseQuarantineResponse(response.Success, response.ReleasedAt.ToDateTimeOffset());
    }

    public async Task<BffModels.DeleteQuarantineResponse> DeleteQuarantineAsync(string quarantineId, CancellationToken cancellationToken = default)
    {
        var protoReq = new GrpcModels.DeleteQuarantineRequest { QuarantineId = quarantineId };
        var response = await _securityClient.DeleteQuarantineAsync(protoReq, cancellationToken: cancellationToken);
        return new BffModels.DeleteQuarantineResponse(response.Success);
    }

    public async Task<BffModels.ThreadResponse> GetThreadAsync(string threadId, CancellationToken cancellationToken = default)
    {
        var protoReq = new GrpcModels.GetThreadRequest { ThreadId = threadId };
        var thread = await _securityClient.GetThreadAsync(protoReq, cancellationToken: cancellationToken);

        var messages = thread.Messages.Select(m => new BffModels.ThreadMessageResponse(
            m.MessageId,
            m.Direction,
            m.SenderAddress,
            m.RecipientAddresses.ToList(),
            m.Subject,
            m.BodyText,
            m.BodyPreview,
            m.ReplyToMessageId,
            m.ReceivedAt.ToDateTimeOffset(),
            m.SentAt.ToDateTimeOffset())).ToList();

        var drafts = thread.Drafts.Select(MapDraftResponse).ToList();
        var histories = thread.AssignmentHistory.Select(h => new BffModels.ThreadAssignmentHistoryResponse(
            h.Id,
            h.ThreadId,
            h.FromUserId,
            h.ToUserId,
            h.Action,
            h.ActorUserId,
            h.Reason,
            h.CreatedAt.ToDateTimeOffset())).ToList();

        return new BffModels.ThreadResponse(
            thread.ThreadId,
            thread.MailboxId,
            thread.Subject,
            thread.Participants.ToList(),
            messages,
            drafts,
            thread.CreatedAt.ToDateTimeOffset(),
            thread.UpdatedAt.ToDateTimeOffset(),
            string.IsNullOrEmpty(thread.PrimaryAssigneeUserId) ? null : thread.PrimaryAssigneeUserId,
            thread.AssignedAt?.ToDateTimeOffset(),
            thread.Status,
            thread.Priority,
            histories);
    }

    public async Task<BffModels.ThreadListResponse> ListThreadsAsync(string? mailboxId, int pageSize, string? nextPageToken, string? scope = null, string? status = null, CancellationToken cancellationToken = default)
    {
        var protoReq = new GrpcModels.ListThreadsRequest
        {
            MailboxId = mailboxId ?? string.Empty,
            PageSize = pageSize,
            NextPageToken = nextPageToken ?? string.Empty,
            Scope = scope ?? string.Empty,
            Status = status ?? string.Empty
        };

        var response = await _securityClient.ListThreadsAsync(protoReq, cancellationToken: cancellationToken);

        var summaries = response.Threads.Select(t => new BffModels.ThreadSummaryResponse(
            t.ThreadId,
            t.MailboxId,
            t.Subject,
            t.Participants.ToList(),
            t.LastMessageAt.ToDateTimeOffset(),
            t.MessageCount,
            t.DraftCount,
            t.HasUnread,
            t.Snippet,
            string.IsNullOrEmpty(t.PrimaryAssigneeUserId) ? null : t.PrimaryAssigneeUserId,
            t.AssignedAt?.ToDateTimeOffset(),
            t.Status,
            t.Priority)).ToList();

        return new BffModels.ThreadListResponse(summaries, response.NextPageToken);
    }

    public async Task<BffModels.ClaimThreadResponse> ClaimThreadAsync(string threadId, CancellationToken cancellationToken = default)
    {
        var protoReq = new GrpcModels.ClaimThreadRequest { ThreadId = threadId };
        var response = await _securityClient.ClaimThreadAsync(protoReq, cancellationToken: cancellationToken);
        return new BffModels.ClaimThreadResponse(
            response.Success,
            response.ThreadId,
            response.PrimaryAssigneeUserId,
            response.AssignedAt.ToDateTimeOffset(),
            response.Status);
    }

    public async Task<BffModels.ReassignThreadResponse> ReassignThreadAsync(string threadId, BffModels.ReassignThreadRequest request, CancellationToken cancellationToken = default)
    {
        var protoReq = new GrpcModels.ReassignThreadRequest
        {
            ThreadId = threadId,
            TargetUserId = request.TargetUserId,
            Reason = request.Reason ?? string.Empty
        };
        var response = await _securityClient.ReassignThreadAsync(protoReq, cancellationToken: cancellationToken);
        return new BffModels.ReassignThreadResponse(
            response.Success,
            response.ThreadId,
            response.PrimaryAssigneeUserId,
            response.AssignedAt.ToDateTimeOffset(),
            response.Status);
    }

    public async Task<BffModels.UnassignThreadResponse> UnassignThreadAsync(string threadId, BffModels.UnassignThreadRequest request, CancellationToken cancellationToken = default)
    {
        var protoReq = new GrpcModels.UnassignThreadRequest
        {
            ThreadId = threadId,
            Reason = request.Reason ?? string.Empty
        };
        var response = await _securityClient.UnassignThreadAsync(protoReq, cancellationToken: cancellationToken);
        return new BffModels.UnassignThreadResponse(response.Success, response.ThreadId, response.Status);
    }

    public async Task<BffModels.ThreadAssignmentHistoryListResponse> GetThreadAssignmentHistoryAsync(string threadId, CancellationToken cancellationToken = default)
    {
        var protoReq = new GrpcModels.GetThreadAssignmentHistoryRequest { ThreadId = threadId };
        var response = await _securityClient.GetThreadAssignmentHistoryAsync(protoReq, cancellationToken: cancellationToken);
        var histories = response.History.Select(h => new BffModels.ThreadAssignmentHistoryResponse(
            h.Id,
            h.ThreadId,
            h.FromUserId,
            h.ToUserId,
            h.Action,
            h.ActorUserId,
            h.Reason,
            h.CreatedAt.ToDateTimeOffset())).ToList();

        return new BffModels.ThreadAssignmentHistoryListResponse(response.ThreadId, histories);
    }

    private static BffModels.DraftResponse MapDraftResponse(GrpcModels.DraftDto draft)
    {
        return new BffModels.DraftResponse(
            draft.DraftId,
            draft.DraftRootId,
            draft.RevisionNumber,
            draft.IsLatestRevision,
            draft.Source,
            draft.Status,
            draft.MailboxId,
            string.IsNullOrEmpty(draft.AssignedStaffId) ? null : draft.AssignedStaffId,
            draft.Subject,
            draft.Body,
            draft.ContentHash,
            draft.CreatedAt.ToDateTimeOffset(),
            string.IsNullOrEmpty(draft.SourceType) ? "MANUAL" : draft.SourceType,
            string.IsNullOrEmpty(draft.SourceId) ? null : draft.SourceId,
            draft.ToRecipients?.ToList(),
            string.IsNullOrEmpty(draft.ThreadId) ? null : draft.ThreadId,
            string.IsNullOrEmpty(draft.ReplyToMessageId) ? null : draft.ReplyToMessageId);
    }

    private static BffModels.ProcessedMessageResponse MapProcessedMessageResponse(GrpcModels.ProcessedMessageDto msg)
    {
        var checks = msg.SecurityChecks.Select(sc => new BffModels.SecurityCheckResultDto(
            sc.Stage,
            sc.Result,
            sc.DetailJson,
            sc.DurationMs)).ToList();

        return new BffModels.ProcessedMessageResponse(
            msg.ProcessedMessageId,
            msg.MessageId,
            msg.Direction,
            msg.SenderAddress,
            msg.RecipientAddresses,
            msg.Subject,
            msg.ReceivedAt.ToDateTimeOffset(),
            msg.ProcessedAt.ToDateTimeOffset(),
            msg.EmailCategory,
            msg.PipelineStatus,
            msg.SpamScore,
            msg.PhishingScore,
            msg.IsQuarantined,
            msg.R2RawEmlPath,
            checks);
    }

    private static BffModels.QuarantineRecordResponse MapQuarantineRecordResponse(GrpcModels.QuarantineRecordDto rec)
    {
        return new BffModels.QuarantineRecordResponse(
            rec.QuarantineId,
            rec.ProcessedMessageId,
            rec.MessageId,
            rec.QuarantineReason,
            rec.QuarantinedAt.ToDateTimeOffset(),
            rec.Status,
            string.IsNullOrEmpty(rec.ReviewedBy) ? null : rec.ReviewedBy,
            rec.ReviewedAt?.ToDateTimeOffset());
    }
}

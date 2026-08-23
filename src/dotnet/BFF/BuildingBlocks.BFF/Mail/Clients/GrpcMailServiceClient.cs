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
            Source = "Manual"
        };

        var createResp = await _securityClient.CreateDraftMessageAsync(protoReq, cancellationToken: cancellationToken);

        var draft = await _securityClient.GetDraftAsync(new GrpcModels.GetDraftRequest { DraftId = createResp.DraftId }, cancellationToken: cancellationToken);

        return MapDraftResponse(draft);
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
            DraftRootId = request.DraftRootId
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
            draft.CreatedAt.ToDateTimeOffset());
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

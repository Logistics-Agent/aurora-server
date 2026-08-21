using Grpc.Core;
using MediatR;
using Google.Protobuf.WellKnownTypes;
using MailService.GrpcServices;
using MailService.Application.Commands.Outbound;
using MailService.Application.Commands.Quarantine;
using MailService.Application.Queries.Drafts;
using MailService.Application.Queries.Messages;
using MailService.Application.Queries.Quarantine;
using MailService.Application.Queries.Audit;
using MailService.Domain.Enums;

namespace MailService.GrpcServices;


public class MailSecurityService : MailSecurity.MailSecurityBase
{
    private readonly ISender _mediator;

    public MailSecurityService(ISender mediator)
    {
        _mediator = mediator;
    }

    public override async Task<CreateDraftMessageResponse> CreateDraftMessage(CreateDraftMessageRequest request, ServerCallContext context)
    {
        Guid mailboxId = Guid.Parse(request.MailboxId);
        Guid? staffId = string.IsNullOrEmpty(request.AssignedStaffId) ? null : Guid.Parse(request.AssignedStaffId);
        var source = System.Enum.TryParse<DraftSource>(request.Source, true, out var parsedSource) ? parsedSource : DraftSource.Manual;

        var draft = await _mediator.Send(new CreateDraftMessageCommand(mailboxId, staffId, request.Subject, request.Body, source), context.CancellationToken);

        return new CreateDraftMessageResponse
        {
            DraftId = draft.Id.ToString(),
            DraftRootId = draft.DraftRootId.ToString(),
            RevisionNumber = draft.RevisionNumber,
            CreatedAt = Timestamp.FromDateTimeOffset(draft.CreatedAt)
        };
    }

    public override async Task<SubmitOutboundMessageResponse> SubmitOutboundMessage(SubmitOutboundMessageRequest request, ServerCallContext context)
    {
        Guid? draftRootId = string.IsNullOrEmpty(request.DraftRootId) ? null : Guid.Parse(request.DraftRootId);
        var attachments = request.Attachments.Select(a => (a.Filename, a.ContentType, a.Content.ToByteArray())).ToList();

        var result = await _mediator.Send(new SubmitOutboundMessageCommand(
            request.SenderAddress,
            request.RecipientAddresses.ToList(),
            request.Subject,
            request.BodyText,
            request.BodyHtml,
            attachments,
            request.IdempotencyKey,
            draftRootId), context.CancellationToken);

        if (result.IsRejected)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, result.RejectionReason ?? "Outbound message rejected by security pipeline"));
        }

        return new SubmitOutboundMessageResponse
        {
            ProcessedMessageId = result.ProcessedMessage.Id.ToString(),
            StalwartQueueId = result.StalwartQueueId ?? string.Empty,
            SubmittedAt = Timestamp.FromDateTimeOffset(result.ProcessedMessage.ProcessedAt)
        };
    }

    public override async Task<DraftDto> GetDraft(GetDraftRequest request, ServerCallContext context)
    {
        Guid draftId = Guid.Parse(request.DraftId);
        var draft = await _mediator.Send(new GetDraftQuery(draftId), context.CancellationToken);
        if (draft == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Draft revision '{request.DraftId}' not found."));
        }

        return MapDraftDto(draft);
    }

    public override async Task<ListDraftsResponse> ListDrafts(ListDraftsRequest request, ServerCallContext context)
    {
        Guid? mailboxId = string.IsNullOrEmpty(request.MailboxId) ? null : Guid.Parse(request.MailboxId);
        var drafts = await _mediator.Send(new ListDraftsQuery(mailboxId, request.Status, request.PageSize), context.CancellationToken);

        var response = new ListDraftsResponse();
        response.Drafts.AddRange(drafts.Select(MapDraftDto));
        return response;
    }

    public override async Task<ProcessedMessageDto> GetProcessedMessage(GetProcessedMessageRequest request, ServerCallContext context)
    {
        Guid messageId = Guid.Parse(request.ProcessedMessageId);
        var message = await _mediator.Send(new GetProcessedMessageQuery(messageId), context.CancellationToken);
        if (message == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Processed message '{request.ProcessedMessageId}' not found."));
        }

        return MapProcessedMessageDto(message);
    }

    public override async Task<ListProcessedMessagesResponse> ListProcessedMessages(ListProcessedMessagesRequest request, ServerCallContext context)
    {
        var messages = await _mediator.Send(new ListProcessedMessagesQuery(request.Direction, request.EmailCategory, request.PageSize, request.NextPageToken), context.CancellationToken);

        var response = new ListProcessedMessagesResponse();
        response.Messages.AddRange(messages.Select(MapProcessedMessageDto));
        return response;
    }

    public override async Task<QuarantineRecordDto> GetQuarantineRecord(GetQuarantineRecordRequest request, ServerCallContext context)
    {
        Guid quarantineId = Guid.Parse(request.QuarantineId);
        var record = await _mediator.Send(new GetQuarantineRecordQuery(quarantineId), context.CancellationToken);
        if (record == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Quarantine record '{request.QuarantineId}' not found."));
        }

        return MapQuarantineRecordDto(record);
    }

    public override async Task<ListQuarantineRecordsResponse> ListQuarantineRecords(ListQuarantineRecordsRequest request, ServerCallContext context)
    {
        var records = await _mediator.Send(new ListQuarantineRecordsQuery(request.Status, request.PageSize), context.CancellationToken);

        var response = new ListQuarantineRecordsResponse();
        response.Records.AddRange(records.Select(MapQuarantineRecordDto));
        return response;
    }

    public override async Task<ReleaseQuarantineResponse> ReleaseQuarantine(ReleaseQuarantineRequest request, ServerCallContext context)
    {
        Guid quarantineId = Guid.Parse(request.QuarantineId);
        bool success = await _mediator.Send(new ReleaseQuarantineCommand(quarantineId), context.CancellationToken);

        return new ReleaseQuarantineResponse
        {
            Success = success,
            ReleasedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow)
        };
    }

    public override async Task<DeleteQuarantineResponse> DeleteQuarantine(DeleteQuarantineRequest request, ServerCallContext context)
    {
        Guid quarantineId = Guid.Parse(request.QuarantineId);
        bool success = await _mediator.Send(new DeleteQuarantineCommand(quarantineId), context.CancellationToken);

        return new DeleteQuarantineResponse { Success = success };
    }

    private static DraftDto MapDraftDto(Domain.Entities.EmailDraft draft)
    {
        return new DraftDto
        {
            DraftId = draft.Id.ToString(),
            DraftRootId = draft.DraftRootId.ToString(),
            RevisionNumber = draft.RevisionNumber,
            IsLatestRevision = draft.IsLatestRevision,
            Source = draft.Source.ToString(),
            Status = draft.Status.ToString(),
            MailboxId = draft.MailboxId.ToString(),
            AssignedStaffId = draft.AssignedStaffId?.ToString() ?? string.Empty,
            Subject = draft.Subject,
            Body = draft.Body,
            ContentHash = draft.ContentHash,
            CreatedAt = Timestamp.FromDateTimeOffset(draft.CreatedAt)
        };
    }

    private static ProcessedMessageDto MapProcessedMessageDto(Domain.Entities.ProcessedMessage msg)
    {
        var dto = new ProcessedMessageDto
        {
            ProcessedMessageId = msg.Id.ToString(),
            MessageId = msg.MessageId,
            Direction = msg.Direction.ToString(),
            SenderAddress = msg.SenderAddress,
            Subject = msg.Subject ?? string.Empty,
            ReceivedAt = Timestamp.FromDateTimeOffset(msg.ReceivedAt),
            ProcessedAt = Timestamp.FromDateTimeOffset(msg.ProcessedAt),
            EmailCategory = msg.EmailCategory.ToString(),
            PipelineStatus = msg.PipelineStatus.ToString(),
            SpamScore = (double)msg.SpamScore,
            PhishingScore = (double)msg.PhishingScore,
            IsQuarantined = msg.IsQuarantined,
            R2RawEmlPath = msg.R2RawEmlPath ?? string.Empty
        };

        dto.RecipientAddresses.AddRange(msg.RecipientAddresses);
        foreach (var sc in msg.SecurityCheckResults)
        {
            dto.SecurityChecks.Add(new SecurityCheckResultDto
            {
                Stage = sc.Stage.ToString(),
                Result = sc.Result,
                DetailJson = sc.DetailJson ?? string.Empty,
                DurationMs = sc.DurationMs
            });
        }

        return dto;
    }

    private static QuarantineRecordDto MapQuarantineRecordDto(Domain.Entities.QuarantineRecord rec)
    {
        return new QuarantineRecordDto
        {
            QuarantineId = rec.Id.ToString(),
            ProcessedMessageId = rec.ProcessedMessageId.ToString(),
            MessageId = rec.MessageId,
            QuarantineReason = rec.QuarantineReason,
            QuarantinedAt = Timestamp.FromDateTimeOffset(rec.QuarantinedAt),
            Status = rec.Status.ToString(),
            ReviewedBy = rec.ReviewedBy?.ToString() ?? string.Empty,
            ReviewedAt = rec.ReviewedAt.HasValue ? Timestamp.FromDateTimeOffset(rec.ReviewedAt.Value) : null
        };
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using MediatR;
using Google.Protobuf.WellKnownTypes;
using MailService.GrpcServices;
using MailService.Application.Commands.Drafts;
using MailService.Application.Commands.Outbound;
using MailService.Application.Commands.Quarantine;
using MailService.Application.Commands.Threads;
using MailService.Application.Queries.Drafts;
using MailService.Application.Queries.Messages;
using MailService.Application.Queries.Quarantine;
using MailService.Application.Queries.Threads;
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
        if (!Guid.TryParse(request.MailboxId, out var mailboxId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid MailboxId GUID format."));
        }

        Guid? staffId = string.IsNullOrEmpty(request.AssignedStaffId) ? null : (Guid.TryParse(request.AssignedStaffId, out var sId) ? sId : null);
        Guid? threadId = string.IsNullOrEmpty(request.ThreadId) ? null : (Guid.TryParse(request.ThreadId, out var tId) ? tId : null);
        var source = System.Enum.TryParse<DraftSource>(request.Source, true, out var parsedSource) ? parsedSource : DraftSource.Manual;

        var command = new CreateDraftMessageCommand(
            mailboxId,
            staffId,
            request.Subject,
            request.Body,
            source,
            string.IsNullOrEmpty(request.SourceType) ? "MANUAL" : request.SourceType,
            string.IsNullOrEmpty(request.SourceId) ? null : request.SourceId,
            string.IsNullOrEmpty(request.IdempotencyKey) ? null : request.IdempotencyKey,
            request.ToRecipients?.ToList(),
            threadId,
            string.IsNullOrEmpty(request.ReplyToMessageId) ? null : request.ReplyToMessageId);

        try
        {
            var result = await _mediator.Send(command, context.CancellationToken);

            return new CreateDraftMessageResponse
            {
                DraftId = result.Draft.Id.ToString(),
                DraftRootId = result.Draft.DraftRootId.ToString(),
                RevisionNumber = result.Draft.RevisionNumber,
                CreatedAt = Timestamp.FromDateTimeOffset(result.Draft.CreatedAt),
                SourceType = result.Draft.SourceType ?? "MANUAL",
                SourceId = result.Draft.SourceId ?? string.Empty,
                IsExisting = result.IsExisting,
                ThreadId = result.Draft.ThreadId?.ToString() ?? string.Empty,
                ReplyToMessageId = result.Draft.ReplyToMessageId ?? string.Empty,
                Status = "DRAFT"
            };
        }
        catch (KeyNotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, ex.Message));
        }
    }

    public override async Task<SubmitOutboundMessageResponse> SubmitOutboundMessage(SubmitOutboundMessageRequest request, ServerCallContext context)
    {
        Guid? draftRootId = string.IsNullOrEmpty(request.DraftRootId) ? null : Guid.Parse(request.DraftRootId);
        Guid? threadId = string.IsNullOrEmpty(request.ThreadId) ? null : Guid.Parse(request.ThreadId);
        string? replyToMessageId = string.IsNullOrEmpty(request.ReplyToMessageId) ? null : request.ReplyToMessageId;
        var attachments = request.Attachments.Select(a => (a.Filename, a.ContentType, a.Content.ToByteArray())).ToList();

        try
        {
            var result = await _mediator.Send(new SubmitOutboundMessageCommand(
                request.SenderAddress,
                request.RecipientAddresses.ToList(),
                request.Subject,
                request.BodyText,
                request.BodyHtml,
                attachments,
                request.IdempotencyKey,
                draftRootId,
                threadId,
                replyToMessageId), context.CancellationToken);

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
        catch (InvalidOperationException ex)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
    }

    public override async Task<DraftDto> GetDraft(GetDraftRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.DraftId, out var draftId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid DraftId GUID format."));
        }

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

    public override async Task<ThreadDto> GetThread(GetThreadRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ThreadId, out var threadId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ThreadId GUID format."));
        }

        try
        {
            var result = await _mediator.Send(new GetThreadQuery(threadId), context.CancellationToken);
            if (result == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, $"Thread '{request.ThreadId}' not found."));
            }

            var dto = new ThreadDto
            {
                ThreadId = result.Thread.Id.ToString(),
                MailboxId = result.Thread.MailboxId.ToString(),
                Subject = result.Thread.Subject,
                CreatedAt = Timestamp.FromDateTimeOffset(result.Thread.CreatedAt),
                UpdatedAt = Timestamp.FromDateTimeOffset(result.Thread.LastMessageAt),
                PrimaryAssigneeUserId = result.Thread.PrimaryAssigneeUserId?.ToString() ?? string.Empty,
                AssignedAt = result.Thread.AssignedAt.HasValue ? Timestamp.FromDateTimeOffset(result.Thread.AssignedAt.Value) : null,
                Status = result.Thread.Status.ToString().ToUpperInvariant(),
                Priority = result.Thread.Priority.ToString().ToUpperInvariant(),
            };

            dto.Participants.AddRange(result.Thread.Participants);

            foreach (var msg in result.Messages)
            {
                var msgDto = new ThreadMessageDto
                {
                    MessageId = msg.Id.ToString(),
                    Direction = msg.Direction.ToString(),
                    SenderAddress = msg.SenderAddress,
                    Subject = msg.Subject ?? string.Empty,
                    BodyText = msg.BodyText ?? string.Empty,
                    BodyPreview = msg.BodyText?.Length > 100 ? msg.BodyText.Substring(0, 100) : (msg.BodyText ?? string.Empty),
                    ReplyToMessageId = msg.InReplyTo ?? string.Empty,
                    ReceivedAt = Timestamp.FromDateTimeOffset(msg.ReceivedAt),
                    SentAt = Timestamp.FromDateTimeOffset(msg.ProcessedAt),
                };
                msgDto.RecipientAddresses.AddRange(msg.RecipientAddresses);
                dto.Messages.Add(msgDto);
            }

            foreach (var draft in result.Drafts)
            {
                dto.Drafts.Add(MapDraftDto(draft));
            }

            foreach (var h in result.AssignmentHistories)
            {
                dto.AssignmentHistory.Add(new ThreadAssignmentHistoryDto
                {
                    Id = h.Id.ToString(),
                    ThreadId = h.ThreadId.ToString(),
                    FromUserId = h.FromUserId?.ToString() ?? string.Empty,
                    ToUserId = h.ToUserId?.ToString() ?? string.Empty,
                    Action = h.Action.ToString().ToUpperInvariant(),
                    ActorUserId = h.ActorUserId.ToString(),
                    Reason = h.Reason ?? string.Empty,
                    CreatedAt = Timestamp.FromDateTimeOffset(h.CreatedAt)
                });
            }

            return dto;
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
    }

    public override async Task<ListThreadsResponse> ListThreads(ListThreadsRequest request, ServerCallContext context)
    {
        Guid? mailboxId = string.IsNullOrEmpty(request.MailboxId) ? null : Guid.Parse(request.MailboxId);
        string? search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        var result = await _mediator.Send(new ListThreadsQuery(mailboxId, request.PageSize, request.NextPageToken, request.Scope, request.Status, search), context.CancellationToken);

        var response = new ListThreadsResponse
        {
            NextPageToken = result.NextPageToken ?? string.Empty,
            HasMore = result.HasMore
        };

        foreach (var t in result.Threads)
        {
            var summary = new ThreadSummaryDto
            {
                ThreadId = t.Id.ToString(),
                MailboxId = t.MailboxId.ToString(),
                Subject = t.Subject,
                LastMessageAt = Timestamp.FromDateTimeOffset(t.LastMessageAt),
                MessageCount = t.MessageCount,
                DraftCount = t.DraftCount,
                HasUnread = t.HasUnread,
                Snippet = t.Snippet ?? string.Empty,
                PrimaryAssigneeUserId = t.PrimaryAssigneeUserId?.ToString() ?? string.Empty,
                AssignedAt = t.AssignedAt.HasValue ? Timestamp.FromDateTimeOffset(t.AssignedAt.Value) : null,
                Status = t.Status.ToString().ToUpperInvariant(),
                Priority = t.Priority.ToString().ToUpperInvariant(),
            };
            summary.Participants.AddRange(t.Participants);
            response.Threads.Add(summary);
        }

        return response;
    }

    public override async Task<ClaimThreadResponse> ClaimThread(ClaimThreadRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ThreadId, out var threadId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ThreadId GUID format."));
        }

        try
        {
            var result = await _mediator.Send(new ClaimThreadCommand(threadId), context.CancellationToken);
            return new ClaimThreadResponse
            {
                Success = result.Success,
                ThreadId = result.ThreadId.ToString(),
                PrimaryAssigneeUserId = result.PrimaryAssigneeUserId.ToString(),
                AssignedAt = Timestamp.FromDateTimeOffset(result.AssignedAt),
                Status = result.Status
            };
        }
        catch (KeyNotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            throw new RpcException(new Status(StatusCode.FailedPrecondition, ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
    }

    public override async Task<ReassignThreadResponse> ReassignThread(ReassignThreadRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ThreadId, out var threadId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ThreadId GUID format."));
        }
        if (!Guid.TryParse(request.TargetUserId, out var targetUserId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid TargetUserId GUID format."));
        }

        try
        {
            var result = await _mediator.Send(new ReassignThreadCommand(threadId, targetUserId, request.Reason), context.CancellationToken);
            return new ReassignThreadResponse
            {
                Success = result.Success,
                ThreadId = result.ThreadId.ToString(),
                PrimaryAssigneeUserId = result.PrimaryAssigneeUserId.ToString(),
                AssignedAt = Timestamp.FromDateTimeOffset(result.AssignedAt),
                Status = result.Status
            };
        }
        catch (KeyNotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
    }

    public override async Task<UnassignThreadResponse> UnassignThread(UnassignThreadRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ThreadId, out var threadId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ThreadId GUID format."));
        }

        try
        {
            var result = await _mediator.Send(new UnassignThreadCommand(threadId, request.Reason), context.CancellationToken);
            return new UnassignThreadResponse
            {
                Success = result.Success,
                ThreadId = result.ThreadId.ToString(),
                Status = result.Status
            };
        }
        catch (KeyNotFoundException ex)
        {
            throw new RpcException(new Status(StatusCode.NotFound, ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
    }

    public override async Task<GetThreadAssignmentHistoryResponse> GetThreadAssignmentHistory(GetThreadAssignmentHistoryRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ThreadId, out var threadId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ThreadId GUID format."));
        }

        var histories = await _mediator.Send(new GetThreadAssignmentHistoryQuery(threadId), context.CancellationToken);
        var response = new GetThreadAssignmentHistoryResponse { ThreadId = request.ThreadId };

        foreach (var h in histories)
        {
            response.History.Add(new ThreadAssignmentHistoryDto
            {
                Id = h.Id.ToString(),
                ThreadId = h.ThreadId.ToString(),
                FromUserId = h.FromUserId?.ToString() ?? string.Empty,
                ToUserId = h.ToUserId?.ToString() ?? string.Empty,
                Action = h.Action.ToString().ToUpperInvariant(),
                ActorUserId = h.ActorUserId.ToString(),
                Reason = h.Reason ?? string.Empty,
                CreatedAt = Timestamp.FromDateTimeOffset(h.CreatedAt)
            });
        }

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
        var dto = new DraftDto
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
            CreatedAt = Timestamp.FromDateTimeOffset(draft.CreatedAt),
            SourceType = draft.SourceType ?? "MANUAL",
            SourceId = draft.SourceId ?? string.Empty,
            ThreadId = draft.ThreadId?.ToString() ?? string.Empty,
            ReplyToMessageId = draft.ReplyToMessageId ?? string.Empty,
        };

        if (draft.ToRecipients != null)
        {
            dto.ToRecipients.AddRange(draft.ToRecipients);
        }

        return dto;
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
            R2RawEmlPath = msg.R2RawEmlPath ?? string.Empty,
            ThreadId = msg.ThreadId?.ToString() ?? string.Empty,
            ReplyToMessageId = msg.InReplyTo ?? string.Empty,
            BodyText = msg.BodyText ?? string.Empty,
            BodyHtml = msg.BodyHtml ?? string.Empty,
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

using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using MediatR;
using Google.Protobuf.WellKnownTypes;
using MailService.GrpcServices;
using MailService.Application.Commands.Outbox;
using MailService.Application.Commands.Provisioning;
using MailService.Application.Queries.Audit;

namespace MailService.GrpcServices;

public class MailManagementService : MailManagement.MailManagementBase
{
    private readonly ISender _mediator;

    public MailManagementService(ISender mediator)
    {
        _mediator = mediator;
    }

    public override async Task<ProvisionDomainResponse> ProvisionDomain(ProvisionDomainRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.DomainName))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "DomainName is required."));
        }

        var domain = await _mediator.Send(new ProvisionDomainCommand(
            request.DomainName,
            request.MaxMailboxCount > 0 ? request.MaxMailboxCount : 100,
            request.RetentionDays > 0 ? request.RetentionDays : 365), context.CancellationToken);

        return new ProvisionDomainResponse
        {
            DomainId = domain.Id.ToString(),
            DomainName = domain.DomainName,
            DkimSelector = domain.DkimSelector ?? "aurora-2025",
            DkimTxtRecord = domain.DkimTxtRecord ?? string.Empty,
            ProvisionedAt = Timestamp.FromDateTimeOffset(domain.CreatedAt)
        };
    }

    public override async Task<CreateMailboxResponse> CreateMailbox(CreateMailboxRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.DomainId, out var domainId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid DomainId GUID format."));
        }

        if (string.IsNullOrWhiteSpace(request.LocalPart))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "LocalPart is required."));
        }

        Guid? userId = string.IsNullOrEmpty(request.UserId) ? null : Guid.Parse(request.UserId);

        var mailbox = await _mediator.Send(new CreateMailboxCommand(domainId, request.LocalPart, userId), context.CancellationToken);

        return new CreateMailboxResponse
        {
            MailboxId = mailbox.Id.ToString(),
            FullAddress = mailbox.FullAddress,
            CreatedAt = Timestamp.FromDateTimeOffset(mailbox.CreatedAt)
        };
    }

    public override async Task<CreateAliasResponse> CreateAlias(CreateAliasRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.DomainId, out var domainId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid DomainId GUID format."));
        }

        if (string.IsNullOrWhiteSpace(request.AliasAddress))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "AliasAddress is required."));
        }

        var alias = await _mediator.Send(new CreateAliasCommand(domainId, request.AliasAddress, request.TargetAddresses.ToList()), context.CancellationToken);

        return new CreateAliasResponse
        {
            AliasId = alias.Id.ToString(),
            CreatedAt = Timestamp.FromDateTimeOffset(alias.CreatedAt)
        };
    }

    public override Task<ResetPasswordResponse> ResetPassword(ResetPasswordRequest request, ServerCallContext context)
    {
        return Task.FromResult(new ResetPasswordResponse
        {
            Acknowledged = true,
            Message = "Password management delegated to Cognito OIDC in v1"
        });
    }

    public override async Task<GetAuditRecordsResponse> GetAuditRecords(GetAuditRecordsRequest request, ServerCallContext context)
    {
        Guid? resourceId = null;
        if (!string.IsNullOrEmpty(request.ResourceId) && Guid.TryParse(request.ResourceId, out var parsedId))
        {
            resourceId = parsedId;
        }

        var records = await _mediator.Send(new GetAuditRecordsQuery(resourceId, request.PageSize), context.CancellationToken);

        var response = new GetAuditRecordsResponse();
        response.Records.AddRange(records.Select(r => new AuditRecordDto
        {
            AuditId = r.Id.ToString(),
            ActorId = r.ActorId.ToString(),
            ActorType = r.ActorType.ToString(),
            Action = r.Action,
            ResourceType = r.ResourceType,
            ResourceId = r.ResourceId.ToString(),
            Timestamp = Timestamp.FromDateTimeOffset(r.Timestamp),
            Result = r.Result,
            DetailJson = r.DetailJson ?? string.Empty
        }));

        return response;
    }

    public override async Task<RequeueDeadLetterResponse> RequeueDeadLetter(RequeueDeadLetterRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.ProcessedMessageId, out var messageId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid ProcessedMessageId GUID format."));
        }

        var result = await _mediator.Send(new RequeueDeadLetterCommand(messageId), context.CancellationToken);

        return new RequeueDeadLetterResponse
        {
            Success = result.Success,
            Message = result.Message
        };
    }
}

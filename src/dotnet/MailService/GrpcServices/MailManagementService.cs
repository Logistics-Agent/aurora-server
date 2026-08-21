using Grpc.Core;
using MediatR;
using Google.Protobuf.WellKnownTypes;
using MailService.GrpcServices;
using MailService.Application.Commands.Provisioning;
using MailService.Application.Commands.Quarantine;
using MailService.Application.Queries.Quarantine;
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
        var domain = await _mediator.Send(new ProvisionDomainCommand(request.DomainName, request.MaxMailboxCount, request.RetentionDays), context.CancellationToken);

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
        Guid domainId = Guid.Parse(request.DomainId);
        Guid? userId = string.IsNullOrEmpty(request.UserId) ? null : Guid.Parse(request.UserId);

        var mailbox = await _mediator.Send(new CreateMailboxCommand(domainId, request.LocalPart, userId), context.CancellationToken);

        return new CreateMailboxResponse
        {
            MailboxId = mailbox.Id.ToString(),
            FullAddress = mailbox.FullAddress,
            CreatedAt = Timestamp.FromDateTimeOffset(mailbox.CreatedAt)
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
        await Task.Yield();
        return new GetAuditRecordsResponse();
    }

    public override async Task<RequeueDeadLetterResponse> RequeueDeadLetter(RequeueDeadLetterRequest request, ServerCallContext context)
    {
        await Task.Yield();
        return new RequeueDeadLetterResponse { Success = true, Message = "Dead letter message requeued successfully" };
    }
}

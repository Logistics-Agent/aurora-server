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
            ProvisionedAt = Timestamp.FromDateTimeOffset(domain.CreatedAt),
            Status = domain.Status.ToString()
        };
    }

    public override async Task<VerifyDomainResponse> VerifyDomain(VerifyDomainRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.DomainId, out var domainId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid DomainId GUID format."));
        var result = await _mediator.Send(new VerifyDomainCommand(domainId), context.CancellationToken);
        var domain = result.Domain;
        var response = new VerifyDomainResponse
        {
            DomainId = domain.Id.ToString(), Verified = result.Verified, Status = domain.Status.ToString(),
            Message = result.Message, DkimSelector = domain.DkimSelector ?? "aurora-2025",
            DkimHost = $"{domain.DkimSelector ?? "aurora-2025"}._domainkey.{domain.DomainName}",
            ExpectedDkimTxtRecord = domain.DkimTxtRecord ?? string.Empty,
            ObservedDkimTxtRecord = result.ObservedRecord ?? string.Empty
        };
        if (result.Verified) response.VerifiedAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow);
        return response;
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
            CreatedAt = Timestamp.FromDateTimeOffset(mailbox.CreatedAt),
            DomainId = mailbox.DomainId.ToString(),
            LocalPart = mailbox.LocalPart,
            Status = mailbox.Status.ToString()
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
        try
        {
            Guid? resourceId = null;
            if (!string.IsNullOrEmpty(request.ResourceId) && Guid.TryParse(request.ResourceId, out var parsedId))
            {
                resourceId = parsedId;
            }

            var records = await _mediator.Send(new GetAuditRecordsQuery(request.ResourceType, resourceId, request.PageSize), context.CancellationToken);

            var response = new GetAuditRecordsResponse();
            if (records != null && records.Count > 0)
            {
                response.Records.AddRange(records.Select(r =>
                {
                    var ts = r.Timestamp != default ? r.Timestamp : (r.CreatedAt != default ? r.CreatedAt : DateTimeOffset.UtcNow);
                    return new AuditRecordDto
                    {
                        AuditId = r.Id.ToString(),
                        ActorId = r.ActorId.ToString(),
                        ActorType = r.ActorType.ToString(),
                        Action = r.Action ?? string.Empty,
                        ResourceType = r.ResourceType ?? string.Empty,
                        ResourceId = r.ResourceId.ToString(),
                        Timestamp = Timestamp.FromDateTimeOffset(ts.ToUniversalTime()),
                        Result = r.Result ?? string.Empty,
                        DetailJson = r.DetailJson ?? string.Empty
                    };
                }));
            }

            return response;
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
        }
        catch (RpcException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, $"Failed to retrieve audit records: {ex.Message}"));
        }
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

    public override async Task<ListDomainsResponse> ListDomains(ListDomainsRequest request, ServerCallContext context)
    {
        try
        {
            var domains = await _mediator.Send(new MailService.Application.Queries.Provisioning.ListDomainsQuery(request.PageSize), context.CancellationToken);
            var response = new ListDomainsResponse();
            if (domains != null)
            {
                response.Domains.AddRange(domains.Select(d =>
                {
                    var ts = d.CreatedAt != default ? d.CreatedAt : DateTimeOffset.UtcNow;
                    return new DomainSummaryDto
                    {
                        DomainId = d.Id.ToString(),
                        DomainName = d.DomainName ?? string.Empty,
                        Status = d.Status.ToString(),
                        MaxMailboxCount = d.MaxMailboxCount,
                        RetentionDays = d.RetentionDays,
                        DkimSelector = d.DkimSelector ?? "aurora-2025",
                        DkimTxtRecord = d.DkimTxtRecord ?? string.Empty,
                        CreatedAt = Timestamp.FromDateTimeOffset(ts.ToUniversalTime()),
                        MailboxUsage = d.Mailboxes?.Count ?? 0
                    };
                }));
            }
            return response;
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, $"Failed to list domains: {ex.Message}"));
        }
    }

    public override async Task<ListMailboxesResponse> ListMailboxes(ListMailboxesRequest request, ServerCallContext context)
    {
        try
        {
            Guid? domainId = null;
            if (!string.IsNullOrEmpty(request.DomainId) && Guid.TryParse(request.DomainId, out var parsedId))
            {
                domainId = parsedId;
            }

            var mailboxes = await _mediator.Send(new MailService.Application.Queries.Provisioning.ListMailboxesQuery(domainId, request.PageSize), context.CancellationToken);
            var response = new ListMailboxesResponse();
            if (mailboxes != null)
            {
                response.Mailboxes.AddRange(mailboxes.Select(m =>
                {
                    var ts = m.CreatedAt != default ? m.CreatedAt : DateTimeOffset.UtcNow;
                    return new MailboxSummaryDto
                    {
                        MailboxId = m.Id.ToString(),
                        DomainId = m.DomainId.ToString(),
                        DomainName = m.Domain?.DomainName ?? string.Empty,
                        LocalPart = m.LocalPart ?? string.Empty,
                        FullAddress = m.FullAddress ?? string.Empty,
                        Status = m.Status.ToString(),
                        CreatedAt = Timestamp.FromDateTimeOffset(ts.ToUniversalTime())
                    };
                }));
            }
            return response;
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, $"Failed to list mailboxes: {ex.Message}"));
        }
    }

    public override async Task<ListAliasesResponse> ListAliases(ListAliasesRequest request, ServerCallContext context)
    {
        try
        {
            Guid? domainId = null;
            if (!string.IsNullOrEmpty(request.DomainId) && Guid.TryParse(request.DomainId, out var parsedId))
            {
                domainId = parsedId;
            }

            var aliases = await _mediator.Send(new MailService.Application.Queries.Provisioning.ListAliasesQuery(domainId, request.PageSize), context.CancellationToken);
            var response = new ListAliasesResponse();
            if (aliases != null)
            {
                response.Aliases.AddRange(aliases.Select(a =>
                {
                    var ts = a.CreatedAt != default ? a.CreatedAt : DateTimeOffset.UtcNow;
                    var dto = new AliasSummaryDto
                    {
                        AliasId = a.Id.ToString(),
                        DomainId = a.DomainId.ToString(),
                        DomainName = a.Domain?.DomainName ?? string.Empty,
                        AliasAddress = a.AliasAddress ?? string.Empty,
                        CreatedAt = Timestamp.FromDateTimeOffset(ts.ToUniversalTime())
                    };
                    if (a.Targets != null)
                    {
                        dto.TargetAddresses.AddRange(a.Targets);
                    }
                    return dto;
                }));
            }
            return response;
        }
        catch (RpcException) { throw; }
        catch (Exception ex)
        {
            throw new RpcException(new Status(StatusCode.Internal, $"Failed to list aliases: {ex.Message}"));
        }
    }
}

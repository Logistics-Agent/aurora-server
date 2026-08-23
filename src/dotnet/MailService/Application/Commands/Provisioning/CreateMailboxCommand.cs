using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Security;
using MailService.Application.Interfaces.Stalwart;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Commands.Provisioning;

public record CreateMailboxCommand(Guid DomainId, string LocalPart, Guid? UserId) : IRequest<Mailbox>;

public class CreateMailboxCommandHandler : IRequestHandler<CreateMailboxCommand, Mailbox>
{
    private readonly MailServiceDbContext _dbContext;
    private readonly IStalwartManagementClient _stalwartClient;
    private readonly ICurrentUserService _currentUserService;

    public CreateMailboxCommandHandler(
        MailServiceDbContext dbContext,
        IStalwartManagementClient stalwartClient,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _stalwartClient = stalwartClient;
        _currentUserService = currentUserService;
    }

    public async Task<Mailbox> Handle(CreateMailboxCommand request, CancellationToken cancellationToken)
    {
        Guid tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var domain = await _dbContext.Domains.FindAsync([request.DomainId], cancellationToken);
        if (domain == null)
        {
            throw new InvalidOperationException($"Domain with ID '{request.DomainId}' not found.");
        }

        string fullAddress = $"{request.LocalPart.Trim().ToLowerInvariant()}@{domain.DomainName.ToLowerInvariant()}";

        // Check if mailbox already exists for repair / reconciliation
        var existingMailbox = await _dbContext.Mailboxes
            .FirstOrDefaultAsync(m => m.FullAddress == fullAddress, cancellationToken);

        if (existingMailbox != null)
        {
            await _stalwartClient.ProvisionAccountAsync(fullAddress, cancellationToken);
            return existingMailbox;
        }

        var mailbox = new Mailbox
        {
            TenantId = tenantId,
            DomainId = request.DomainId,
            LocalPart = request.LocalPart.Trim().ToLowerInvariant(),
            FullAddress = fullAddress,
            Status = MailboxStatus.Active,
            UserId = request.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Mailboxes.Add(mailbox);

        var audit = new AuditRecord
        {
            TenantId = tenantId,
            ActorId = _currentUserService.UserId ?? Guid.Empty,
            ActorType = ActorType.TenantAdmin,
            Action = "MailboxCreated",
            ResourceType = "Mailbox",
            ResourceId = mailbox.Id,
            Timestamp = DateTimeOffset.UtcNow,
            Result = "Success",
            DetailJson = JsonSerializer.Serialize(new { FullAddress = fullAddress, DomainId = request.DomainId, UserId = request.UserId })
        };
        _dbContext.AuditRecords.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _stalwartClient.ProvisionAccountAsync(fullAddress, cancellationToken);

        return mailbox;
    }
}

using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Security;
using MailService.Application.Interfaces.Stalwart;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.Messaging;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Commands.Provisioning;

public record CreateMailboxCommand(Guid DomainId, string LocalPart, Guid? UserId) : IRequest<Mailbox>;

public class CreateMailboxCommandHandler : IRequestHandler<CreateMailboxCommand, Mailbox>
{
    private readonly MailServiceDbContext _dbContext;
    private readonly IStalwartManagementClient _stalwartClient;
    private readonly ICurrentUserService _currentUserService;

    public CreateMailboxCommandHandler(MailServiceDbContext dbContext, IStalwartManagementClient stalwartClient, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _stalwartClient = stalwartClient;
        _currentUserService = currentUserService;
    }

    public async Task<Mailbox> Handle(CreateMailboxCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId is { } id && id != Guid.Empty
            ? id : throw new UnauthorizedAccessException("Tenant context is required to create a mailbox.");
        var domain = await _dbContext.Domains.FindAsync([request.DomainId], cancellationToken)
            ?? throw new KeyNotFoundException($"Domain with ID '{request.DomainId}' not found.");
        if (domain.Status != DomainStatus.Active)
            throw new InvalidOperationException($"Domain '{domain.DomainName}' must be verified before creating a shared mailbox.");

        var localPart = request.LocalPart.Trim().ToLowerInvariant();
        var fullAddress = $"{localPart}@{domain.DomainName.ToLowerInvariant()}";
        var existing = await _dbContext.Mailboxes.FirstOrDefaultAsync(m => m.FullAddress == fullAddress, cancellationToken);
        if (existing != null) return existing;

        if (!await _stalwartClient.ProvisionAccountAsync(fullAddress, cancellationToken))
            throw new InvalidOperationException("Stalwart could not provision the mailbox; no local mailbox was created.");

        var mailbox = new Mailbox
        {
            TenantId = tenantId, DomainId = request.DomainId, LocalPart = localPart, FullAddress = fullAddress,
            Status = MailboxStatus.Active, UserId = request.UserId, CreatedAt = DateTimeOffset.UtcNow
        };
        _dbContext.Mailboxes.Add(mailbox);
        var audit = new AuditRecord
        {
            TenantId = tenantId,
            ActorId = _currentUserService.UserId ?? Guid.Empty,
            ActorType = ActorType.TenantAdmin,
            Action = "SharedMailboxCreated",
            ResourceType = "Mailbox",
            ResourceId = mailbox.Id,
            Timestamp = DateTimeOffset.UtcNow,
            Result = "Success",
            DetailJson = JsonSerializer.Serialize(new { FullAddress = fullAddress, DomainId = request.DomainId, UserId = request.UserId })
        };
        _dbContext.AuditRecords.Add(audit);
        CentralAuditOutbox.Enqueue(_dbContext, audit);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return mailbox;
    }
}

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MailService.Application.Interfaces.Stalwart;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.Messaging;
using MailService.Infrastructure.Persistence;
using Shared.Security;

namespace MailService.Application.Commands.Provisioning;

public record CreateMailboxCommand(Guid DomainId, string LocalPart, Guid? UserId, bool IsShared = false, string? DisplayName = null) : IRequest<Mailbox>;

public class CreateMailboxCommandHandler : IRequestHandler<CreateMailboxCommand, Mailbox>
{
    private readonly MailServiceDbContext _dbContext;
    private readonly IStalwartManagementClient _stalwartClient;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHostEnvironment? _environment;
    private readonly ILogger<CreateMailboxCommandHandler>? _logger;

    public CreateMailboxCommandHandler(
        MailServiceDbContext dbContext,
        IStalwartManagementClient stalwartClient,
        ICurrentUserService currentUserService,
        IHostEnvironment? environment = null,
        ILogger<CreateMailboxCommandHandler>? logger = null)
    {
        _dbContext = dbContext;
        _stalwartClient = stalwartClient;
        _currentUserService = currentUserService;
        _environment = environment;
        _logger = logger;
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

        // Ensure Domain has valid StalwartDomainId
        if (string.IsNullOrEmpty(domain.StalwartDomainId))
        {
            domain.StalwartDomainId = await _stalwartClient.ResolveOrCreateStalwartDomainIdAsync(domain.DomainName, cancellationToken);
            domain.LastDomainSyncedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        // PHASE 1: Save to Aurora DB with status Pending
        var mailbox = new Mailbox
        {
            TenantId = tenantId,
            DomainId = request.DomainId,
            LocalPart = localPart,
            FullAddress = fullAddress,
            Type = request.IsShared ? MailboxType.Shared : MailboxType.User,
            Status = MailboxStatus.Active,
            ProvisioningStatus = ProvisioningStatus.Pending,
            UserId = request.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Mailboxes.Add(mailbox);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // PHASE 2: Provision to Stalwart JMAP
        var provisionResult = await _stalwartClient.ProvisionAccountAsync(
            localPart, 
            domain.StalwartDomainId ?? domain.DomainName, 
            request.DisplayName, 
            cancellationToken);

        // PHASE 3: Update DB with Provisioned or Failed
        if (provisionResult.IsSuccess)
        {
            mailbox.ProvisioningStatus = ProvisioningStatus.Provisioned;
            mailbox.StalwartAccountId = provisionResult.StalwartAccountId;
            mailbox.LastProvisionedAt = DateTimeOffset.UtcNow;
            mailbox.ProvisioningError = null;
        }
        else
        {
            mailbox.ProvisioningStatus = ProvisioningStatus.Failed;
            mailbox.ProvisioningError = provisionResult.ErrorMessage;
            _logger?.LogWarning("Stalwart JMAP could not provision account for {Address}: {Error}", fullAddress, provisionResult.ErrorMessage);
        }

        var audit = new AuditRecord
        {
            TenantId = tenantId,
            ActorId = _currentUserService.UserId ?? Guid.Empty,
            ActorType = ActorType.TenantAdmin,
            Action = "MailboxCreated",
            ResourceType = "Mailbox",
            ResourceId = mailbox.Id,
            Timestamp = DateTimeOffset.UtcNow,
            Result = provisionResult.IsSuccess ? "Success" : "PendingProvisioning",
            DetailJson = JsonSerializer.Serialize(new 
            { 
                FullAddress = fullAddress, 
                DomainId = request.DomainId, 
                UserId = request.UserId,
                StalwartAccountId = mailbox.StalwartAccountId,
                ProvisioningStatus = mailbox.ProvisioningStatus.ToString()
            })
        };

        _dbContext.AuditRecords.Add(audit);
        CentralAuditOutbox.Enqueue(_dbContext, audit);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return mailbox;
    }
}

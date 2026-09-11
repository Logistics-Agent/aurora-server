using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.Exceptions;
using Shared.Security;
using MailService.Application.Interfaces.Stalwart;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.Messaging;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Commands.Provisioning;

public record ProvisionDomainCommand(string DomainName, int MaxMailboxCount = 100, int RetentionDays = 365) : IRequest<Domain.Entities.Domain>;

public class ProvisionDomainCommandHandler : IRequestHandler<ProvisionDomainCommand, Domain.Entities.Domain>
{
    private readonly MailServiceDbContext _dbContext;
    private readonly IStalwartManagementClient _stalwartClient;
    private readonly ICurrentUserService _currentUserService;

    public ProvisionDomainCommandHandler(MailServiceDbContext dbContext, IStalwartManagementClient stalwartClient, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _stalwartClient = stalwartClient;
        _currentUserService = currentUserService;
    }

    public async Task<Domain.Entities.Domain> Handle(ProvisionDomainCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUserService.TenantId is { } id && id != Guid.Empty
            ? id : throw new UnauthorizedAccessException("Tenant context is required to provision a mail domain.");
        var domainName = request.DomainName.Trim().TrimEnd('.').ToLowerInvariant();

        var existing = await _dbContext.Domains.IgnoreQueryFilters()
            .SingleOrDefaultAsync(d => d.DomainName == domainName, cancellationToken);
        if (existing != null)
        {
            if (existing.TenantId == tenantId) return existing;
            throw new ConflictException($"Mail domain '{domainName}' is already registered.");
        }

        if (!await _stalwartClient.RegisterDomainAsync(domainName, cancellationToken))
            throw new PolicyUnavailableException("Stalwart could not register the mail domain.");

        const string selector = "aurora-2025";
        var dkimTxt = await _stalwartClient.GenerateDkimKeyAsync(domainName, selector, cancellationToken);
        if (string.IsNullOrWhiteSpace(dkimTxt))
            throw new PolicyUnavailableException("Stalwart did not return a DKIM record.");

        var domain = new Domain.Entities.Domain
        {
            TenantId = tenantId,
            DomainName = domainName,
            Status = DomainStatus.Pending,
            MaxMailboxCount = request.MaxMailboxCount,
            RetentionDays = request.RetentionDays,
            DkimSelector = selector,
            DkimTxtRecord = dkimTxt,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _dbContext.Domains.Add(domain);

        // Auto-provision default shared department mailbox: operations@<domain>
        var opsEmail = $"operations@{domainName}";
        var opsMailbox = new Mailbox
        {
            TenantId = tenantId,
            DomainId = domain.Id,
            LocalPart = "operations",
            FullAddress = opsEmail,
            Status = MailboxStatus.Active,
            UserId = null, // Shared mailbox (not tied to a specific user)
            CreatedAt = DateTimeOffset.UtcNow
        };
        _dbContext.Mailboxes.Add(opsMailbox);

        // Provision Stalwart account for operations mailbox
        await _stalwartClient.ProvisionAccountAsync(opsEmail, cancellationToken);

        var audit = new AuditRecord
        {
            TenantId = tenantId,
            ActorId = _currentUserService.UserId ?? Guid.Empty,
            ActorType = ActorType.TenantAdmin,
            Action = "MailDomainProvisioned",
            ResourceType = "MailDomain",
            ResourceId = domain.Id,
            Timestamp = DateTimeOffset.UtcNow,
            Result = "Success",
            DetailJson = JsonSerializer.Serialize(new { DomainName = domainName, Status = domain.Status.ToString(), DkimSelector = selector, DefaultMailbox = opsEmail })
        };
        _dbContext.AuditRecords.Add(audit);
        CentralAuditOutbox.Enqueue(_dbContext, audit);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ConflictException($"Mail domain '{domainName}' is already registered.");
        }
        return domain;
    }
}

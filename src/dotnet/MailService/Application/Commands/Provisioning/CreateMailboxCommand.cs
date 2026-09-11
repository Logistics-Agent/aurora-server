using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Shared.Security;
using MailService.Application.Interfaces.Stalwart;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.Messaging;
using MailService.Infrastructure.Persistence;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MailService.Application.Commands.Provisioning;

public record CreateMailboxCommand(Guid DomainId, string LocalPart, Guid? UserId) : IRequest<Mailbox>;

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

        var provisioned = await _stalwartClient.ProvisionAccountAsync(fullAddress, cancellationToken);
        if (!provisioned)
        {
            _logger?.LogWarning("Stalwart could not provision account for {Address} (management API offline or unreachable). Proceeding with database mailbox creation and audit sync.", fullAddress);
        }

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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Shared.Security;
using MailService.Application.Interfaces.Stalwart;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Commands.Provisioning;

public record CreateAliasCommand(Guid DomainId, string AliasAddress, List<string> TargetAddresses) : IRequest<Alias>;

public class CreateAliasCommandHandler : IRequestHandler<CreateAliasCommand, Alias>
{
    private readonly MailServiceDbContext _dbContext;
    private readonly IStalwartManagementClient _stalwartClient;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<CreateAliasCommandHandler>? _logger;

    public CreateAliasCommandHandler(
        MailServiceDbContext dbContext,
        IStalwartManagementClient stalwartClient,
        ICurrentUserService currentUserService,
        ILogger<CreateAliasCommandHandler>? logger = null)
    {
        _dbContext = dbContext;
        _stalwartClient = stalwartClient;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<Alias> Handle(CreateAliasCommand request, CancellationToken cancellationToken)
    {
        Guid tenantId = _currentUserService.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required to create an alias.");

        var domain = await _dbContext.Domains.FindAsync([request.DomainId], cancellationToken);
        if (domain == null)
        {
            throw new KeyNotFoundException($"Domain with ID '{request.DomainId}' not found for current tenant.");
        }

        if (domain.Status != DomainStatus.Active)
        {
            throw new InvalidOperationException($"Domain '{domain.DomainName}' must be verified before creating an alias.");
        }

        string aliasAddress = request.AliasAddress.Trim().ToLowerInvariant();

        if (!await _stalwartClient.CreateAliasAsync(aliasAddress, request.TargetAddresses, cancellationToken))
        {
            _logger?.LogWarning("Stalwart could not provision alias {Alias} (management API offline or unreachable). Proceeding with database alias creation.", aliasAddress);
        }

        var alias = new Alias
        {
            TenantId = tenantId,
            DomainId = request.DomainId,
            AliasAddress = aliasAddress,
            Targets = request.TargetAddresses,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Aliases.Add(alias);

        var audit = new AuditRecord
        {
            TenantId = tenantId,
            ActorId = _currentUserService.UserId ?? Guid.Empty,
            ActorType = ActorType.TenantAdmin,
            Action = "MailAliasCreated",
            ResourceType = "Alias",
            ResourceId = alias.Id,
            Timestamp = DateTimeOffset.UtcNow,
            Result = "Success",
            DetailJson = System.Text.Json.JsonSerializer.Serialize(new { AliasAddress = aliasAddress, DomainId = request.DomainId, Targets = request.TargetAddresses })
        };
        _dbContext.AuditRecords.Add(audit);
        MailService.Infrastructure.Messaging.CentralAuditOutbox.Enqueue(_dbContext, audit);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return alias;
    }
}

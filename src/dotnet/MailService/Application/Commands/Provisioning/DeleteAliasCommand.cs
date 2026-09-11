using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Security;
using MailService.Application.Interfaces.Stalwart;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Commands.Provisioning;

public record DeleteAliasCommand(Guid AliasId) : IRequest<bool>;

public class DeleteAliasCommandHandler : IRequestHandler<DeleteAliasCommand, bool>
{
    private readonly MailServiceDbContext _dbContext;
    private readonly IStalwartManagementClient _stalwartClient;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<DeleteAliasCommandHandler>? _logger;

    public DeleteAliasCommandHandler(
        MailServiceDbContext dbContext,
        IStalwartManagementClient stalwartClient,
        ICurrentUserService currentUserService,
        ILogger<DeleteAliasCommandHandler>? logger = null)
    {
        _dbContext = dbContext;
        _stalwartClient = stalwartClient;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteAliasCommand request, CancellationToken cancellationToken)
    {
        Guid tenantId = _currentUserService.TenantId
            ?? throw new UnauthorizedAccessException("Tenant context is required to delete an alias.");

        var alias = await _dbContext.Aliases
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == request.AliasId, cancellationToken);

        if (alias == null)
        {
            throw new KeyNotFoundException($"Alias with ID '{request.AliasId}' not found for current tenant.");
        }

        if (!await _stalwartClient.DeleteAliasAsync(alias.AliasAddress, cancellationToken))
        {
            _logger?.LogWarning("Stalwart could not delete alias {Alias} (management API offline or unreachable). Proceeding with database alias deletion.", alias.AliasAddress);
        }

        _dbContext.Aliases.Remove(alias);

        var audit = new AuditRecord
        {
            TenantId = tenantId,
            ActorId = _currentUserService.UserId ?? Guid.Empty,
            ActorType = ActorType.TenantAdmin,
            Action = "MailAliasDeleted",
            ResourceType = "Alias",
            ResourceId = alias.Id,
            Timestamp = DateTimeOffset.UtcNow,
            Result = "Success",
            DetailJson = System.Text.Json.JsonSerializer.Serialize(new { AliasId = alias.Id, AliasAddress = alias.AliasAddress, DomainId = alias.DomainId })
        };
        _dbContext.AuditRecords.Add(audit);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

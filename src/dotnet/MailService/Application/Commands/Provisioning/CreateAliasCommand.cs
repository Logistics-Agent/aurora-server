using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Shared.Security;
using MailService.Application.Interfaces.Stalwart;
using MailService.Domain.Entities;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Commands.Provisioning;

public record CreateAliasCommand(Guid DomainId, string AliasAddress, List<string> TargetAddresses) : IRequest<Alias>;

public class CreateAliasCommandHandler : IRequestHandler<CreateAliasCommand, Alias>
{
    private readonly MailServiceDbContext _dbContext;
    private readonly IStalwartManagementClient _stalwartClient;
    private readonly ICurrentUserService _currentUserService;

    public CreateAliasCommandHandler(
        MailServiceDbContext dbContext,
        IStalwartManagementClient stalwartClient,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _stalwartClient = stalwartClient;
        _currentUserService = currentUserService;
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

        string aliasAddress = request.AliasAddress.Trim().ToLowerInvariant();

        var alias = new Alias
        {
            TenantId = tenantId,
            DomainId = request.DomainId,
            AliasAddress = aliasAddress,
            Targets = request.TargetAddresses,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Aliases.Add(alias);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _stalwartClient.CreateAliasAsync(aliasAddress, request.TargetAddresses, cancellationToken);

        return alias;
    }
}

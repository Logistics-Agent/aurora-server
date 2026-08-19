using MediatR;
using Shared.Security;
using MailService.Application.Interfaces;
using MailService.Application.Pipeline;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Commands.Provisioning;

public record ProvisionDomainCommand(string DomainName, int MaxMailboxCount = 100, int RetentionDays = 365) : IRequest<Domain.Entities.Domain>;

public class ProvisionDomainCommandHandler : IRequestHandler<ProvisionDomainCommand, Domain.Entities.Domain>
{
    private readonly MailServiceDbContext _dbContext;
    private readonly IStalwartManagementClient _stalwartClient;
    private readonly ICurrentUserService _currentUserService;

    public ProvisionDomainCommandHandler(
        MailServiceDbContext dbContext,
        IStalwartManagementClient stalwartClient,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _stalwartClient = stalwartClient;
        _currentUserService = currentUserService;
    }

    public async Task<Domain.Entities.Domain> Handle(ProvisionDomainCommand request, CancellationToken cancellationToken)
    {
        Guid tenantId = _currentUserService.TenantId ?? Guid.Empty;
        var domain = new Domain.Entities.Domain
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            DomainName = request.DomainName.Trim().ToLowerInvariant(),
            Status = DomainStatus.Active,
            MaxMailboxCount = request.MaxMailboxCount,
            RetentionDays = request.RetentionDays,
            DkimSelector = "aurora-2025",
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Domains.Add(domain);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Instruct Stalwart via HTTP management API
        await _stalwartClient.RegisterDomainAsync(domain.DomainName, cancellationToken);
        string dkimTxt = await _stalwartClient.GenerateDkimKeyAsync(domain.DomainName, "aurora-2025", cancellationToken);

        domain.DkimTxtRecord = dkimTxt;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return domain;
    }
}

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
        var domain = await _dbContext.Domains.FindAsync(new object[] { request.DomainId }, cancellationToken);
        if (domain == null)
        {
            throw new InvalidOperationException($"Domain with ID '{request.DomainId}' not found.");
        }

        string fullAddress = $"{request.LocalPart.Trim().ToLowerInvariant()}@{domain.DomainName}";

        var mailbox = new Mailbox
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            DomainId = request.DomainId,
            LocalPart = request.LocalPart.Trim().ToLowerInvariant(),
            FullAddress = fullAddress,
            Status = MailboxStatus.Active,
            UserId = request.UserId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Mailboxes.Add(mailbox);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _stalwartClient.ProvisionAccountAsync(fullAddress, cancellationToken);

        return mailbox;
    }
}

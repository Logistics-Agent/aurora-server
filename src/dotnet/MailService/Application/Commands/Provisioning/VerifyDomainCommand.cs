using System.Text.Json;
using MediatR;
using Shared.Security;
using MailService.Application.Interfaces.Security;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.Messaging;
using MailService.Infrastructure.Persistence;

namespace MailService.Application.Commands.Provisioning;

public record VerifyDomainCommand(Guid DomainId) : IRequest<DomainVerificationResult>;
public record DomainVerificationResult(Domain.Entities.Domain Domain, bool Verified, string Message, string? ObservedRecord);

public class VerifyDomainCommandHandler : IRequestHandler<VerifyDomainCommand, DomainVerificationResult>
{
    private readonly MailServiceDbContext _dbContext;
    private readonly IDnsLookupService _dns;
    private readonly ICurrentUserService _currentUser;

    public VerifyDomainCommandHandler(MailServiceDbContext dbContext, IDnsLookupService dns, ICurrentUserService currentUser)
    { _dbContext = dbContext; _dns = dns; _currentUser = currentUser; }

    public async Task<DomainVerificationResult> Handle(VerifyDomainCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId is { } id && id != Guid.Empty
            ? id : throw new UnauthorizedAccessException("Tenant context is required to verify a mail domain.");
        var domain = await _dbContext.Domains.FindAsync([request.DomainId], cancellationToken)
            ?? throw new KeyNotFoundException($"Domain with ID '{request.DomainId}' not found.");
        var selector = domain.DkimSelector ?? "aurora-2025";
        var observed = await _dns.GetDkimRecordAsync(domain.DomainName, selector, cancellationToken);
        var expected = Normalize(domain.DkimTxtRecord);
        var verified = expected.Length > 0 && string.Equals(expected, Normalize(observed), StringComparison.OrdinalIgnoreCase);
        domain.Status = verified ? DomainStatus.Active : DomainStatus.Pending;
        var message = verified ? "DKIM DNS record verified." : "DKIM record is missing or does not match; wait for DNS propagation and retry.";

        var audit = new AuditRecord
        {
            TenantId = tenantId, ActorId = _currentUser.UserId ?? Guid.Empty, ActorType = ActorType.TenantAdmin,
            Action = verified ? "MailDomainVerified" : "MailDomainVerificationFailed", ResourceType = "MailDomain",
            ResourceId = domain.Id, Timestamp = DateTimeOffset.UtcNow, Result = verified ? "Success" : "Failure",
            DetailJson = JsonSerializer.Serialize(new { domain.DomainName, Selector = selector, Observed = observed, Message = message })
        };
        _dbContext.AuditRecords.Add(audit);
        CentralAuditOutbox.Enqueue(_dbContext, audit);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new DomainVerificationResult(domain, verified, message, observed);
    }

    private static string Normalize(string? value) => string.Concat((value ?? string.Empty).Where(c => !char.IsWhiteSpace(c) && c != '"'));
}

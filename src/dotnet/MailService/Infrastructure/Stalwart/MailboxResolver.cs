using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MailService.Application.Interfaces.Stalwart;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.Persistence;

namespace MailService.Infrastructure.Stalwart;

public class MailboxResolver : IMailboxResolver
{
    private readonly MailServiceDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly ILogger<MailboxResolver> _logger;

    public MailboxResolver(
        MailServiceDbContext dbContext,
        IMemoryCache cache,
        ILogger<MailboxResolver> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task<MailboxResolutionResult> ResolveMailboxByStalwartAccountIdAsync(
        string stalwartAccountId, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(stalwartAccountId))
        {
            return new MailboxResolutionResult(false, Guid.Empty, Guid.Empty, Guid.Empty, string.Empty, MailboxType.User, null, FailureReason: "Empty StalwartAccountId");
        }

        var cacheKey = $"mb_accid:{stalwartAccountId.Trim()}";
        if (_cache.TryGetValue(cacheKey, out MailboxResolutionResult? cached) && cached != null)
        {
            return cached;
        }

        var mailbox = await _dbContext.Mailboxes
            .Include(m => m.Domain)
            .IgnoreQueryFilters() // Inbound resolution occurs before tenant context is set
            .FirstOrDefaultAsync(m => m.StalwartAccountId == stalwartAccountId && m.Status == MailboxStatus.Active, cancellationToken);

        if (mailbox != null)
        {
            var result = new MailboxResolutionResult(
                true,
                mailbox.TenantId,
                mailbox.DomainId,
                mailbox.Id,
                mailbox.FullAddress,
                mailbox.Type,
                mailbox.UserId,
                mailbox.StalwartAccountId);

            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(10));
            return result;
        }

        _logger.LogInformation("No active mailbox found for StalwartAccountId {StalwartAccountId}", stalwartAccountId);
        return new MailboxResolutionResult(false, Guid.Empty, Guid.Empty, Guid.Empty, string.Empty, MailboxType.User, null, FailureReason: $"No mailbox for accountId {stalwartAccountId}");
    }

    public async Task<MailboxResolutionResult> ResolveMailboxByAddressAsync(
        string recipientAddress, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientAddress))
        {
            return new MailboxResolutionResult(false, Guid.Empty, Guid.Empty, Guid.Empty, string.Empty, MailboxType.User, null, FailureReason: "Empty recipient address");
        }

        var normalized = recipientAddress.Trim().ToLowerInvariant();
        var cacheKey = $"mb_addr:{normalized}";
        if (_cache.TryGetValue(cacheKey, out MailboxResolutionResult? cached) && cached != null)
        {
            return cached;
        }

        // 1. Direct mailbox match
        var mailbox = await _dbContext.Mailboxes
            .Include(m => m.Domain)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.FullAddress == normalized && m.Status == MailboxStatus.Active, cancellationToken);

        if (mailbox != null)
        {
            var result = new MailboxResolutionResult(
                true,
                mailbox.TenantId,
                mailbox.DomainId,
                mailbox.Id,
                mailbox.FullAddress,
                mailbox.Type,
                mailbox.UserId,
                mailbox.StalwartAccountId);

            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(10));
            return result;
        }

        // 2. Alias match
        var alias = await _dbContext.Aliases
            .Include(a => a.Domain)
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.AliasAddress == normalized, cancellationToken);

        if (alias != null && alias.Targets.Count > 0)
        {
            var primaryTarget = alias.Targets[0].Trim().ToLowerInvariant();
            var targetMailbox = await _dbContext.Mailboxes
                .Include(m => m.Domain)
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(m => m.FullAddress == primaryTarget && m.Status == MailboxStatus.Active, cancellationToken);

            if (targetMailbox != null)
            {
                var result = new MailboxResolutionResult(
                    true,
                    targetMailbox.TenantId,
                    targetMailbox.DomainId,
                    targetMailbox.Id,
                    targetMailbox.FullAddress,
                    targetMailbox.Type,
                    targetMailbox.UserId,
                    targetMailbox.StalwartAccountId);

                _cache.Set(cacheKey, result, TimeSpan.FromMinutes(10));
                return result;
            }
        }

        _logger.LogWarning("Recipient address {Recipient} not found in Aurora mailboxes or aliases", normalized);
        return new MailboxResolutionResult(false, Guid.Empty, Guid.Empty, Guid.Empty, normalized, MailboxType.User, null, FailureReason: $"Mailbox not found for address {normalized}");
    }
}

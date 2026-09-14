using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MailService.Application.Interfaces.Stalwart;
using MailService.Domain.Entities;
using MailService.Infrastructure.Persistence;

namespace MailService.Infrastructure.Stalwart;

public class MailboxReconciliationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MailboxReconciliationWorker> _logger;

    public MailboxReconciliationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<MailboxReconciliationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MailboxReconciliationWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcilePendingMailboxesAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred during mailbox reconciliation cycle.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ReconcilePendingMailboxesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MailServiceDbContext>();
        var stalwartClient = scope.ServiceProvider.GetRequiredService<IStalwartManagementClient>();

        var unprovisioned = await db.Mailboxes
            .Include(m => m.Domain)
            .IgnoreQueryFilters()
            .Where(m => m.ProvisioningStatus == ProvisioningStatus.Pending || m.ProvisioningStatus == ProvisioningStatus.Failed)
            .Take(25)
            .ToListAsync(ct);

        if (unprovisioned.Count == 0) return;

        _logger.LogInformation("Reconciling {Count} pending/failed mailboxes with Stalwart infrastructure.", unprovisioned.Count);

        foreach (var mb in unprovisioned)
        {
            if (mb.Domain == null) continue;

            // 1. Ensure StalwartDomainId is populated
            if (string.IsNullOrEmpty(mb.Domain.StalwartDomainId))
            {
                mb.Domain.StalwartDomainId = await stalwartClient.ResolveOrCreateStalwartDomainIdAsync(mb.Domain.DomainName, ct);
                mb.Domain.LastDomainSyncedAt = DateTimeOffset.UtcNow;
            }

            var domainId = mb.Domain.StalwartDomainId ?? mb.Domain.DomainName;

            // 2. Idempotent search: Check if account already exists on Stalwart
            var existingAccountId = await stalwartClient.FindExistingAccountIdAsync(mb.LocalPart, domainId, ct);

            if (!string.IsNullOrEmpty(existingAccountId))
            {
                // Account already exists on Stalwart -> Link ID and mark Provisioned
                mb.StalwartAccountId = existingAccountId;
                mb.ProvisioningStatus = ProvisioningStatus.Provisioned;
                mb.LastReconciledAt = DateTimeOffset.UtcNow;
                mb.ProvisioningError = null;
                _logger.LogInformation("Reconciled existing Stalwart account {AccountId} for {Address}", existingAccountId, mb.FullAddress);
            }
            else
            {
                // Account missing on Stalwart -> Provision new account
                var res = await stalwartClient.ProvisionAccountAsync(mb.LocalPart, domainId, null, ct);
                if (res.IsSuccess)
                {
                    mb.StalwartAccountId = res.StalwartAccountId;
                    mb.ProvisioningStatus = ProvisioningStatus.Provisioned;
                    mb.LastReconciledAt = DateTimeOffset.UtcNow;
                    mb.ProvisioningError = null;
                    _logger.LogInformation("Successfully provisioned missing Stalwart account {AccountId} for {Address}", res.StalwartAccountId, mb.FullAddress);
                }
                else
                {
                    mb.ProvisioningStatus = ProvisioningStatus.Failed;
                    mb.ProvisioningError = res.ErrorMessage;
                    _logger.LogWarning("Reconciliation provisioning failed for {Address}: {Error}", mb.FullAddress, res.ErrorMessage);
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }
}

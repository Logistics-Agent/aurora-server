using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Events;
using MailService.Application.Interfaces.Stalwart;
using MailService.Domain.Entities;
using MailService.Domain.Enums;
using MailService.Infrastructure.Persistence;

namespace MailService.Infrastructure.Messaging.Consumers;

/// <summary>
/// Event consumer for automatic, idempotent mailbox provisioning when
/// a Tenant Admin or Staff member is created in IamTenant.
/// </summary>
public class TenantUserProvisionedConsumer(
    MailServiceDbContext dbContext,
    IStalwartManagementClient stalwartClient,
    ILogger<TenantUserProvisionedConsumer> logger)
    : IConsumer<TenantAdminCreatedEvent>,
      IConsumer<TenantStaffCreatedEvent>
{
    public async Task Consume(ConsumeContext<TenantAdminCreatedEvent> context)
    {
        var msg = context.Message;
        logger.LogInformation(
            "Received TenantAdminCreatedEvent: Tenant {TenantId}, User {UserId}, Email {Email}, EventId {EventId}",
            msg.TenantId, msg.UserId, msg.Email, context.MessageId);

        await ProvisionMailboxInternalAsync(
            msg.TenantId,
            msg.UserId,
            msg.Email,
            sourceEventName: nameof(TenantAdminCreatedEvent),
            eventId: context.MessageId?.ToString(),
            cancellationToken: context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<TenantStaffCreatedEvent> context)
    {
        var msg = context.Message;
        logger.LogInformation(
            "Received TenantStaffCreatedEvent: Tenant {TenantId}, User {UserId}, Email {Email}, EventId {EventId}",
            msg.TenantId, msg.UserId, msg.Email, context.MessageId);

        await ProvisionMailboxInternalAsync(
            msg.TenantId,
            msg.UserId,
            msg.Email,
            sourceEventName: nameof(TenantStaffCreatedEvent),
            eventId: context.MessageId?.ToString(),
            cancellationToken: context.CancellationToken);
    }

    private async Task ProvisionMailboxInternalAsync(
        Guid tenantId,
        Guid userId,
        string rawEmail,
        string sourceEventName,
        string? eventId,
        CancellationToken cancellationToken)
    {
        // 1. Authoritative Payload Validation
        if (tenantId == Guid.Empty)
        {
            logger.LogError("Invalid provisioning event: TenantId is empty.");
            throw new ArgumentException("TenantId cannot be empty in provisioning event.");
        }

        if (string.IsNullOrWhiteSpace(rawEmail) || !rawEmail.Contains('@'))
        {
            logger.LogError("Invalid provisioning event: Email '{Email}' is malformed.", rawEmail);
            throw new ArgumentException($"Invalid email format: '{rawEmail}'.");
        }

        var normalizedEmail = rawEmail.Trim().ToLowerInvariant();
        var emailParts = normalizedEmail.Split('@');
        var localPart = emailParts[0];
        var domainName = emailParts[1];

        // 2. Cross-Tenant Protection & Domain Verification
        var existingAnyDomain = await dbContext.Domains
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.DomainName.ToLower() == domainName, cancellationToken);

        if (existingAnyDomain != null && existingAnyDomain.TenantId != tenantId)
        {
            logger.LogError(
                "Security violation: Tenant {TenantId} attempted to provision mailbox on Domain {Domain} owned by Tenant {OwnerTenantId}",
                tenantId, domainName, existingAnyDomain.TenantId);
            throw new InvalidOperationException($"Security violation: Domain '{domainName}' belongs to another tenant.");
        }

        var tenantDomain = existingAnyDomain;
        if (tenantDomain == null)
        {
            // Auto-register tenant domain if not present
            tenantDomain = new MailService.Domain.Entities.Domain
            {
                TenantId = tenantId,
                DomainName = domainName,
                Status = DomainStatus.Active,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Domains.Add(tenantDomain);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Auto-registered Domain {Domain} for Tenant {TenantId}", domainName, tenantId);
        }

        // 3. Idempotency & Reconciliation Barrier
        var existingMailbox = await dbContext.Mailboxes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(m => m.FullAddress.ToLower() == normalizedEmail || (m.TenantId == tenantId && m.UserId == userId), cancellationToken);

        if (existingMailbox != null)
        {
            // Security check: mailbox cannot belong to another tenant
            if (existingMailbox.TenantId != tenantId)
            {
                logger.LogError(
                    "Security violation: Mailbox {Email} already exists for Tenant {OwnerTenantId}, not Tenant {TenantId}",
                    normalizedEmail, existingMailbox.TenantId, tenantId);
                throw new InvalidOperationException($"Security violation: Mailbox '{normalizedEmail}' belongs to another tenant.");
            }

            // Ensure Stalwart account exists
            await stalwartClient.ProvisionAccountAsync(normalizedEmail, cancellationToken);

            if (existingMailbox.UserId == null && userId != Guid.Empty)
            {
                existingMailbox.UserId = userId;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            logger.LogInformation(
                "Mailbox {Email} already exists for Tenant {TenantId} (Id: {MailboxId}). Reconciled Stalwart account.",
                normalizedEmail, tenantId, existingMailbox.Id);
            return;
        }

        // 4. Provision in Stalwart first
        await stalwartClient.ProvisionAccountAsync(normalizedEmail, cancellationToken);

        // 5. Persist Mailbox entity in MailService DB
        var mailbox = new Mailbox
        {
            TenantId = tenantId,
            DomainId = tenantDomain.Id,
            LocalPart = localPart,
            FullAddress = normalizedEmail,
            Status = MailboxStatus.Active,
            UserId = userId != Guid.Empty ? userId : null,
            SourceEventId = eventId ?? Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Mailboxes.Add(mailbox);

        // 6. Record Audit Trail
        var audit = new AuditRecord
        {
            TenantId = tenantId,
            ActorId = userId != Guid.Empty ? userId : Guid.Empty,
            ActorType = ActorType.System,
            Action = "MailboxProvisioned",
            ResourceType = "Mailbox",
            ResourceId = mailbox.Id,
            Timestamp = DateTimeOffset.UtcNow,
            Result = "Success",
            DetailJson = JsonSerializer.Serialize(new
            {
                Email = normalizedEmail,
                UserId = userId,
                EventId = eventId,
                Source = sourceEventName
            })
        };

        dbContext.AuditRecords.Add(audit);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Successfully auto-provisioned Mailbox {Email} (Id: {MailboxId}) for Tenant {TenantId} via {SourceEvent}",
            normalizedEmail, mailbox.Id, tenantId, sourceEventName);
    }
}

using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using MailService.Application.Interfaces.Stalwart;
using Shared.Events;

namespace MailService.Infrastructure.Messaging.Consumers;

public class TenantUserProvisionedConsumer(
    IStalwartManagementClient stalwartClient,
    ILogger<TenantUserProvisionedConsumer> logger)
    : IConsumer<TenantAdminCreatedEvent>,
      IConsumer<TenantStaffCreatedEvent>
{
    public async Task Consume(ConsumeContext<TenantAdminCreatedEvent> context)
    {
        var msg = context.Message;
        logger.LogInformation("Provisioning Stalwart Mailbox for TenantAdmin: Tenant {TenantId}, Email {Email}", msg.TenantId, msg.Email);
        await stalwartClient.ProvisionAccountAsync(msg.Email, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<TenantStaffCreatedEvent> context)
    {
        var msg = context.Message;
        logger.LogInformation("Provisioning Stalwart Mailbox for TenantStaff: Tenant {TenantId}, Email {Email}", msg.TenantId, msg.Email);
        await stalwartClient.ProvisionAccountAsync(msg.Email, context.CancellationToken);
    }
}

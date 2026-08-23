using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using MailService.Application.Interfaces.Stalwart;
using Shared.Events;

namespace MailService.Infrastructure.Messaging.Consumers;

public class SendSystemEmailConsumer(
    ILogger<SendSystemEmailConsumer> logger) : IConsumer<SendSystemEmailCommand>
{
    public async Task Consume(ConsumeContext<SendSystemEmailCommand> context)
    {
        var msg = context.Message;
        logger.LogInformation("Processing SendSystemEmailCommand for Tenant {TenantId} to {Recipients}", msg.TenantId, string.Join(", ", msg.RecipientEmails));

        // Consume system email command
        await Task.CompletedTask;
    }
}

using System.Threading.Tasks;
using MassTransit;
using RoutePlanningAgent.Application.Interfaces;
using Shared.Events;

namespace RoutePlanningAgent.Infrastructure.Consumers;

public class TenantAiConfigChangedConsumer(ITenantAiConfigService configService)
    : IConsumer<TenantAiConfigChangedEvent>
{
    public async Task Consume(ConsumeContext<TenantAiConfigChangedEvent> context)
    {
        await configService.InvalidateCacheAsync(
            context.Message.TenantId, context.Message.Feature, context.CancellationToken);
    }
}

using System.Threading.Tasks;
using MassTransit;
using RoutePlanningAgent.Application.Interfaces;
using Shared.Events;

namespace RoutePlanningAgent.Infrastructure.Consumers;

public class TenantRuleConfigChangedConsumer(ITenantRuleConfigService configService)
    : IConsumer<TenantRuleConfigChangedEvent>
{
    public async Task Consume(ConsumeContext<TenantRuleConfigChangedEvent> context)
    {
        await configService.InvalidateCacheAsync(
            context.Message.TenantId, context.Message.RuleName, context.CancellationToken);
    }
}

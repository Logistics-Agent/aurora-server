using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using RoutePlanningAgent.Infrastructure.Persistences;
using Shared.Events;

namespace RoutePlanningAgent.Infrastructure.BackgroundJobs;

/// <summary>
/// Relay các OutboxMessage chưa xử lý lên RabbitMQ theo chu kỳ.
/// Cấu hình qua section "Outbox": PollingSeconds, BatchSize, MaxRetry.
/// </summary>
public class OutboxProcessorBackgroundService(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<OutboxProcessorBackgroundService> logger) : BackgroundService
{
    private readonly TimeSpan _pollingInterval =
        TimeSpan.FromSeconds(configuration.GetValue("Outbox:PollingSeconds", 10));
    private readonly int _batchSize = configuration.GetValue("Outbox:BatchSize", 50);
    private readonly int _maxRetry = configuration.GetValue("Outbox:MaxRetry", 5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while processing outbox messages.");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RoutePlanningDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var messages = await context.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < _maxRetry)
            .OrderBy(m => m.CreatedAt)
            .Take(_batchSize)
            .ToListAsync(stoppingToken);

        foreach (var message in messages)
        {
            try
            {
                var eventType = GetEventType(message.EventType);
                if (eventType != null)
                {
                    var eventObject = JsonSerializer.Deserialize(message.Payload, eventType);
                    if (eventObject != null)
                    {
                        await publishEndpoint.Publish(eventObject, eventType, stoppingToken);
                    }
                }
                else
                {
                    logger.LogWarning("Unknown outbox event type {EventType} — bỏ qua message {MessageId}",
                        message.EventType, message.Id);
                }

                message.ProcessedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish outbox message {MessageId}", message.Id);
                message.RetryCount++;
                message.Error = ex.Message;
            }
        }

        if (messages.Count > 0)
        {
            await context.SaveChangesAsync(stoppingToken);
        }
    }

    private static Type? GetEventType(string typeName) => typeName switch
    {
        nameof(RouteCreatedEvent) => typeof(RouteCreatedEvent),
        nameof(RouteUpdatedEvent) => typeof(RouteUpdatedEvent),
        nameof(RouteDeletedEvent) => typeof(RouteDeletedEvent),
        nameof(RouteStatusChangedEvent) => typeof(RouteStatusChangedEvent),
        nameof(RouteOptimizedEvent) => typeof(RouteOptimizedEvent),
        nameof(RouteApprovalRequestedEvent) => typeof(RouteApprovalRequestedEvent),
        nameof(TenantRuleConfigChangedEvent) => typeof(TenantRuleConfigChangedEvent),
        nameof(AiUsageEvent) => typeof(AiUsageEvent),
        _ => null
    };
}

using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MailService.Infrastructure.Persistence;
using Shared.Events;

namespace MailService.Infrastructure.Messaging;

public class OutboxProcessorBackgroundService(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<OutboxProcessorBackgroundService> logger) : BackgroundService
{
    private readonly TimeSpan _pollingInterval =
        TimeSpan.FromSeconds(configuration.GetValue("Outbox:PollingSeconds", 5));
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
                logger.LogError(ex, "Error occurred while processing mail outbox messages.");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MailServiceDbContext>();
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
                    logger.LogWarning("Unknown outbox event type: {EventType}", message.EventType);
                }

                message.ProcessedAt = DateTimeOffset.UtcNow;
                message.Error = null;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.Error = ex.Message;
                logger.LogError(ex, "Failed to publish outbox message {Id} (Attempt {RetryCount})", message.Id, message.RetryCount);
            }
        }

        if (messages.Count > 0)
        {
            await context.SaveChangesAsync(stoppingToken);
        }
    }

    private static Type? GetEventType(string eventTypeName)
    {
        return eventTypeName switch
        {
            nameof(InboundEmailReceivedEvent) => typeof(InboundEmailReceivedEvent),
            nameof(InboundEmailQuarantinedEvent) => typeof(InboundEmailQuarantinedEvent),
            nameof(OutboundEmailSentEvent) => typeof(OutboundEmailSentEvent),
            nameof(OutboundEmailRejectedEvent) => typeof(OutboundEmailRejectedEvent),
            nameof(SendSystemEmailCommand) => typeof(SendSystemEmailCommand),
            _ => null
        };
    }
}

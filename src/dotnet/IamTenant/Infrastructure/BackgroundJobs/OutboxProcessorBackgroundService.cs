using System.Text.Json;
using IamTenant.Infrastructure.Persistences;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Shared.Events;
using Shared.Cache;

namespace IamTenant.Infrastructure.BackgroundJobs;

public class OutboxProcessorBackgroundService(
    IServiceProvider serviceProvider,
    ILogger<OutboxProcessorBackgroundService> logger) : BackgroundService
{
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("OutboxProcessorBackgroundService is stopping.");
                break;
            }
            catch (ObjectDisposedException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred while processing outbox messages.");
                try
                {
                    await Task.Delay(_pollingInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken stoppingToken)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IamTenantDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var permissionCache = scope.ServiceProvider.GetRequiredService<IPermissionCacheService>();

        var messages = await context.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < 5)
            .OrderBy(m => m.CreatedAt)
            .Take(50)
            .ToListAsync(stoppingToken);

        foreach (var message in messages)
        {
            try
            {
                // Find event type
                Type? eventType = GetEventType(message.EventType);
                if (eventType != null)
                {
                    var eventObject = JsonSerializer.Deserialize(message.Payload, eventType);
                    if (eventObject != null)
                    {
                        if (eventObject is RolePermissionsChangedEvent roleEvent)
                        {
                            var affectedUsers = await context.UserRoles
                                .Where(ur => ur.RoleId == roleEvent.RoleId)
                                .Select(ur => ur.User)
                                .ToListAsync(stoppingToken);

                            foreach (var user in affectedUsers)
                            {
                                if (user != null)
                                {
                                    user.PermissionVersion++;
                                    await permissionCache.InvalidateAsync(user.Id, stoppingToken);
                                }
                            }
                        }
                        else
                        {
                            await publishEndpoint.Publish(eventObject, eventType, stoppingToken);
                        }
                    }
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

        if (messages.Any())
        {
            await context.SaveChangesAsync(stoppingToken);
        }
    }

    private Type? GetEventType(string typeName)
    {
        return typeName switch
        {
            nameof(TenantAdminCreatedEvent) => typeof(TenantAdminCreatedEvent),
            nameof(TenantStaffCreatedEvent) => typeof(TenantStaffCreatedEvent),
            nameof(TenantStaffPasswordResetEvent) => typeof(TenantStaffPasswordResetEvent),
            nameof(RolePermissionsChangedEvent) => typeof(RolePermissionsChangedEvent),
            _ => null
        };
    }
}

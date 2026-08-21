using DocumentOcr.Contracts.Events;
using GpsTracking.Contracts.Events;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notification.Application.Consumers;
using Notification.Application.Services;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Infrastructure.Persistences;
using RegulatoryCompliance.Contracts.Events;
using Shared.Interceptors;
using Shared.Security;

namespace Notification.Tests.Integration;

public sealed class OwnedServiceRabbitMqIntegrationTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5434;Database=aurora_notification;Username=postgres;Password=postgres";

    [Fact]
    public async Task OwnedServiceEventsAreConsumedThroughRabbitMq()
    {
        var tenantId = Guid.CreateVersion7();
        var userId = Guid.CreateVersion7();
        var gpsEvent = new GpsMonitoringAlertRaisedEvent
        {
            TenantId = tenantId,
            AlertId = Guid.CreateVersion7(),
            AlertType = "SignalLoss",
            VehicleId = "TRUCK-RABBIT",
            Message = "No position received.",
            OccurredAt = DateTimeOffset.UtcNow
        };
        var ocrEvent = new DocumentOcrFailedEvent
        {
            TenantId = tenantId,
            JobId = Guid.CreateVersion7(),
            ErrorCode = "OCR_TIMEOUT",
            ErrorMessage = "Provider timed out.",
            OccurredAt = DateTimeOffset.UtcNow
        };
        var complianceEvent = new ComplianceEvaluationCompletedEvent
        {
            TenantId = tenantId,
            EvaluationId = Guid.CreateVersion7(),
            ExternalShipmentId = Guid.CreateVersion7(),
            RiskLevel = "Low",
            EvidenceSufficiency = "Sufficient",
            ComplianceConfidence = 0.93m,
            Summary = "No blocking findings.",
            OccurredAt = DateTimeOffset.UtcNow
        };

        var services = CreateServices();
        await using var provider = services.BuildServiceProvider();
        await SeedPreferencesAsync(provider, tenantId, userId);
        var bus = provider.GetRequiredService<IBusControl>();

        await bus.StartAsync();
        try
        {
            var publisher = provider.GetRequiredService<IPublishEndpoint>();
            await publisher.Publish(gpsEvent);
            await publisher.Publish(ocrEvent);
            await publisher.Publish(complianceEvent);

            await WaitForReceiptsAsync(
                provider,
                [gpsEvent.EventId, ocrEvent.EventId, complianceEvent.EventId],
                TimeSpan.FromSeconds(15));

            await using var scope = provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var notifications = await context.Notifications
                .IgnoreQueryFilters()
                .Where(item => item.TenantId == tenantId)
                .ToListAsync();

            Assert.Equal(3, notifications.Count);
            Assert.Contains(notifications, item =>
                item.SourceEventId == gpsEvent.EventId
                && item.EventType == NotificationEventType.GpsMonitoringAlertRaised);
            Assert.Contains(notifications, item =>
                item.SourceEventId == ocrEvent.EventId
                && item.EventType == NotificationEventType.DocumentOcrFailed
                && item.ShipmentId == null);
            Assert.Contains(notifications, item =>
                item.SourceEventId == complianceEvent.EventId
                && item.EventType == NotificationEventType.ComplianceEvaluationCompleted
                && item.ShipmentId == complianceEvent.ExternalShipmentId);
        }
        finally
        {
            await bus.StopAsync();
            await CleanupAsync(provider, tenantId);
        }
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<CurrentUserService>();
        services.AddScoped<ICurrentUserService>(provider =>
            provider.GetRequiredService<CurrentUserService>());
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddDbContext<NotificationDbContext>(options =>
            options.UseNpgsql(ConnectionString));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IIntegrationEventNotificationProjector, IntegrationEventNotificationProjector>();
        services.AddMassTransit(registration =>
        {
            registration.AddConsumer<GpsNotificationConsumer>();
            registration.AddConsumer<DocumentOcrNotificationConsumer>();
            registration.AddConsumer<ComplianceNotificationConsumer>();
            registration.UsingRabbitMq((context, configuration) =>
            {
                configuration.Host("localhost", "/", host =>
                {
                    host.Username("aurora");
                    host.Password("aurora_dev");
                });
                configuration.UseRawJsonSerializer();
                configuration.ReceiveEndpoint(
                    $"notification-owned-events-proof-{Guid.NewGuid():N}",
                    endpoint =>
                    {
                        endpoint.Durable = false;
                        endpoint.AutoDelete = true;
                        endpoint.ConfigureConsumer<GpsNotificationConsumer>(context);
                        endpoint.ConfigureConsumer<DocumentOcrNotificationConsumer>(context);
                        endpoint.ConfigureConsumer<ComplianceNotificationConsumer>(context);
                    });
            });
        });

        return services;
    }

    private static async Task SeedPreferencesAsync(
        IServiceProvider provider,
        Guid tenantId,
        Guid userId)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        context.NotificationPreferences.AddRange(
            Preference(tenantId, userId, NotificationEventType.GpsMonitoringAlertRaised),
            Preference(tenantId, userId, NotificationEventType.DocumentOcrFailed),
            Preference(tenantId, userId, NotificationEventType.ComplianceEvaluationCompleted));
        await context.SaveChangesAsync();
    }

    private static NotificationPreference Preference(
        Guid tenantId,
        Guid userId,
        NotificationEventType eventType) =>
        NotificationPreference.Create(
            tenantId,
            userId,
            eventType,
            NotificationChannel.InApp,
            true,
            null);

    private static async Task WaitForReceiptsAsync(
        IServiceProvider provider,
        IReadOnlyCollection<Guid> eventIds,
        TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            await using var scope = provider.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var count = await context.ConsumedIntegrationEvents
                .IgnoreQueryFilters()
                .CountAsync(item => eventIds.Contains(item.SourceEventId));
            if (count == eventIds.Count)
                return;

            await Task.Delay(100);
        }

        throw new TimeoutException("Owned-service events were not consumed within the timeout.");
    }

    private static async Task CleanupAsync(IServiceProvider provider, Guid tenantId)
    {
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await context.DeliveryAttempts.IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId)
            .ExecuteDeleteAsync();
        await context.Notifications.IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId)
            .ExecuteDeleteAsync();
        await context.NotificationPreferences.IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId)
            .ExecuteDeleteAsync();
        await context.ConsumedIntegrationEvents.IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId)
            .ExecuteDeleteAsync();
    }
}

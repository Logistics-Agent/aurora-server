using System.Text.Json;
using GpsTracking.Contracts.Events;
using GpsTracking.Domain.Entities;
using GpsTracking.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GpsTracking.Tests.Infrastructure;

public sealed class GpsOutboxPublisherTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 20, 0, 0, TimeSpan.Zero);

    public static TheoryData<string, Type> EventTypes => new()
    {
        { nameof(GpsPositionUpdatedEvent), typeof(GpsPositionUpdatedEvent) },
        { nameof(GpsMonitoringAlertRaisedEvent), typeof(GpsMonitoringAlertRaisedEvent) }
    };

    [Theory]
    [MemberData(nameof(EventTypes))]
    public void RegistryResolvesOnlySupportedEventTypes(string eventType, Type expectedType)
    {
        Assert.True(GpsIntegrationEventTypeRegistry.TryResolve(eventType, out var resolvedType));
        Assert.Equal(expectedType, resolvedType);
        Assert.False(GpsIntegrationEventTypeRegistry.TryResolve("UnknownEvent", out _));
    }

    [Fact]
    public void SerializationPreservesContractIdentityAndTenant()
    {
        var tenantId = Guid.CreateVersion7();
        var integrationEvent = new GpsPositionUpdatedEvent
        {
            TenantId = tenantId,
            PositionId = Guid.CreateVersion7(),
            DeviceId = "device-1",
            VehicleId = "vehicle-1",
            RecordedAt = Now
        };

        var deserialized = Assert.IsType<GpsPositionUpdatedEvent>(
            GpsIntegrationEventTypeRegistry.Deserialize(
                nameof(GpsPositionUpdatedEvent), JsonSerializer.Serialize(integrationEvent)));

        Assert.Equal(integrationEvent.EventId, deserialized.EventId);
        Assert.Equal(tenantId, deserialized.TenantId);
        Assert.Equal(1, deserialized.ContractVersion);
        Assert.Equal(Now, deserialized.RecordedAt);
        Assert.NotEqual(integrationEvent.EventId, new GpsPositionUpdatedEvent().EventId);
    }

    [Fact]
    public async Task ProcessorPublishesAndMarksMessageProcessed()
    {
        var integrationEvent = PositionEvent();
        var message = Message(integrationEvent);
        var store = new RecordingBatchStore(message);
        var publisher = new RecordingPublisher();

        var count = await Processor(store, publisher).ProcessBatchAsync();

        Assert.Equal(1, count);
        var published = Assert.IsType<GpsPositionUpdatedEvent>(Assert.Single(publisher.Messages));
        Assert.Equal(integrationEvent.EventId, published.EventId);
        Assert.NotNull(message.ProcessedAt);
        Assert.Equal(0, message.RetryCount);
        Assert.True(store.Committed);
    }

    [Fact]
    public async Task ProcessorRecordsUnknownTypeAsBoundedFailure()
    {
        var message = OutboxMessage.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "UnknownEvent", "{}", Now);
        var store = new RecordingBatchStore(message);

        await Processor(store, new RecordingPublisher()).ProcessBatchAsync();

        Assert.Null(message.ProcessedAt);
        Assert.Equal(1, message.RetryCount);
        Assert.Contains("Unsupported GPS outbox event type", message.Error);
        Assert.True(store.Committed);
    }

    [Fact]
    public async Task ProcessorDoesNotRetryMessagesAtLimit()
    {
        var message = Message(PositionEvent());
        for (var attempt = 0; attempt < 3; attempt++)
            message.RecordFailure("failure", Now.AddSeconds(attempt));
        var store = new RecordingBatchStore(message);
        var publisher = new RecordingPublisher();

        var count = await Processor(store, publisher, maxRetries: 3).ProcessBatchAsync();

        Assert.Equal(0, count);
        Assert.Empty(publisher.Messages);
    }

    [Fact]
    public async Task ProcessorRecordsPublishFailureForRetry()
    {
        var message = Message(PositionEvent());
        var store = new RecordingBatchStore(message);

        await Processor(store, new FailingPublisher()).ProcessBatchAsync();

        Assert.Equal(1, message.RetryCount);
        Assert.Null(message.ProcessedAt);
        Assert.Contains("RabbitMQ unavailable", message.Error);
    }

    private static GpsPositionUpdatedEvent PositionEvent() => new()
    {
        TenantId = Guid.CreateVersion7(),
        PositionId = Guid.CreateVersion7(),
        DeviceId = "device-1",
        VehicleId = "vehicle-1",
        RecordedAt = Now
    };

    private static OutboxMessage Message(GpsPositionUpdatedEvent integrationEvent) =>
        OutboxMessage.Create(
            integrationEvent.TenantId,
            integrationEvent.EventId,
            nameof(GpsPositionUpdatedEvent),
            JsonSerializer.Serialize(integrationEvent),
            Now);

    private static GpsOutboxProcessor Processor(
        IGpsOutboxBatchStore store,
        IGpsIntegrationEventPublisher publisher,
        int maxRetries = 5) =>
        new(
            store,
            publisher,
            new FixedTimeProvider(Now),
            Options.Create(new GpsOutboxPublisherOptions
            {
                BatchSize = 20,
                MaxRetries = maxRetries
            }),
            NullLogger<GpsOutboxProcessor>.Instance);

    private sealed class RecordingBatchStore(params OutboxMessage[] messages) : IGpsOutboxBatchStore
    {
        public bool Committed { get; private set; }

        public Task<IGpsOutboxBatch> LockPendingBatchAsync(
            int batchSize,
            int maxRetries,
            CancellationToken cancellationToken) =>
            Task.FromResult<IGpsOutboxBatch>(new Batch(
                messages.Where(item => item.ProcessedAt is null && item.RetryCount < maxRetries)
                    .Take(batchSize).ToList(),
                () => Committed = true));

        private sealed class Batch(
            IReadOnlyList<OutboxMessage> messages,
            Action onCommit) : IGpsOutboxBatch
        {
            public IReadOnlyList<OutboxMessage> Messages => messages;

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;

            public Task CommitAsync(CancellationToken cancellationToken)
            {
                onCommit();
                return Task.CompletedTask;
            }
        }
    }

    private sealed class RecordingPublisher : IGpsIntegrationEventPublisher
    {
        public List<object> Messages { get; } = [];

        public Task PublishAsync(object message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingPublisher : IGpsIntegrationEventPublisher
    {
        public Task PublishAsync(object message, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("RabbitMQ unavailable");
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

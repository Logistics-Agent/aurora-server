using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Interceptors;
using Shared.Security;
using Shipment.Contracts.Events;
using ShipmentWorkflow.Domain.Entities;
using ShipmentWorkflow.Infrastructure.BackgroundJobs;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Tests;

[Collection("ShipmentWorkflowDatabase")]
public sealed class ShipmentOutboxPublisherTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5433;Database=aurora_shipment_workflow_tests;Username=postgres;Password=postgres";

    public static TheoryData<string, Type> EventTypes => new()
    {
        { nameof(ShipmentCreatedEvent), typeof(ShipmentCreatedEvent) },
        { nameof(ShipmentSubmittedEvent), typeof(ShipmentSubmittedEvent) },
        { nameof(ShipmentUpdatedEvent), typeof(ShipmentUpdatedEvent) },
        { nameof(ShipmentCancelledEvent), typeof(ShipmentCancelledEvent) },
        { nameof(ShipmentStatusChangedEvent), typeof(ShipmentStatusChangedEvent) },
        { nameof(CargoUpdatedEvent), typeof(CargoUpdatedEvent) },
        { nameof(DocumentAttachedEvent), typeof(DocumentAttachedEvent) },
        { nameof(RouteAssignedEvent), typeof(RouteAssignedEvent) },
        { nameof(ShipmentPickedUpEvent), typeof(ShipmentPickedUpEvent) },
        { nameof(ShipmentDeliveredEvent), typeof(ShipmentDeliveredEvent) },
        { nameof(ShipmentCompletedEvent), typeof(ShipmentCompletedEvent) }
    };

    [Theory]
    [MemberData(nameof(EventTypes))]
    public void Registry_resolves_all_supported_event_types(string eventType, Type expectedType)
    {
        Assert.True(ShipmentIntegrationEventTypeRegistry.TryResolve(eventType, out var resolvedType));
        Assert.Equal(expectedType, resolvedType);
    }

    [Fact]
    public async Task Processor_publishes_pending_message_and_marks_it_processed()
    {
        await using var dbContext = await CreateDbContextAsync();
        var integrationEvent = new ShipmentCreatedEvent
        {
            ShipmentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ShipmentNumber = "SHP-OUTBOX-1",
            CreatedAt = DateTimeOffset.UtcNow
        };
        var message = AddOutboxMessage(dbContext, integrationEvent);
        await dbContext.SaveChangesAsync();
        var publisher = new RecordingPublisher();

        var count = await CreateProcessor(dbContext, publisher).ProcessBatchAsync();

        Assert.Equal(1, count);
        Assert.Single(publisher.Messages);
        var published = Assert.IsType<ShipmentCreatedEvent>(publisher.Messages[0]);
        Assert.Equal(integrationEvent.EventId, published.EventId);
        await dbContext.Entry(message).ReloadAsync();
        Assert.NotNull(message.ProcessedAt);
        Assert.Equal(0, message.RetryCount);
        Assert.Null(message.Error);
    }

    [Fact]
    public async Task Processor_records_failure_and_leaves_message_pending_for_retry()
    {
        await using var dbContext = await CreateDbContextAsync();
        var message = AddOutboxMessage(dbContext, new ShipmentCreatedEvent
        {
            ShipmentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ShipmentNumber = "SHP-OUTBOX-2",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var count = await CreateProcessor(
            dbContext,
            new FailingPublisher("RabbitMQ unavailable")).ProcessBatchAsync();

        Assert.Equal(1, count);
        await dbContext.Entry(message).ReloadAsync();
        Assert.Null(message.ProcessedAt);
        Assert.Equal(1, message.RetryCount);
        Assert.Contains("RabbitMQ unavailable", message.Error);
    }

    [Fact]
    public async Task Processor_does_not_select_messages_at_retry_limit()
    {
        await using var dbContext = await CreateDbContextAsync();
        var message = AddOutboxMessage(dbContext, new ShipmentCreatedEvent
        {
            ShipmentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            ShipmentNumber = "SHP-OUTBOX-3",
            CreatedAt = DateTimeOffset.UtcNow
        });
        message.RetryCount = 5;
        await dbContext.SaveChangesAsync();
        var publisher = new RecordingPublisher();

        var count = await CreateProcessor(dbContext, publisher).ProcessBatchAsync();

        Assert.Equal(0, count);
        Assert.Empty(publisher.Messages);
    }

    [Fact]
    public async Task Processor_records_unknown_event_type_as_failure()
    {
        await using var dbContext = await CreateDbContextAsync();
        var message = new OutboxMessage
        {
            EventType = "UnknownShipmentEvent",
            Payload = "{}",
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.OutboxMessages.Add(message);
        await dbContext.SaveChangesAsync();

        await CreateProcessor(dbContext, new RecordingPublisher()).ProcessBatchAsync();

        await dbContext.Entry(message).ReloadAsync();
        Assert.Null(message.ProcessedAt);
        Assert.Equal(1, message.RetryCount);
        Assert.Contains("Unsupported Shipment outbox event type", message.Error);
    }

    [Fact]
    public async Task Processor_records_malformed_payload_as_failure()
    {
        await using var dbContext = await CreateDbContextAsync();
        var message = new OutboxMessage
        {
            EventType = nameof(ShipmentCreatedEvent),
            Payload = "{not-json}",
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.OutboxMessages.Add(message);
        await dbContext.SaveChangesAsync();
        var publisher = new RecordingPublisher();

        await CreateProcessor(dbContext, publisher).ProcessBatchAsync();

        await dbContext.Entry(message).ReloadAsync();
        Assert.Empty(publisher.Messages);
        Assert.Null(message.ProcessedAt);
        Assert.Equal(1, message.RetryCount);
        Assert.NotNull(message.Error);
    }

    private static OutboxMessage AddOutboxMessage<T>(
        ShipmentWorkflowDbContext dbContext,
        T integrationEvent)
    {
        var message = new OutboxMessage
        {
            EventType = typeof(T).Name,
            Payload = JsonSerializer.Serialize(integrationEvent),
            CreatedAt = DateTimeOffset.UtcNow
        };
        dbContext.OutboxMessages.Add(message);
        return message;
    }

    private static ShipmentOutboxProcessor CreateProcessor(
        ShipmentWorkflowDbContext dbContext,
        IShipmentIntegrationEventPublisher publisher) =>
        new(
            dbContext,
            publisher,
            TimeProvider.System,
            Options.Create(new ShipmentOutboxPublisherOptions()),
            NullLogger<ShipmentOutboxProcessor>.Instance);

    private static async Task<ShipmentWorkflowDbContext> CreateDbContextAsync()
    {
        var currentUser = new TestCurrentUserService();
        var options = new DbContextOptionsBuilder<ShipmentWorkflowDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        var dbContext = new ShipmentWorkflowDbContext(
            options,
            currentUser,
            new AuditSaveChangesInterceptor(currentUser));
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
        return dbContext;
    }

    private sealed class RecordingPublisher : IShipmentIntegrationEventPublisher
    {
        public List<object> Messages { get; } = [];

        public Task PublishAsync(object message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingPublisher(string error) : IShipmentIntegrationEventPublisher
    {
        public Task PublishAsync(object message, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(error);
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => null;
        public Guid? TenantId => null;
        public string? Role => null;
        public IReadOnlyList<string> Permissions => [];
    }
}

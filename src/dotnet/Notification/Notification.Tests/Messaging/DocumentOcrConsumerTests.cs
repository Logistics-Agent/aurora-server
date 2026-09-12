using DocumentOcr.Contracts.Events;
using MassTransit;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;
using Notification.Domain.Enums;
using Notification.Infrastructure.Messaging;
using Notification.Infrastructure.Messaging.Consumers;
using Notification.Infrastructure.Persistences;
using Xunit;

namespace Notification.Tests.Messaging;

public sealed class DocumentOcrConsumerTests
{
    [Fact]
    public async Task Completed_shipment_event_uses_actual_consumer_and_creates_notification()
    {
        await using var fixture = await ConsumerFixture.CreateAsync();
        var message = new DocumentOcrCompletedEvent
        {
            TenantId = fixture.TenantId,
            JobId = Guid.CreateVersion7(),
            ExternalShipmentId = fixture.ShipmentId,
            Purpose = DocumentOcrPurpose.ShipmentDocument,
            DetectedDocumentType = "COMMERCIAL_INVOICE",
            Confidence = 0.98m,
            OccurredAt = DateTimeOffset.UtcNow
        };

        await new DocumentOcrCompletedConsumer(fixture.Processor).Consume(ContextFor(message));

        Assert.Equal("DOCUMENT_OCR_COMPLETED", Assert.Single(fixture.Db.Notifications).Type);
        Assert.Single(fixture.PushProvider.Messages);
    }

    [Fact]
    public async Task Failed_shipment_event_uses_actual_consumer_and_creates_notification()
    {
        await using var fixture = await ConsumerFixture.CreateAsync();
        var message = new DocumentOcrFailedEvent
        {
            TenantId = fixture.TenantId,
            JobId = Guid.CreateVersion7(),
            ExternalShipmentId = fixture.ShipmentId,
            Purpose = DocumentOcrPurpose.ShipmentDocument,
            ErrorCode = "OCR_PROVIDER_ERROR",
            ErrorMessage = "Provider unavailable",
            OccurredAt = DateTimeOffset.UtcNow
        };

        await new DocumentOcrFailedConsumer(fixture.Processor).Consume(ContextFor(message));

        Assert.Equal("DOCUMENT_OCR_FAILED", Assert.Single(fixture.Db.Notifications).Type);
        Assert.Single(fixture.PushProvider.Messages);
    }

    [Theory]
    [InlineData(DocumentOcrPurpose.RegulatoryCorpus)]
    [InlineData(DocumentOcrPurpose.KnowledgeCorpus)]
    [InlineData(DocumentOcrPurpose.GeneralDocument)]
    public async Task Completed_non_shipment_purpose_is_ignored_by_actual_consumer(DocumentOcrPurpose purpose)
    {
        await using var fixture = await ConsumerFixture.CreateAsync();
        var message = new DocumentOcrCompletedEvent
        {
            TenantId = fixture.TenantId,
            JobId = Guid.CreateVersion7(),
            ExternalShipmentId = fixture.ShipmentId,
            Purpose = purpose,
            OccurredAt = DateTimeOffset.UtcNow
        };

        await new DocumentOcrCompletedConsumer(fixture.Processor).Consume(ContextFor(message));

        Assert.Empty(fixture.Db.Notifications);
        Assert.Empty(fixture.PushProvider.Messages);
    }

    [Theory]
    [InlineData(DocumentOcrPurpose.RegulatoryCorpus)]
    [InlineData(DocumentOcrPurpose.KnowledgeCorpus)]
    [InlineData(DocumentOcrPurpose.GeneralDocument)]
    public async Task Failed_non_shipment_purpose_is_ignored_by_actual_consumer(DocumentOcrPurpose purpose)
    {
        await using var fixture = await ConsumerFixture.CreateAsync();
        var message = new DocumentOcrFailedEvent
        {
            TenantId = fixture.TenantId,
            JobId = Guid.CreateVersion7(),
            ExternalShipmentId = fixture.ShipmentId,
            Purpose = purpose,
            OccurredAt = DateTimeOffset.UtcNow
        };

        await new DocumentOcrFailedConsumer(fixture.Processor).Consume(ContextFor(message));

        Assert.Empty(fixture.Db.Notifications);
        Assert.Empty(fixture.PushProvider.Messages);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Unsupported_version_throws_before_actual_consumer_can_notify(bool completed)
    {
        await using var fixture = await ConsumerFixture.CreateAsync();

        if (completed)
        {
            var message = new DocumentOcrCompletedEvent
            {
                ContractVersion = 1,
                TenantId = fixture.TenantId,
                JobId = Guid.CreateVersion7(),
                ExternalShipmentId = fixture.ShipmentId,
                OccurredAt = DateTimeOffset.UtcNow
            };
            await Assert.ThrowsAsync<NotSupportedException>(() =>
                new DocumentOcrCompletedConsumer(fixture.Processor).Consume(ContextFor(message)));
        }
        else
        {
            var message = new DocumentOcrFailedEvent
            {
                ContractVersion = 1,
                TenantId = fixture.TenantId,
                JobId = Guid.CreateVersion7(),
                ExternalShipmentId = fixture.ShipmentId,
                OccurredAt = DateTimeOffset.UtcNow
            };
            await Assert.ThrowsAsync<NotSupportedException>(() =>
                new DocumentOcrFailedConsumer(fixture.Processor).Consume(ContextFor(message)));
        }

        Assert.Empty(fixture.Db.Notifications);
        Assert.Empty(fixture.PushProvider.Messages);
    }

    private static ConsumeContext<T> ContextFor<T>(T message) where T : class
    {
        var context = new Moq.Mock<ConsumeContext<T>>();
        context.SetupGet(value => value.Message).Returns(message);
        context.SetupGet(value => value.CancellationToken).Returns(CancellationToken.None);
        return context.Object;
    }

    private sealed class ConsumerFixture : IAsyncDisposable
    {
        private ConsumerFixture(
            SqliteConnection connection,
            NotificationDbContext db,
            FixedRecipientResolver recipientResolver,
            FakePushProvider pushProvider)
        {
            Connection = connection;
            Db = db;
            TenantId = Guid.CreateVersion7();
            ShipmentId = Guid.CreateVersion7();
            PushProvider = pushProvider;
            Processor = new NotificationEventProcessor(
                db,
                recipientResolver,
                pushProvider,
                NullLogger<NotificationEventProcessor>.Instance);
        }

        public SqliteConnection Connection { get; }
        public NotificationDbContext Db { get; }
        public Guid TenantId { get; }
        public Guid ShipmentId { get; }
        public FakePushProvider PushProvider { get; }
        public NotificationEventProcessor Processor { get; }

        public static async Task<ConsumerFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<NotificationDbContext>().UseSqlite(connection).Options;
            var db = new NotificationDbContext(options);
            await db.Database.EnsureCreatedAsync();
            var resolver = new FixedRecipientResolver(Guid.CreateVersion7());
            var fixture = new ConsumerFixture(connection, db, resolver, new FakePushProvider());
            db.Devices.Add(NotificationDevice.Register(
                fixture.TenantId,
                resolver.UserId,
                "fake-token",
                DevicePlatform.Web));
            await db.SaveChangesAsync();
            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private sealed class FixedRecipientResolver : IRecipientResolver
    {
        public FixedRecipientResolver(Guid userId) => UserId = userId;

        public Guid UserId { get; }

        public Task<IReadOnlyCollection<Guid>> ResolveAsync(Guid tenantId, Guid? shipmentId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Guid>>([UserId]);
    }

    private sealed class FakePushProvider : IFcmPushProvider
    {
        public List<FcmMessage> Messages { get; } = [];

        public Task<FcmSendResult> SendAsync(NotificationDevice device, FcmMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.FromResult(new FcmSendResult(FcmSendStatus.Sent, "fake-message"));
        }
    }
}

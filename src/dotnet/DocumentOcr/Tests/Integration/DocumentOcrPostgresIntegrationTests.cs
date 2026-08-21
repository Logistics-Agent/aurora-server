using System.Collections.Concurrent;
using System.Text.Json;
using DocumentOcr.Application.Jobs;
using DocumentOcr.Application.Providers;
using DocumentOcr.Contracts.Events;
using DocumentOcr.Domain.Entities;
using DocumentOcr.Domain.Enums;
using DocumentOcr.Infrastructure.BackgroundJobs;
using DocumentOcr.Infrastructure.Persistences;
using DocumentOcr.Infrastructure.Providers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Exceptions;
using Shared.Security;

namespace DocumentOcr.Tests.Integration;

[Collection(DocumentOcrPostgresCollection.Name)]
public sealed class DocumentOcrPostgresIntegrationTests(DocumentOcrPostgresFixture database)
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MigrationBackedPipelinePersistsJsonRelationshipAndTenantIsolation()
    {
        await database.ResetAsync();
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = database.CreateContext(currentUser);
        var service = CreateService(context, currentUser);

        var submitted = await service.SubmitAsync(Input("postgres-pipeline"));
        var completed = await service.ProcessAsync(tenantId, submitted.Id);

        Assert.Equal(DocumentOcrJobStatus.Completed, completed!.Status);
        context.ChangeTracker.Clear();
        var persisted = await context.Jobs.Include(job => job.Attempts).SingleAsync();
        Assert.Equal(JsonValueKind.Object, JsonDocument.Parse(persisted.NormalizedJson!).RootElement.ValueKind);
        Assert.Single(persisted.Attempts);
        Assert.Single(await context.OutboxMessages.ToListAsync());
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());

        var otherUser = CurrentUser(Guid.CreateVersion7());
        await using var otherContext = database.CreateContext(otherUser);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateService(otherContext, otherUser).GetAsync(submitted.Id));

        await using var missingTenantContext = database.CreateContext(new CurrentUserService());
        Assert.Empty(await missingTenantContext.Jobs.ToListAsync());
        Assert.Empty(await missingTenantContext.ProviderAttempts.ToListAsync());
        Assert.Empty(await missingTenantContext.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task DeletingJobCascadesProviderAttemptsButPreservesIntegrationOutbox()
    {
        await database.ResetAsync();
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = database.CreateContext(currentUser);
        var service = CreateService(context, currentUser);
        var submitted = await service.SubmitAsync(Input("cascade-check"));
        await service.ProcessAsync(tenantId, submitted.Id);
        context.Jobs.Remove(submitted);

        await context.SaveChangesAsync();

        Assert.Empty(await context.ProviderAttempts.ToListAsync());
        Assert.Single(await context.OutboxMessages.ToListAsync());
    }

    [Fact]
    public async Task ConcurrentSubmissionUsesDatabaseIdempotencyConstraint()
    {
        await database.ResetAsync();
        var tenantId = Guid.CreateVersion7();
        var firstUser = CurrentUser(tenantId);
        var secondUser = CurrentUser(tenantId);
        await using var firstContext = database.CreateContext(firstUser);
        await using var secondContext = database.CreateContext(secondUser);

        var results = await Task.WhenAll(
            CreateService(firstContext, firstUser).SubmitAsync(Input("concurrent-submit")),
            CreateService(secondContext, secondUser).SubmitAsync(Input("concurrent-submit")));

        Assert.Equal(results[0].Id, results[1].Id);
        await using var verification = database.CreateContext(CurrentUser(tenantId));
        Assert.Equal(1, await verification.Jobs.CountAsync());
    }

    [Fact]
    public async Task ConcurrentPostgresClaimersClaimJobOnlyOnce()
    {
        await database.ResetAsync();
        var tenantId = Guid.CreateVersion7();
        await using (var seed = database.CreateContext(CurrentUser(tenantId)))
        {
            seed.Jobs.Add(CreateJob(tenantId, "claim-once"));
            await seed.SaveChangesAsync();
        }

        await using var firstContext = database.CreateContext(new CurrentUserService());
        await using var secondContext = database.CreateContext(new CurrentUserService());
        var options = new DocumentOcrWorkerOptions { BatchSize = 1 };
        var firstStore = new DocumentOcrJobBatchStore(
            firstContext, new DeterministicOcrProvider(), new FixedTimeProvider(Now), options);
        var secondStore = new DocumentOcrJobBatchStore(
            secondContext, new DeterministicOcrProvider(), new FixedTimeProvider(Now), options);

        var claimed = await Task.WhenAll(
            firstStore.ClaimPendingAsync(),
            secondStore.ClaimPendingAsync());

        Assert.Equal(1, claimed.Sum(batch => batch.Count));
        await using var verification = database.CreateContext(new CurrentUserService());
        var job = await verification.Jobs.IgnoreQueryFilters().SingleAsync();
        Assert.Equal(DocumentOcrJobStatus.Processing, job.Status);
        Assert.Equal(1, await verification.ProviderAttempts.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task ConcurrentOutboxProcessorsLockDistinctMessages()
    {
        await database.ResetAsync();
        var tenantId = Guid.CreateVersion7();
        await using (var seed = database.CreateContext(new CurrentUserService()))
        {
            seed.OutboxMessages.AddRange(
                CompletionMessage(tenantId, Guid.CreateVersion7()),
                CompletionMessage(tenantId, Guid.CreateVersion7()));
            await seed.SaveChangesAsync();
        }

        var published = new ConcurrentBag<Guid>();
        await using var firstContext = database.CreateContext(new CurrentUserService());
        await using var secondContext = database.CreateContext(new CurrentUserService());
        var options = new DocumentOcrOutboxPublisherOptions { BatchSize = 1 };
        var firstProcessor = Processor(firstContext, new CapturingPublisher(published), options);
        var secondProcessor = Processor(secondContext, new CapturingPublisher(published), options);

        await Task.WhenAll(
            firstProcessor.ProcessBatchAsync(),
            secondProcessor.ProcessBatchAsync());

        Assert.Equal(2, published.Distinct().Count());
        await using var verification = database.CreateContext(new CurrentUserService());
        Assert.Equal(2, await verification.OutboxMessages
            .IgnoreQueryFilters()
            .CountAsync(message => message.ProcessedAt != null));
    }

    [Fact]
    public async Task CompletionAndFailureEventsPublishThroughRabbitMqAndMarkOutboxProcessed()
    {
        await database.ResetAsync();
        var completionReceived = new TaskCompletionSource<DocumentOcrCompletedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var failureReceived = new TaskCompletionSource<DocumentOcrFailedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var queueName = $"ocr-runtime-proof-{Guid.NewGuid():N}";
        var bus = Bus.Factory.CreateUsingRabbitMq(configuration =>
        {
            configuration.Host("localhost", "/", host =>
            {
                host.Username("aurora");
                host.Password("aurora_dev");
            });
            configuration.UseRawJsonSerializer();
            configuration.ReceiveEndpoint(queueName, endpoint =>
            {
                endpoint.Durable = false;
                endpoint.AutoDelete = true;
                endpoint.Handler<DocumentOcrCompletedEvent>(context =>
                {
                    completionReceived.TrySetResult(context.Message);
                    return Task.CompletedTask;
                });
                endpoint.Handler<DocumentOcrFailedEvent>(context =>
                {
                    failureReceived.TrySetResult(context.Message);
                    return Task.CompletedTask;
                });
            });
        });

        await bus.StartAsync();
        try
        {
            var tenantId = Guid.CreateVersion7();
            var completion = CompletedEvent(tenantId, Guid.CreateVersion7());
            var failure = FailedEvent(tenantId, Guid.CreateVersion7());
            var completionMessage = OutboxMessage.Create(
                tenantId,
                completion.EventId,
                nameof(DocumentOcrCompletedEvent),
                JsonSerializer.Serialize(completion),
                Now);
            var failureMessage = OutboxMessage.Create(
                tenantId,
                failure.EventId,
                nameof(DocumentOcrFailedEvent),
                JsonSerializer.Serialize(failure),
                Now);
            await using var context = database.CreateContext(new CurrentUserService());
            context.OutboxMessages.AddRange(completionMessage, failureMessage);
            await context.SaveChangesAsync();
            var processor = Processor(
                context,
                new DocumentOcrIntegrationEventPublisher(bus),
                new DocumentOcrOutboxPublisherOptions());

            Assert.Equal(2, await processor.ProcessBatchAsync());
            var deliveredCompletion = await completionReceived.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var deliveredFailure = await failureReceived.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await context.Entry(completionMessage).ReloadAsync();
            await context.Entry(failureMessage).ReloadAsync();

            Assert.Equal(completion.EventId, deliveredCompletion.EventId);
            Assert.Equal(tenantId, deliveredCompletion.TenantId);
            Assert.Equal(failure.EventId, deliveredFailure.EventId);
            Assert.Equal(tenantId, deliveredFailure.TenantId);
            Assert.NotNull(completionMessage.ProcessedAt);
            Assert.NotNull(failureMessage.ProcessedAt);
        }
        finally
        {
            await bus.StopAsync();
        }
    }

    private static DocumentOcrJobService CreateService(
        DocumentOcrDbContext context,
        CurrentUserService currentUser)
    {
        var options = new DocumentProcessingOptions();
        var policy = new DocumentInputPolicy(options);
        return new DocumentOcrJobService(
            context,
            currentUser,
            new FixedTimeProvider(Now),
            options,
            new DocumentOcrWorkerOptions(),
            policy,
            new DeterministicDocumentContentReader(policy),
            new DeterministicOcrProvider());
    }

    private static DocumentOcrOutboxProcessor Processor(
        DocumentOcrDbContext context,
        IDocumentOcrIntegrationEventPublisher publisher,
        DocumentOcrOutboxPublisherOptions options) => new(
            new DocumentOcrOutboxBatchStore(context),
            publisher,
            new FixedTimeProvider(Now),
            options,
            NullLogger<DocumentOcrOutboxProcessor>.Instance);

    private static SubmitDocumentJobInput Input(string key) => new(
        key,
        "objects/tenant/invoice.pdf",
        "invoice.pdf",
        "application/pdf",
        1_024,
        OcrDocumentType.CommercialInvoice,
        Guid.Parse("019bf000-0000-7000-8000-000000000001"),
        Guid.Parse("019bf000-0000-7000-8000-000000000002"));

    private static DocumentOcrJob CreateJob(Guid tenantId, string key) => DocumentOcrJob.Create(
        tenantId,
        key,
        "objects/tenant/invoice.pdf",
        "invoice.pdf",
        "application/pdf",
        1_024,
        OcrDocumentType.CommercialInvoice,
        Guid.CreateVersion7(),
        null,
        Now);

    private static OutboxMessage CompletionMessage(Guid tenantId, Guid jobId)
    {
        var integrationEvent = CompletedEvent(tenantId, jobId);
        return OutboxMessage.Create(
            tenantId,
            integrationEvent.EventId,
            nameof(DocumentOcrCompletedEvent),
            JsonSerializer.Serialize(integrationEvent),
            Now);
    }

    private static DocumentOcrCompletedEvent CompletedEvent(Guid tenantId, Guid jobId) => new()
    {
        TenantId = tenantId,
        JobId = jobId,
        ExternalDocumentId = Guid.CreateVersion7(),
        DetectedDocumentType = OcrDocumentType.CommercialInvoice.ToString(),
        NormalizedJson = "{}",
        Confidence = 0.99m,
        NeedsReview = false,
        OccurredAt = Now
    };

    private static DocumentOcrFailedEvent FailedEvent(Guid tenantId, Guid jobId) => new()
    {
        TenantId = tenantId,
        JobId = jobId,
        ExternalDocumentId = Guid.CreateVersion7(),
        ErrorCode = "provider_failure",
        ErrorMessage = "Provider failed permanently.",
        OccurredAt = Now
    };

    private static CurrentUserService CurrentUser(Guid tenantId)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, [], []);
        return currentUser;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class CapturingPublisher(ConcurrentBag<Guid> eventIds)
        : IDocumentOcrIntegrationEventPublisher
    {
        public Task PublishAsync(object message, CancellationToken cancellationToken)
        {
            eventIds.Add(message switch
            {
                DocumentOcrCompletedEvent completed => completed.EventId,
                DocumentOcrFailedEvent failed => failed.EventId,
                _ => throw new InvalidOperationException("Unexpected event type.")
            });
            return Task.CompletedTask;
        }
    }
}

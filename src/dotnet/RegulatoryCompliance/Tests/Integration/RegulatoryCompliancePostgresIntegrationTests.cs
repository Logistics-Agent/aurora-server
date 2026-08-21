using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RegulatoryCompliance.Application.Embeddings;
using RegulatoryCompliance.Application.Evaluations;
using RegulatoryCompliance.Application.Ingestion;
using RegulatoryCompliance.Application.Retrieval;
using RegulatoryCompliance.Contracts.Events;
using RegulatoryCompliance.Domain.Entities;
using RegulatoryCompliance.Domain.Enums;
using RegulatoryCompliance.Infrastructure.BackgroundJobs;
using RegulatoryCompliance.Infrastructure.Persistences;
using Shared.Security;

namespace RegulatoryCompliance.Tests.Integration;

[Collection(RegulatoryCompliancePostgresCollection.Name)]
public sealed class RegulatoryCompliancePostgresIntegrationTests(
    RegulatoryCompliancePostgresFixture database)
{
    private static readonly DateTimeOffset Now = new(2026, 7, 23, 14, 0, 0, TimeSpan.Zero);
    private static readonly RegulationType[] EvaluationTypes =
    [
        RegulationType.ImportRestriction,
        RegulationType.ExportRestriction,
        RegulationType.RequiredDocument,
        RegulationType.TransportMode,
        RegulationType.Customs
    ];

    [Fact]
    public async Task MigrationBackedPipelinePersistsVectorsJsonCitationsAndTenantIsolation()
    {
        await database.ResetAsync();
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(
            tenantId, RegulatoryIngestionService.TenantIngestionPermission);
        await using var context = database.CreateContext(currentUser);
        var ingestion = Ingestion(context, currentUser);

        foreach (var type in EvaluationTypes)
            await ingestion.IngestAsync(SourceInput(type, $"pipeline-{type}"));

        var provider = new DeterministicEmbeddingProvider();
        var vectorStore = new EfRegulationVectorStore(context);
        var processor = new EmbeddingBatchProcessor(
            context, provider, vectorStore, new FixedTimeProvider(Now));
        Assert.Equal(EvaluationTypes.Length * 2, await processor.ProcessPendingAsync());

        var retrieval = new RegulationRetrievalService(
            context, provider, vectorStore, currentUser, new FixedTimeProvider(Now));
        var evaluation = await new ComplianceEvaluationService(
            context, retrieval, currentUser, new FixedTimeProvider(Now))
            .EvaluateAsync(EvaluationInput("pipeline-evaluation"));

        Assert.Equal(ComplianceEvaluationStatus.Completed, evaluation.Status);
        Assert.Equal(ComplianceRiskLevel.Low, evaluation.RiskLevel);
        Assert.NotEmpty(evaluation.Findings);
        Assert.All(evaluation.Findings, finding => Assert.NotEmpty(finding.Citations));
        Assert.Equal(10, evaluation.RetrievalTraces.Count);
        Assert.Single(await context.OutboxMessages.ToListAsync());
        Assert.All(await context.RegulatoryChunks.ToListAsync(), chunk =>
        {
            Assert.Equal(ChunkEmbeddingStatus.Completed, chunk.EmbeddingStatus);
            Assert.Equal(64, chunk.Embedding!.Length);
        });
        var persisted = await context.ComplianceEvaluations.AsNoTracking().SingleAsync();
        Assert.Equal(JsonValueKind.Object,
            JsonDocument.Parse(persisted.RequestSnapshotJson).RootElement.ValueKind);
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());

        await using var otherTenant = database.CreateContext(CurrentUser(Guid.CreateVersion7()));
        Assert.Empty(await otherTenant.RegulatoryDocuments.ToListAsync());
        Assert.Empty(await otherTenant.ComplianceEvaluations.ToListAsync());
        Assert.Empty(await otherTenant.ComplianceCitations.ToListAsync());

        await using var missingTenant = database.CreateContext(new CurrentUserService());
        Assert.Empty(await missingTenant.RegulatoryDocuments.ToListAsync());
        Assert.Empty(await missingTenant.ComplianceEvaluations.ToListAsync());
    }

    [Fact]
    public async Task ConcurrentIngestionAndEvaluationReplaysAreIdempotent()
    {
        await database.ResetAsync();
        var tenantId = Guid.CreateVersion7();
        var firstUser = CurrentUser(
            tenantId, RegulatoryIngestionService.TenantIngestionPermission);
        var secondUser = CurrentUser(
            tenantId, RegulatoryIngestionService.TenantIngestionPermission);
        await using var firstContext = database.CreateContext(firstUser);
        await using var secondContext = database.CreateContext(secondUser);
        var source = SourceInput(RegulationType.Customs, "concurrent-source");

        var ingestionResults = await Task.WhenAll(
            Ingestion(firstContext, firstUser).IngestAsync(source),
            Ingestion(secondContext, secondUser).IngestAsync(source));

        Assert.Equal(ingestionResults[0].DocumentVersionId, ingestionResults[1].DocumentVersionId);

        firstContext.ChangeTracker.Clear();
        secondContext.ChangeTracker.Clear();
        var input = EvaluationInput("concurrent-evaluation");
        var evaluationResults = await Task.WhenAll(
            new ComplianceEvaluationService(
                    firstContext,
                    new NoEvidenceRetrievalService(firstContext, tenantId),
                    firstUser,
                    new FixedTimeProvider(Now))
                .EvaluateAsync(input),
            new ComplianceEvaluationService(
                    secondContext,
                    new NoEvidenceRetrievalService(secondContext, tenantId),
                    secondUser,
                    new FixedTimeProvider(Now))
                .EvaluateAsync(input));

        Assert.Equal(evaluationResults[0].Id, evaluationResults[1].Id);
        await using var verification = database.CreateContext(CurrentUser(tenantId));
        Assert.Equal(1, await verification.RegulatoryDocuments.CountAsync());
        Assert.Equal(1, await verification.RegulatoryDocumentVersions.CountAsync());
        Assert.Equal(1, await verification.ComplianceEvaluations.CountAsync());
        Assert.Equal(1, await verification.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task ConcurrentOutboxProcessorsLockDistinctMessages()
    {
        await database.ResetAsync();
        var tenantId = Guid.CreateVersion7();
        await using (var seed = database.CreateContext(new CurrentUserService()))
        {
            seed.OutboxMessages.AddRange(
                CompletedMessage(tenantId, Guid.CreateVersion7()),
                CompletedMessage(tenantId, Guid.CreateVersion7()));
            await seed.SaveChangesAsync();
        }

        var published = new ConcurrentBag<Guid>();
        await using var firstContext = database.CreateContext(new CurrentUserService());
        await using var secondContext = database.CreateContext(new CurrentUserService());
        var options = new RegulatoryComplianceRuntimeOptions { OutboxBatchSize = 1 };

        await Task.WhenAll(
            Processor(firstContext, new CapturingPublisher(published), options).ProcessBatchAsync(),
            Processor(secondContext, new CapturingPublisher(published), options).ProcessBatchAsync());

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
        var completionReceived = new TaskCompletionSource<ComplianceEvaluationCompletedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var failureReceived = new TaskCompletionSource<ComplianceEvaluationFailedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var bus = Bus.Factory.CreateUsingRabbitMq(configuration =>
        {
            configuration.Host("localhost", "/", host =>
            {
                host.Username("aurora");
                host.Password("aurora_dev");
            });
            configuration.UseRawJsonSerializer();
            configuration.ReceiveEndpoint($"compliance-runtime-proof-{Guid.NewGuid():N}", endpoint =>
            {
                endpoint.Durable = false;
                endpoint.AutoDelete = true;
                endpoint.Handler<ComplianceEvaluationCompletedEvent>(context =>
                {
                    completionReceived.TrySetResult(context.Message);
                    return Task.CompletedTask;
                });
                endpoint.Handler<ComplianceEvaluationFailedEvent>(context =>
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
            var completed = CompletedEvent(tenantId, Guid.CreateVersion7());
            var failed = FailedEvent(tenantId, Guid.CreateVersion7());
            var completedMessage = OutboxMessage.Create(
                tenantId, completed.EventId, typeof(ComplianceEvaluationCompletedEvent).FullName!,
                JsonSerializer.Serialize(completed), Now);
            var failedMessage = OutboxMessage.Create(
                tenantId, failed.EventId, typeof(ComplianceEvaluationFailedEvent).FullName!,
                JsonSerializer.Serialize(failed), Now);
            await using var context = database.CreateContext(new CurrentUserService());
            context.OutboxMessages.AddRange(completedMessage, failedMessage);
            await context.SaveChangesAsync();

            Assert.Equal(2, await Processor(
                context,
                new ComplianceIntegrationEventPublisher(bus),
                new RegulatoryComplianceRuntimeOptions()).ProcessBatchAsync());
            var deliveredCompleted = await completionReceived.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var deliveredFailed = await failureReceived.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await context.Entry(completedMessage).ReloadAsync();
            await context.Entry(failedMessage).ReloadAsync();

            Assert.Equal(completed.EventId, deliveredCompleted.EventId);
            Assert.Equal(tenantId, deliveredCompleted.TenantId);
            Assert.Equal(failed.EventId, deliveredFailed.EventId);
            Assert.Equal(tenantId, deliveredFailed.TenantId);
            Assert.NotNull(completedMessage.ProcessedAt);
            Assert.NotNull(failedMessage.ProcessedAt);
        }
        finally
        {
            await bus.StopAsync();
        }
    }

    private static RegulatoryIngestionService Ingestion(
        RegulatoryComplianceDbContext context,
        ICurrentUserService currentUser) =>
        new(context, new DeterministicRegulatoryChunker(), currentUser, new FixedTimeProvider(Now));

    private static ComplianceOutboxProcessor Processor(
        RegulatoryComplianceDbContext context,
        IComplianceIntegrationEventPublisher publisher,
        RegulatoryComplianceRuntimeOptions options) =>
        new(
            new ComplianceOutboxBatchStore(context),
            publisher,
            new FixedTimeProvider(Now),
            options,
            NullLogger<ComplianceOutboxProcessor>.Instance);

    private static RegulatoryIngestionInput SourceInput(RegulationType type, string key)
    {
        var content = $"# {type}\n{type} requirements for Sea shipment from VN to SG.";
        var bytes = Encoding.UTF8.GetBytes(content);
        return new RegulatoryIngestionInput(
            key,
            "Global Trade Authority",
            $"{type} rule",
            $"https://regulations.example/{key}",
            "GLOBAL",
            type,
            "en",
            "2026.1",
            Now.AddDays(-30),
            Now.AddDays(-1),
            null,
            $"regulatory/global/{key}.md",
            $"{key}.md",
            "text/markdown",
            bytes.Length,
            Sha256(bytes),
            bytes,
            SourceVisibility.Tenant);
    }

    private static ComplianceEvaluationInput EvaluationInput(string key) =>
        new(
            key,
            Guid.Parse("01900000-0000-7000-8000-000000000001"),
            [new CargoEvaluationSnapshot(
                "Machinery", "847989", 1, "unit", 500m, 2m, false, null, "crate")],
            "VN",
            "SG",
            ["VN"],
            "Sea",
            [
                new OcrEvaluationSnapshot(
                    Guid.CreateVersion7(), "CommercialInvoice", "{\"fields\":{}}", 0.95m, false),
                new OcrEvaluationSnapshot(
                    Guid.CreateVersion7(), "PackingList", "{\"fields\":{}}", 0.95m, false)
            ],
            Now);

    private static OutboxMessage CompletedMessage(Guid tenantId, Guid evaluationId)
    {
        var integrationEvent = CompletedEvent(tenantId, evaluationId);
        return OutboxMessage.Create(
            tenantId,
            integrationEvent.EventId,
            typeof(ComplianceEvaluationCompletedEvent).FullName!,
            JsonSerializer.Serialize(integrationEvent),
            Now);
    }

    private static ComplianceEvaluationCompletedEvent CompletedEvent(
        Guid tenantId,
        Guid evaluationId) => new()
    {
        TenantId = tenantId,
        EvaluationId = evaluationId,
        ExternalShipmentId = Guid.CreateVersion7(),
        RiskLevel = ComplianceRiskLevel.Low.ToString(),
        EvidenceSufficiency = Domain.Enums.EvidenceSufficiency.Sufficient.ToString(),
        ComplianceConfidence = 0.9m,
        Summary = "Evaluation completed.",
        OccurredAt = Now
    };

    private static ComplianceEvaluationFailedEvent FailedEvent(
        Guid tenantId,
        Guid evaluationId) => new()
    {
        TenantId = tenantId,
        EvaluationId = evaluationId,
        ExternalShipmentId = Guid.CreateVersion7(),
        ErrorCode = "EVALUATION_FAILED",
        ErrorMessage = "Evaluation failed.",
        Summary = "Evaluation requires review.",
        OccurredAt = Now
    };

    private static CurrentUserService CurrentUser(Guid tenantId, params string[] permissions)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, [], [.. permissions]);
        return currentUser;
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class NoEvidenceRetrievalService(
        RegulatoryComplianceDbContext context,
        Guid tenantId) : IRegulationRetrievalService
    {
        public Task<RegulationQueryResult> QueryAsync(
            RegulationQueryInput input,
            CancellationToken cancellationToken = default)
        {
            var trace = RetrievalTrace.Create(
                tenantId,
                null,
                Sha256(Encoding.UTF8.GetBytes(input.Query)),
                input.JurisdictionCode,
                input.EffectiveAt,
                input.LanguageCode,
                JsonSerializer.Serialize(input.RegulationTypes),
                "deterministic-local:1",
                input.TopK,
                input.MinimumRelevanceScore,
                "[]",
                "[]",
                Domain.Enums.EvidenceSufficiency.Insufficient,
                Now);
            context.RetrievalTraces.Add(trace);
            return Task.FromResult(new RegulationQueryResult(
                trace.Id,
                Domain.Enums.EvidenceSufficiency.Insufficient,
                [],
                "Insufficient evidence."));
        }
    }

    private sealed class CapturingPublisher(ConcurrentBag<Guid> eventIds)
        : IComplianceIntegrationEventPublisher
    {
        public Task PublishAsync(object message, CancellationToken cancellationToken)
        {
            eventIds.Add(message switch
            {
                ComplianceEvaluationCompletedEvent completed => completed.EventId,
                ComplianceEvaluationFailedEvent failed => failed.EventId,
                _ => throw new InvalidOperationException("Unexpected event type.")
            });
            return Task.CompletedTask;
        }
    }
}

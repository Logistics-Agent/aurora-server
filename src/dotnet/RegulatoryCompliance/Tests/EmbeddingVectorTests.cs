using Microsoft.EntityFrameworkCore;
using RegulatoryCompliance.Application.Embeddings;
using RegulatoryCompliance.Domain.Entities;
using RegulatoryCompliance.Domain.Enums;
using RegulatoryCompliance.Infrastructure.Persistences;
using Shared.Interceptors;
using Shared.Security;

namespace RegulatoryCompliance.Tests;

public sealed class EmbeddingVectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DeterministicProviderRanksSimilarTextAndReembeddingIsIdempotent()
    {
        var tenantId = Guid.CreateVersion7();
        await using var context = CreateContext(CurrentUser(tenantId));
        var dangerous = await SeedChunk(context, tenantId, SourceVisibility.Tenant,
            "dangerous goods declaration required", "dangerous");
        await SeedChunk(context, tenantId, SourceVisibility.Tenant,
            "fresh fruit phytosanitary certificate", "fruit");
        var provider = new DeterministicEmbeddingProvider();
        var store = new EfRegulationVectorStore(context);
        var processor = new EmbeddingBatchProcessor(
            context, provider, store, new FixedTimeProvider(Now));

        Assert.Equal(2, await processor.ProcessPendingAsync());
        Assert.Equal(0, await processor.ProcessPendingAsync());

        var query = (await provider.GenerateAsync(["dangerous goods declaration"]))[0];
        var results = await store.SearchAsync(new VectorSearchRequest(
            query, provider.Model.Name, provider.Model.Version, provider.Model.Dimension,
            [dangerous.Id, .. (await context.RegulatoryChunks.Select(chunk => chunk.Id).ToArrayAsync())],
            10, 0m));

        Assert.Equal(dangerous.Id, results[0].ChunkId);
        Assert.True(results[0].Score > results[1].Score);
    }

    [Fact]
    public async Task SearchReturnsPlatformAndCurrentTenantButNeverAnotherTenant()
    {
        var databaseName = $"compliance-vectors-{Guid.CreateVersion7()}";
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var provider = new DeterministicEmbeddingProvider();

        await using (var writer = CreateContext(CurrentUser(tenantA), databaseName))
        {
            await SeedChunk(writer, null, SourceVisibility.Platform, "customs declaration", "platform");
            await SeedChunk(writer, tenantA, SourceVisibility.Tenant, "customs declaration tenant a", "tenant-a");
            await SeedChunk(writer, tenantB, SourceVisibility.Tenant, "customs declaration tenant b", "tenant-b");
            var store = new EfRegulationVectorStore(writer);
            await new EmbeddingBatchProcessor(
                writer, provider, store, new FixedTimeProvider(Now)).ProcessPendingAsync();
        }

        var query = (await provider.GenerateAsync(["customs declaration"]))[0];
        await using var tenantContext = CreateContext(CurrentUser(tenantA), databaseName);
        var tenantResults = await new EfRegulationVectorStore(tenantContext).SearchAsync(
            new VectorSearchRequest(
                query, provider.Model.Name, provider.Model.Version, provider.Model.Dimension,
                await tenantContext.RegulatoryChunks.Select(chunk => chunk.Id).ToArrayAsync(), 10, 0m));
        Assert.Equal(2, tenantResults.Count);

        await using var missingTenantContext = CreateContext(new CurrentUserService(), databaseName);
        var anonymousResults = await new EfRegulationVectorStore(missingTenantContext).SearchAsync(
            new VectorSearchRequest(
                query, provider.Model.Name, provider.Model.Version, provider.Model.Dimension,
                await missingTenantContext.RegulatoryChunks.Select(chunk => chunk.Id).ToArrayAsync(), 10, 0m));
        Assert.Single(anonymousResults);
    }

    [Fact]
    public async Task VectorStoreRejectsDimensionNonFiniteAndScopeMismatch()
    {
        var tenantId = Guid.CreateVersion7();
        await using var context = CreateContext(CurrentUser(tenantId));
        var chunk = await SeedChunk(
            context, tenantId, SourceVisibility.Tenant, "customs declaration", "validation");
        var store = new EfRegulationVectorStore(context);
        var model = new EmbeddingModelDescriptor("model", "1", 4);

        await Assert.ThrowsAsync<ArgumentException>(() => store.UpsertAsync(
            new EmbeddingModelDescriptor("", "1", 4),
            [new VectorUpsert(
                chunk.Id, tenantId, SourceVisibility.Tenant, chunk.ContentSha256,
                [1f, 0f, 0f, 0f])],
            Now));
        await Assert.ThrowsAsync<ArgumentException>(() => store.UpsertAsync(
            model,
            [new VectorUpsert(chunk.Id, tenantId, SourceVisibility.Tenant, chunk.ContentSha256, [1f, 2f])],
            Now));
        await Assert.ThrowsAsync<ArgumentException>(() => store.UpsertAsync(
            model,
            [new VectorUpsert(
                chunk.Id, tenantId, SourceVisibility.Tenant, chunk.ContentSha256,
                [1f, float.NaN, 0f, 0f])],
            Now));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.UpsertAsync(
            model,
            [new VectorUpsert(
                chunk.Id, Guid.CreateVersion7(), SourceVisibility.Tenant, chunk.ContentSha256,
                [1f, 0f, 0f, 0f])],
            Now));
    }

    [Fact]
    public async Task ProcessorBoundsBatchAndRecordsProviderFailure()
    {
        var tenantId = Guid.CreateVersion7();
        await using var context = CreateContext(CurrentUser(tenantId));
        for (var index = 0; index < EmbeddingBatchProcessor.MaximumBatchSize + 1; index++)
            await SeedChunk(
                context, tenantId, SourceVisibility.Tenant, $"regulation text {index}", $"batch-{index}");

        var provider = new DeterministicEmbeddingProvider();
        var store = new EfRegulationVectorStore(context);
        var processor = new EmbeddingBatchProcessor(
            context, provider, store, new FixedTimeProvider(Now));

        Assert.Equal(EmbeddingBatchProcessor.MaximumBatchSize, await processor.ProcessPendingAsync());
        Assert.Equal(1, await processor.ProcessPendingAsync());

        var failed = await SeedChunk(
            context, tenantId, SourceVisibility.Tenant, "provider failure", "failure");
        var failingProcessor = new EmbeddingBatchProcessor(
            context, new FailingEmbeddingProvider(), store, new FixedTimeProvider(Now));
        await Assert.ThrowsAsync<InvalidOperationException>(() => failingProcessor.ProcessPendingAsync());

        Assert.Equal(ChunkEmbeddingStatus.Failed, failed.EmbeddingStatus);
        Assert.NotNull(failed.EmbeddingError);
    }

    [Fact]
    public async Task CancelledBatchDoesNotChangePendingChunk()
    {
        var tenantId = Guid.CreateVersion7();
        await using var context = CreateContext(CurrentUser(tenantId));
        var chunk = await SeedChunk(
            context, tenantId, SourceVisibility.Tenant, "cancelled embedding", "cancelled");
        var provider = new DeterministicEmbeddingProvider();
        var processor = new EmbeddingBatchProcessor(
            context, provider, new EfRegulationVectorStore(context), new FixedTimeProvider(Now));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            processor.ProcessPendingAsync(cancellation.Token));

        Assert.Equal(ChunkEmbeddingStatus.Pending, chunk.EmbeddingStatus);
    }

    private static async Task<RegulatoryChunk> SeedChunk(
        RegulatoryComplianceDbContext context,
        Guid? tenantId,
        SourceVisibility visibility,
        string text,
        string key)
    {
        var document = visibility == SourceVisibility.Platform
            ? RegulatoryDocument.CreatePlatform(
                "Authority", $"Rule {key}", $"https://regulations.example/{key}",
                "GLOBAL", RegulationType.Customs, "en", Now)
            : RegulatoryDocument.CreateTenant(
                tenantId!.Value, "Authority", $"Rule {key}", $"https://regulations.example/{key}",
                "VN", RegulationType.Customs, "en", Now);
        var version = document.AddVersion(
            $"ingestion-{key}", "1", Now.AddDays(-2), Now.AddDays(-1), null,
            new string('a', 63) + (key.GetHashCode() & 0xf).ToString("x"),
            $"regulatory/{key}.txt", $"{key}.txt", "text/plain", text.Length, Now);
        version.StartIngestion(Now);
        var chunk = version.AddChunk(
            1, "Rule", "1", text, text.Split(' ').Length, 0, text.Length,
            new string('b', 64), Now);
        version.CompleteIngestion(Now);
        context.RegulatoryDocuments.Add(document);
        await context.SaveChangesAsync();
        return chunk;
    }

    private static RegulatoryComplianceDbContext CreateContext(
        CurrentUserService currentUser,
        string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<RegulatoryComplianceDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.CreateVersion7().ToString())
            .Options;
        return new RegulatoryComplianceDbContext(
            options, currentUser, new AuditSaveChangesInterceptor(currentUser));
    }

    private static CurrentUserService CurrentUser(Guid tenantId)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, [], []);
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, null, []);
        return currentUser;
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class FailingEmbeddingProvider : IEmbeddingProvider
    {
        public EmbeddingModelDescriptor Model { get; } = new("failing", "1", 64);

        public Task<IReadOnlyList<float[]>> GenerateAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Embedding provider failed.");
    }
}

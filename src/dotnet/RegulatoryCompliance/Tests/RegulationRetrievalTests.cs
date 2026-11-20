using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RegulatoryCompliance.Application.Embeddings;
using RegulatoryCompliance.Application.Retrieval;
using RegulatoryCompliance.Domain.Entities;
using RegulatoryCompliance.Domain.Enums;
using RegulatoryCompliance.Infrastructure.Persistences;
using Shared.Interceptors;
using Shared.Security;

namespace RegulatoryCompliance.Tests;

public sealed class RegulationRetrievalTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task QueryAppliesFiltersReturnsCompleteCitationsAndPersistsBoundedTrace()
    {
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantA);
        await using var context = CreateContext(currentUser);
        var expected = await Seed(context, tenantA, SourceVisibility.Tenant,
            "VN", RegulationType.Customs, "dangerous goods declaration required", "expected");
        await Seed(context, tenantB, SourceVisibility.Tenant,
            "VN", RegulationType.Customs, "dangerous goods declaration tenant b", "tenant-b");
        await Seed(context, tenantA, SourceVisibility.Tenant,
            "US", RegulationType.Customs, "dangerous goods declaration us", "wrong-jurisdiction");
        await Seed(context, null, SourceVisibility.Platform,
            "GLOBAL", RegulationType.Customs, "global dangerous goods rule", "platform");
        await EmbedAll(context);

        var result = await CreateService(context, currentUser).QueryAsync(Query());

        Assert.Equal(EvidenceSufficiency.Sufficient, result.EvidenceSufficiency);
        Assert.Contains(result.Evidence, evidence => evidence.ChunkId == expected.Id);
        Assert.All(result.Evidence, evidence =>
        {
            Assert.Contains(evidence.JurisdictionCode, new[] { "VN", "GLOBAL" });
            Assert.NotEqual(Guid.Empty, evidence.RegulatoryDocumentId);
            Assert.NotEqual(Guid.Empty, evidence.DocumentVersionId);
            Assert.StartsWith("https://", evidence.CanonicalSourceUri);
            Assert.False(string.IsNullOrWhiteSpace(evidence.Excerpt));
        });
        Assert.DoesNotContain(result.Evidence, evidence => evidence.Title.Contains("tenant-b"));
        Assert.DoesNotContain(result.Evidence, evidence => evidence.Title.Contains("wrong-jurisdiction"));

        var trace = await context.RetrievalTraces.SingleAsync();
        Assert.Equal(result.RetrievalTraceId, trace.Id);
        Assert.Equal(64, trace.QueryHash.Length);
        Assert.DoesNotContain("dangerous goods", trace.QueryHash);
        Assert.True(trace.RetrievedChunkIdsJson.Length < 50_000);
    }

    [Fact]
    public async Task QueryExcludesExpiredAndSupersededVersions()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = CreateContext(currentUser);
        await Seed(context, tenantId, SourceVisibility.Tenant,
            "VN", RegulationType.Customs, "expired dangerous goods", "expired",
            effectiveTo: Now.AddDays(-1));
        var (_, oldChunk, replacementChunk) = await SeedSuperseded(context, tenantId);
        await EmbedAll(context);

        var result = await CreateService(context, currentUser).QueryAsync(Query());

        Assert.DoesNotContain(result.Evidence, evidence => evidence.ChunkId == oldChunk.Id);
        Assert.Contains(result.Evidence, evidence => evidence.ChunkId == replacementChunk.Id);
        Assert.DoesNotContain(result.Evidence, evidence => evidence.Title.Contains("expired"));
    }

    [Fact]
    public async Task NoCandidateReturnsExplicitInsufficientEvidenceAndTrace()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = CreateContext(currentUser);

        var result = await CreateService(context, currentUser).QueryAsync(Query());

        Assert.Equal(EvidenceSufficiency.Insufficient, result.EvidenceSufficiency);
        Assert.Empty(result.Evidence);
        Assert.Contains("Insufficient", result.GeneratedExplanation);
        Assert.Single(await context.RetrievalTraces.ToListAsync());
    }

    [Fact]
    public async Task EmptyEmbeddingResponseReturnsInsufficientEvidenceInsteadOfThrowing()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = CreateContext(currentUser);

        var service = CreateService(context, currentUser, new EmptyEmbeddingProvider());
        var result = await service.QueryAsync(Query());

        Assert.Equal(EvidenceSufficiency.Insufficient, result.EvidenceSufficiency);
        Assert.Empty(result.Evidence);
        Assert.Single(await context.RetrievalTraces.ToListAsync());
    }

    [Fact]
    public async Task ConcurrentQueriesUseFactoryOwnedContextsAndPersistIndependentTraces()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        var databaseName = Guid.CreateVersion7().ToString();
        await using var context = CreateContext(currentUser, databaseName);
        await Seed(context, tenantId, SourceVisibility.Tenant,
            "VN", RegulationType.Customs, "dangerous goods declaration required", "concurrent");
        await EmbedAll(context);

        var factory = new TestDbContextFactory(databaseName, currentUser);
        var provider = new DeterministicEmbeddingProvider();
        var service = new RegulationRetrievalService(
            context,
            provider,
            new EfRegulationVectorStore(context, factory),
            currentUser,
            new FixedTimeProvider(Now),
            factory);

        var results = await Task.WhenAll(
            service.QueryAsync(Query()),
            service.QueryAsync(Query() with { Query = "declaration" }));

        Assert.All(results, result =>
        {
            Assert.Equal(EvidenceSufficiency.Sufficient, result.EvidenceSufficiency);
            Assert.NotEqual(Guid.Empty, result.RetrievalTraceId);
        });
        Assert.True(factory.CreatedCount >= 4);

        await using var verification = CreateContext(currentUser, databaseName);
        Assert.Equal(2, await verification.RetrievalTraces.CountAsync());
    }

    [Fact]
    public async Task HighlyOverlappingChunksAreDeduplicatedDeterministically()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = CreateContext(currentUser);
        var document = RegulatoryDocument.CreateTenant(
            tenantId, "Authority", "Overlap rule", "https://regulations.example/overlap",
            "VN", RegulationType.Customs, "en", Now.AddDays(-10));
        var version = document.AddVersion(
            "ingestion-overlap", "1", Now.AddDays(-10), Now.AddDays(-5), null,
            Hash("overlap"), "regulatory/overlap.txt", "overlap.txt", "text/plain", 100, Now.AddDays(-4));
        version.StartIngestion(Now.AddDays(-4));
        version.AddChunk(
            1, "Article", "1", "dangerous goods declaration is required", 5, 0, 39,
            Hash("overlap-1"), Now.AddDays(-4));
        version.AddChunk(
            2, "Article", "1", "goods declaration is required for dangerous cargo", 7, 20, 66,
            Hash("overlap-2"), Now.AddDays(-4));
        version.CompleteIngestion(Now.AddDays(-4));
        context.RegulatoryDocuments.Add(document);
        await context.SaveChangesAsync();
        await EmbedAll(context);

        var result = await CreateService(context, currentUser).QueryAsync(Query());

        Assert.Single(result.Evidence);
    }

    [Fact]
    public async Task MissingTenantAndInvalidBoundsAreRejected()
    {
        await using var context = CreateContext(new CurrentUserService());
        var service = CreateService(context, new CurrentUserService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.QueryAsync(Query()));

        var currentUser = CurrentUser(Guid.CreateVersion7());
        await using var validContext = CreateContext(currentUser);
        var validService = CreateService(validContext, currentUser);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            validService.QueryAsync(Query() with { TopK = RegulationRetrievalService.MaximumTopK + 1 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            validService.QueryAsync(Query() with { MinimumRelevanceScore = 1.1m }));
    }

    [Fact]
    public async Task CrossLingualFallbackFindsEnglishRegulationsWhenQueriedInVietnamese()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = CreateContext(currentUser);
        var chunk = await Seed(context, tenantId, SourceVisibility.Tenant,
            "VN", RegulationType.Customs, "UN3480 lithium battery declaration required", "cross-lang");
        await EmbedAll(context);

        var result = await CreateService(context, currentUser).QueryAsync(
            Query() with { LanguageCode = "vi", Query = "UN3480 lithium battery" });

        Assert.Equal(EvidenceSufficiency.Sufficient, result.EvidenceSufficiency);
        Assert.Contains(result.Evidence, e => e.ChunkId == chunk.Id);
    }

    [Fact]
    public async Task HybridSearchRescuesExactCodeMatchWhenVectorRankIsLowAndBoostsCombinedScore()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(CurrentUser(tenantId));

        await Seed(
            context,
            tenantId,
            SourceVisibility.Tenant,
            "VN",
            RegulationType.Customs,
            "General warehousing pallet stacking guidelines and domestic dock scheduling procedures.",
            "filler-doc");

        await Seed(
            context,
            tenantId,
            SourceVisibility.Tenant,
            "VN",
            RegulationType.Customs,
            "Special annex for UN3480 HS 8507.60 MSDS lithium ion battery packaging notice.",
            "target-un3480");

        await EmbedAll(context);

        var fillerChunk = await context.RegulatoryChunks
            .SingleAsync(c => c.NormalizedText.Contains("warehousing pallet"));
        var targetChunk = await context.RegulatoryChunks
            .SingleAsync(c => c.NormalizedText.Contains("UN3480"));

        var targetVec = targetChunk.Embedding!;
        var fillerVec = fillerChunk.Embedding!;
        var dotTf = Dot(fillerVec, targetVec);
        var orthoFiller = Normalize(
            Enumerable.Range(0, targetVec.Length)
                .Select(i => fillerVec[i] - dotTf * targetVec[i])
                .ToArray());

        float[]? neutralVec = null;
        for (var basis = 0; basis < targetVec.Length; basis++)
        {
            var candidate = new float[targetVec.Length];
            candidate[basis] = 1f;
            var dotT = Dot(candidate, targetVec);
            var dotF = Dot(candidate, orthoFiller);
            for (var i = 0; i < candidate.Length; i++)
                candidate[i] -= dotT * targetVec[i] + dotF * orthoFiller[i];

            var norm = MathF.Sqrt(candidate.Sum(x => x * x));
            if (norm > 0.1f)
            {
                neutralVec = candidate.Select(x => x / norm).ToArray();
                break;
            }
        }

        Assert.NotNull(neutralVec);
        var neutralWeight = MathF.Sqrt(MathF.Max(0f, 1f - 0.45f * 0.45f - 0.05f * 0.05f));
        var queryVec = Normalize(
            Enumerable.Range(0, targetVec.Length)
                .Select(i => 0.45f * orthoFiller[i] + 0.05f * targetVec[i] + neutralWeight * neutralVec![i])
                .ToArray());

        var service = CreateService(
            context,
            CurrentUser(tenantId),
            new FixedQueryEmbeddingProvider(new EmbeddingModelDescriptor("deterministic-local", "1", 64), queryVec));

        var result = await service.QueryAsync(new RegulationQueryInput(
            "UN3480 MSDS HS 8507.60",
            "VN",
            Now,
            "en",
            [RegulationType.Customs],
            2,
            0.25m));

        Assert.Contains(result.Evidence, item => item.ChunkId == targetChunk.Id);
        Assert.Equal(targetChunk.Id, result.Evidence[0].ChunkId);
    }

    [Fact]
    public async Task ShortChunkExpandsAdjacentNonOverlappingNeighborInSameSectionAtReadTime()
    {
        var tenantId = Guid.NewGuid();
        await using var context = CreateContext(CurrentUser(tenantId));

        var document = RegulatoryDocument.CreateTenant(
            tenantId,
            "Authority",
            "Rule article-5-doc",
            "https://regulations.example/article-5-doc",
            "VN",
            RegulationType.Customs,
            "en",
            Now.AddDays(-10));

        var text1 = "Article 5: Lithium battery air transport rules.";
        var text2 = "Penalty for undeclared cargo is 20,000,000 VND.";
        var text3 = "Other chapter tariff notice for cold chain containers.";
        var version = document.AddVersion(
            "ingestion-article-5-doc",
            "1",
            Now.AddDays(-10),
            Now.AddDays(-5),
            null,
            Hash("article-5-doc"),
            "regulatory/article-5-doc.txt",
            "article-5-doc.txt",
            "text/plain",
            text1.Length + text2.Length + text3.Length,
            Now.AddDays(-4));
        version.StartIngestion(Now.AddDays(-4));
        var chunk1 = version.AddChunk(
            1,
            "Article 5",
            "1",
            text1,
            7,
            0,
            text1.Length,
            Hash("article-5-chunk-1"),
            Now.AddDays(-4));
        var chunk2 = version.AddChunk(
            2,
            "Article 5",
            "1",
            text2,
            8,
            100,
            100 + text2.Length,
            Hash("article-5-chunk-2"),
            Now.AddDays(-4));
        version.AddChunk(
            3,
            "Other-Section-99",
            "2",
            text3,
            9,
            200,
            200 + text3.Length,
            Hash("article-5-chunk-3"),
            Now.AddDays(-4));
        version.CompleteIngestion(Now.AddDays(-4));

        context.RegulatoryDocuments.Add(document);
        await context.SaveChangesAsync();
        await EmbedAll(context);

        var service = CreateService(context, CurrentUser(tenantId));

        var firstResult = await service.QueryAsync(new RegulationQueryInput(
            "Article 5 lithium battery air transport rules",
            "VN",
            Now,
            "en",
            [RegulationType.Customs],
            1,
            0m));

        Assert.Single(firstResult.Evidence);
        Assert.Equal(chunk1.Id, firstResult.Evidence[0].ChunkId);
        Assert.Contains("Article 5: Lithium battery air transport rules.", firstResult.Evidence[0].Excerpt);
        Assert.Contains("Penalty for undeclared cargo is 20,000,000 VND.", firstResult.Evidence[0].Excerpt);

        var secondResult = await service.QueryAsync(new RegulationQueryInput(
            "Penalty for undeclared cargo 20,000,000 VND",
            "VN",
            Now,
            "en",
            [RegulationType.Customs],
            1,
            0m));

        Assert.Single(secondResult.Evidence);
        Assert.Equal(chunk2.Id, secondResult.Evidence[0].ChunkId);
        Assert.Contains("Article 5: Lithium battery air transport rules.", secondResult.Evidence[0].Excerpt);
        Assert.Contains("Penalty for undeclared cargo is 20,000,000 VND.", secondResult.Evidence[0].Excerpt);
        Assert.DoesNotContain("cold chain containers", secondResult.Evidence[0].Excerpt);
    }

    [Fact]
    public async Task EmptyLanguageCodeSearchesAllLanguagesAndPersistsTraceWithAnyLanguage()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = CreateContext(currentUser);
        var chunk = await Seed(context, tenantId, SourceVisibility.Tenant,
            "VN", RegulationType.Customs, "dangerous goods declaration required", "empty-lang");
        await EmbedAll(context);

        var result = await CreateService(context, currentUser).QueryAsync(
            Query() with { LanguageCode = string.Empty });

        Assert.Equal(EvidenceSufficiency.Sufficient, result.EvidenceSufficiency);
        Assert.Contains(result.Evidence, item => item.ChunkId == chunk.Id);

        var trace = await context.RetrievalTraces.SingleAsync();
        Assert.Equal(result.RetrievalTraceId, trace.Id);
        Assert.Equal("any", trace.LanguageCode);
    }

    private static float Dot(float[] left, float[] right)
    {
        var sum = 0f;
        for (var i = 0; i < left.Length; i++)
            sum += left[i] * right[i];
        return sum;
    }

    private static float[] Normalize(float[] vector)
    {
        var norm = MathF.Sqrt(vector.Sum(x => x * x));
        return norm <= 0f ? vector : vector.Select(x => x / norm).ToArray();
    }

    private sealed class FixedQueryEmbeddingProvider(EmbeddingModelDescriptor model, float[] queryVector)
        : IEmbeddingProvider
    {
        public EmbeddingModelDescriptor Model => model;

        public Task<IReadOnlyList<float[]>> GenerateAsync(
            IReadOnlyList<EmbeddingInput> inputs,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<float[]>>(
                inputs.Select(_ => queryVector).ToArray());
    }

    private static RegulationQueryInput Query() =>
        new(
            "dangerous goods declaration",
            "VN",
            Now,
            "en",
            [RegulationType.Customs],
            10,
            0m);

    private static RegulationRetrievalService CreateService(
        RegulatoryComplianceDbContext context,
        ICurrentUserService currentUser,
        IEmbeddingProvider? embeddingProvider = null)
    {
        var provider = embeddingProvider ?? new DeterministicEmbeddingProvider();
        return new RegulationRetrievalService(
            context,
            provider,
            new EfRegulationVectorStore(context),
            currentUser,
            new FixedTimeProvider(Now));
    }

    private static async Task EmbedAll(RegulatoryComplianceDbContext context)
    {
        var provider = new DeterministicEmbeddingProvider();
        var processor = new EmbeddingBatchProcessor(
            context,
            provider,
            new EfRegulationVectorStore(context),
            new FixedTimeProvider(Now));
        while (await processor.ProcessPendingAsync() > 0)
        {
        }
    }

    private static async Task<RegulatoryChunk> Seed(
        RegulatoryComplianceDbContext context,
        Guid? tenantId,
        SourceVisibility visibility,
        string jurisdiction,
        RegulationType type,
        string text,
        string key,
        DateTimeOffset? effectiveTo = null)
    {
        var document = visibility == SourceVisibility.Platform
            ? RegulatoryDocument.CreatePlatform(
                "Authority", $"Rule {key}", $"https://regulations.example/{key}",
                jurisdiction, type, "en", Now.AddDays(-10))
            : RegulatoryDocument.CreateTenant(
                tenantId!.Value, "Authority", $"Rule {key}", $"https://regulations.example/{key}",
                jurisdiction, type, "en", Now.AddDays(-10));
        var version = AddVersion(document, key, text, effectiveTo);
        var chunk = version.Chunks.Single();
        context.RegulatoryDocuments.Add(document);
        await context.SaveChangesAsync();
        return chunk;
    }

    private static async Task<(RegulatoryDocument Document, RegulatoryChunk Old, RegulatoryChunk Replacement)>
        SeedSuperseded(RegulatoryComplianceDbContext context, Guid tenantId)
    {
        var document = RegulatoryDocument.CreateTenant(
            tenantId, "Authority", "Rule superseded", "https://regulations.example/superseded",
            "VN", RegulationType.Customs, "en", Now.AddDays(-10));
        var old = AddVersion(document, "old", "old dangerous goods declaration", null);
        var replacement = document.AddVersion(
            "ingestion-new", "2", Now.AddDays(-2), Now.AddDays(-1), null,
            Hash("replacement"), "regulatory/new.txt", "new.txt", "text/plain", 33, Now, old.Id);
        replacement.StartIngestion(Now);
        var replacementChunk = replacement.AddChunk(
            1, "Article 2", "2", "current dangerous goods declaration", 4, 0, 35,
            Hash("replacement-chunk"), Now);
        replacement.CompleteIngestion(Now);
        context.RegulatoryDocuments.Add(document);
        await context.SaveChangesAsync();
        return (document, old.Chunks.Single(), replacementChunk);
    }

    private static RegulatoryDocumentVersion AddVersion(
        RegulatoryDocument document,
        string key,
        string text,
        DateTimeOffset? effectiveTo)
    {
        var version = document.AddVersion(
            $"ingestion-{key}", "1", Now.AddDays(-10), Now.AddDays(-5), effectiveTo,
            Hash(key), $"regulatory/{key}.txt", $"{key}.txt", "text/plain", text.Length, Now.AddDays(-4));
        version.StartIngestion(Now.AddDays(-4));
        version.AddChunk(
            1, "Article 1", "1", text, text.Split(' ').Length, 0, text.Length,
            Hash($"{key}-chunk"), Now.AddDays(-4));
        version.CompleteIngestion(Now.AddDays(-4));
        return version;
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static RegulatoryComplianceDbContext CreateContext(CurrentUserService currentUser) =>
        CreateContext(currentUser, Guid.CreateVersion7().ToString());

    private static RegulatoryComplianceDbContext CreateContext(
        CurrentUserService currentUser,
        string databaseName)
    {
        var options = new DbContextOptionsBuilder<RegulatoryComplianceDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new RegulatoryComplianceDbContext(
            options, currentUser, new AuditSaveChangesInterceptor(currentUser));
    }

    private static CurrentUserService CurrentUser(Guid tenantId)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, null, []);
        return currentUser;
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class EmptyEmbeddingProvider : IEmbeddingProvider
    {
        public EmbeddingModelDescriptor Model { get; } = new("empty", "1", 768);

        public Task<IReadOnlyList<float[]>> GenerateAsync(
            IReadOnlyList<EmbeddingInput> inputs,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<float[]>>([]);
    }

    private sealed class TestDbContextFactory(
        string databaseName,
        CurrentUserService currentUser) : IDbContextFactory<RegulatoryComplianceDbContext>
    {
        public int CreatedCount { get; private set; }

        public RegulatoryComplianceDbContext CreateDbContext()
        {
            CreatedCount++;
            var options = new DbContextOptionsBuilder<RegulatoryComplianceDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;
            return new RegulatoryComplianceDbContext(
                options, currentUser, new AuditSaveChangesInterceptor(currentUser));
        }

        public ValueTask<RegulatoryComplianceDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(CreateDbContext());
    }
}

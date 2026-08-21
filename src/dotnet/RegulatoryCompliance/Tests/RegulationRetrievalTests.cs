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
        ICurrentUserService currentUser)
    {
        var provider = new DeterministicEmbeddingProvider();
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

    private static RegulatoryComplianceDbContext CreateContext(CurrentUserService currentUser)
    {
        var options = new DbContextOptionsBuilder<RegulatoryComplianceDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        return new RegulatoryComplianceDbContext(
            options, currentUser, new AuditSaveChangesInterceptor(currentUser));
    }

    private static CurrentUserService CurrentUser(Guid tenantId)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, [], []);
        return currentUser;
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}

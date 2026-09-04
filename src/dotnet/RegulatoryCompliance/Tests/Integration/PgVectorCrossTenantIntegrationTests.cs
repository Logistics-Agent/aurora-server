using Microsoft.EntityFrameworkCore;
using RegulatoryCompliance.Application.Embeddings;
using RegulatoryCompliance.Domain.Entities;
using RegulatoryCompliance.Domain.Enums;
using RegulatoryCompliance.Infrastructure.Persistences;
using Shared.Entity;
using Shared.Interceptors;
using Shared.Security;

namespace RegulatoryCompliance.Tests.Integration;

public sealed class PgVectorCrossTenantIntegrationTests
{
    [Fact]
    public async Task QueryRegulations_EnforcesStrictTenantIsolation_NeverLeaksTenantBToTenantA()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<RegulatoryComplianceDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var currentUserA = new CurrentUserService();
        currentUserA.Populate(Guid.NewGuid(), tenantA, null, null, [], []);
        currentUserA.Populate(Guid.NewGuid(), tenantA, null, null, null, []);

        var auditInterceptor = new AuditSaveChangesInterceptor(currentUserA);
        await using var dbContext = new RegulatoryComplianceDbContext(options, currentUserA, auditInterceptor);

        // 1. Create Tenant A Document & Version & Chunk
        var docA = RegulatoryDocument.CreateTenant(tenantA, "VN_CUSTOMS", "Tenant A Rule", "uri://tenantA/doc1", "VN", RegulationType.Customs, "vi", now);
        dbContext.RegulatoryDocuments.Add(docA);
        var verA = docA.AddVersion("key-a-1", "1.0", now, now.AddDays(-1), null, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", "ref-a", "docA.txt", "text/plain", 100, now, null);
        verA.StartIngestion(now);
        var chunkA = verA.AddChunk(1, "1", "1", "Tenant A private tariff classification guide", 10, 0, 50, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", now);
        verA.CompleteIngestion(now);

        // 2. Create Tenant B Document & Version & Chunk (Exact same content/vector)
        var docB = RegulatoryDocument.CreateTenant(tenantB, "VN_CUSTOMS", "Tenant B Rule", "uri://tenantB/doc1", "VN", RegulationType.Customs, "vi", now);
        dbContext.RegulatoryDocuments.Add(docB);
        var verB = docB.AddVersion("key-b-1", "1.0", now, now.AddDays(-1), null, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", "ref-b", "docB.txt", "text/plain", 100, now, null);
        verB.StartIngestion(now);
        var chunkB = verB.AddChunk(1, "1", "1", "Tenant B confidential customs pricing formula", 10, 0, 50, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", now);
        verB.CompleteIngestion(now);

        // 3. Create Platform Document
        var docPlat = RegulatoryDocument.CreatePlatform("WCO", "Platform HS Code Standard", "uri://wco/hs2022", "GLOBAL", RegulationType.Customs, "en", now);
        dbContext.RegulatoryDocuments.Add(docPlat);
        var verPlat = docPlat.AddVersion("key-plat-1", "1.0", now, now.AddDays(-1), null, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", "ref-plat", "plat.txt", "text/plain", 100, now, null);
        verPlat.StartIngestion(now);
        var chunkPlat = verPlat.AddChunk(1, "1", "1", "WCO General Rules of Interpretation", 10, 0, 50, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", now);
        verPlat.CompleteIngestion(now);

        // Seed 768-dim identical vectors
        var vector768 = new float[768];
        vector768[0] = 1.0f;
        chunkA.MarkEmbedded(vector768, "text-embedding-004", "1.0", 768, now);
        chunkB.MarkEmbedded(vector768, "text-embedding-004", "1.0", 768, now);
        chunkPlat.MarkEmbedded(vector768, "text-embedding-004", "1.0", 768, now);

        await dbContext.SaveChangesAsync();

        // Act - Query as Tenant A
        var vectorStore = new PgVectorRegulationVectorStore(dbContext);
        var results = await vectorStore.SearchAsync(
            new VectorSearchRequest(
                vector768,
                "text-embedding-004",
                "1.0",
                768,
                [chunkA.Id, chunkB.Id, chunkPlat.Id],
                10,
                0.5m));

        // Assert
        Assert.NotEmpty(results);
        // Tenant B chunk must NEVER be in Tenant A query results
        Assert.DoesNotContain(results, r => r.ChunkId == chunkB.Id);
        Assert.Contains(results, r => r.ChunkId == chunkA.Id);
        Assert.Contains(results, r => r.ChunkId == chunkPlat.Id);
    }

    [Fact]
    public async Task QueryKnowledge_EnforcesStrictTenantIsolation_AndCategoryFiltering()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<RegulatoryComplianceDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var currentUserA = new CurrentUserService();
        currentUserA.Populate(Guid.NewGuid(), tenantA, null, null, [], []);
        currentUserA.Populate(Guid.NewGuid(), tenantA, null, null, null, []);

        var auditInterceptor = new AuditSaveChangesInterceptor(currentUserA);
        await using var dbContext = new RegulatoryComplianceDbContext(options, currentUserA, auditInterceptor);

        // Tenant A SOP
        var docA = KnowledgeDocument.CreateTenant(tenantA, KnowledgeCategory.Sop, "Tenant A Warehousing SOP", "sop://a/1", "vi", now);
        dbContext.KnowledgeDocuments.Add(docA);
        var verA = docA.AddVersion("k-key-a", "1.0", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", "r-a", "sopA.txt", "text/plain", 100, now);
        var chunkA = verA.AddChunk(1, "1", "1", "Standard storage procedures for tenant A", 10, 0, 50, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", now);
        verA.MarkCompleted(now);

        // Tenant B SOP
        var docB = KnowledgeDocument.CreateTenant(tenantB, KnowledgeCategory.Sop, "Tenant B Secret SOP", "sop://b/1", "vi", now);
        dbContext.KnowledgeDocuments.Add(docB);
        var verB = docB.AddVersion("k-key-b", "1.0", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", "r-b", "sopB.txt", "text/plain", 100, now);
        var chunkB = verB.AddChunk(1, "1", "1", "Secret processes for tenant B", 10, 0, 50, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", now);
        verB.MarkCompleted(now);

        // Platform Guide
        var docPlat = KnowledgeDocument.CreatePlatform(KnowledgeCategory.Guide, "Global Logistics Best Practice", "sop://plat/1", "en", now);
        dbContext.KnowledgeDocuments.Add(docPlat);
        var verPlat = docPlat.AddVersion("k-key-plat", "1.0", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", "r-plat", "sopPlat.txt", "text/plain", 100, now);
        var chunkPlat = verPlat.AddChunk(1, "1", "1", "General cold chain storage practices", 10, 0, 50, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", now);
        verPlat.MarkCompleted(now);

        var vector768 = new float[768];
        vector768[0] = 1.0f;
        chunkA.MarkEmbedded(vector768, "text-embedding-004", "1.0", 768, now);
        chunkB.MarkEmbedded(vector768, "text-embedding-004", "1.0", 768, now);
        chunkPlat.MarkEmbedded(vector768, "text-embedding-004", "1.0", 768, now);

        await dbContext.SaveChangesAsync();

        // Act - Query as Tenant A
        var vectorStore = new PgVectorKnowledgeVectorStore(dbContext);
        var results = await vectorStore.SearchAsync(
            new VectorSearchRequest(
                vector768,
                "text-embedding-004",
                "1.0",
                768,
                [chunkA.Id, chunkB.Id, chunkPlat.Id],
                10,
                0.5m));

        // Assert
        Assert.NotEmpty(results);
        Assert.DoesNotContain(results, r => r.ChunkId == chunkB.Id);
        Assert.Contains(results, r => r.ChunkId == chunkA.Id);
        Assert.Contains(results, r => r.ChunkId == chunkPlat.Id);
    }
}

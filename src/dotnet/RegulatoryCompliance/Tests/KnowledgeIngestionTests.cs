using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RegulatoryCompliance.Application.Embeddings;
using RegulatoryCompliance.Application.Ingestion;
using RegulatoryCompliance.Domain.Enums;
using RegulatoryCompliance.Infrastructure.Persistences;
using Shared.Interceptors;
using Shared.Security;

namespace RegulatoryCompliance.Tests;

public sealed class KnowledgeIngestionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TenantKnowledgeRequiresDocumentsIngestPermission()
    {
        var currentUser = CurrentUser(Guid.CreateVersion7());
        await using var context = CreateContext(currentUser);
        var service = CreateService(context, currentUser);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.IngestAsync(CreateInput()));
    }

    [Fact]
    public async Task KnowledgeIngestionRejectsEmptyContentAndMismatchedHash()
    {
        var currentUser = CurrentUser(Guid.CreateVersion7(), "documents:ingest");
        await using var context = CreateContext(currentUser);
        var service = CreateService(context, currentUser);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.IngestAsync(CreateInput() with { Content = ReadOnlyMemory<byte>.Empty, SizeBytes = 0, ContentSha256 = Sha256([]) }));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.IngestAsync(CreateInput() with { ContentSha256 = new string('0', 64) }));
    }

    [Fact]
    public async Task PendingOcrKnowledgeIntakeCreatesAndReplaysVersion()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId, "documents:ingest");
        await using var context = CreateContext(currentUser);
        var service = CreateService(context, currentUser);
        var input = new KnowledgePendingOcrInput(
            "knowledge-pending-001",
            "Warehouse SOP",
            KnowledgeCategory.Sop,
            "https://docs.example.test/sop/warehouse",
            "en",
            "1.0",
            $"tenants/{tenantId}/documents/{Guid.CreateVersion7()}/warehouse.pdf",
            "warehouse.pdf",
            "application/pdf",
            128,
            new string('c', 64),
            SourceVisibility.Tenant);

        var first = await service.CreatePendingOcrAsync(input);
        var replay = await service.CreatePendingOcrAsync(input);

        Assert.Equal(RegulatoryIngestionStatus.PendingOcr, first.Status);
        Assert.Equal(first.DocumentVersionId, replay.DocumentVersionId);
        Assert.True(replay.Replayed);
    }

    [Fact]
    public async Task TenantlessUserCannotCreatePendingOcrKnowledgeVersion()
    {
        var currentUser = CurrentUser(null, "documents:ingest");
        await using var context = CreateContext(currentUser);
        var service = CreateService(context, currentUser);
        var tenantId = Guid.CreateVersion7();
        var input = new KnowledgePendingOcrInput(
            "knowledge-pending-tenantless",
            "Warehouse SOP",
            KnowledgeCategory.Sop,
            "https://docs.example.test/sop/warehouse",
            "en",
            "1.0",
            $"tenants/{tenantId}/documents/{Guid.CreateVersion7()}/warehouse.pdf",
            "warehouse.pdf",
            "application/pdf",
            128,
            new string('c', 64),
            SourceVisibility.Tenant);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreatePendingOcrAsync(input));
    }

    private static KnowledgeIngestionService CreateService(
        RegulatoryComplianceDbContext context,
        ICurrentUserService currentUser) =>
        new(
            context,
            new DeterministicRegulatoryChunker(),
            new DeterministicEmbeddingProvider(),
            new RegulatoryCompliance.Infrastructure.Persistences.PgVectorKnowledgeVectorStore(context),
            currentUser,
            new FixedTimeProvider(Now));

    private static RegulatoryComplianceDbContext CreateContext(CurrentUserService currentUser)
    {
        var options = new DbContextOptionsBuilder<RegulatoryComplianceDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        return new RegulatoryComplianceDbContext(
            options, currentUser, new AuditSaveChangesInterceptor(currentUser));
    }

    private static KnowledgeIngestionInput CreateInput()
    {
        var content = Encoding.UTF8.GetBytes("# Warehouse SOP\nHandle dangerous goods carefully.");
        return new KnowledgeIngestionInput(
            "knowledge-001",
            "Warehouse SOP",
            KnowledgeCategory.Sop,
            "https://docs.example.test/sop/warehouse",
            "en",
            "1.0",
            "knowledge/tenant/warehouse.md",
            "warehouse.md",
            "text/markdown",
            content.Length,
            Sha256(content),
            content,
            SourceVisibility.Tenant);
    }

    private static CurrentUserService CurrentUser(Guid? tenantId, params string[] permissions)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, null, [.. permissions]);
        return currentUser;
    }

    private static string Sha256(ReadOnlySpan<byte> content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}

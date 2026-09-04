using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RegulatoryCompliance.Application.Ingestion;
using RegulatoryCompliance.Domain.Enums;
using RegulatoryCompliance.Infrastructure.Persistences;
using Shared.Interceptors;
using Shared.Security;

namespace RegulatoryCompliance.Tests;

public sealed class RegulatoryIngestionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AuthorizedTenantIngestionCreatesDeterministicPendingEmbeddingChunks()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId, RegulatoryIngestionService.TenantIngestionPermission);
        await using var context = CreateContext(currentUser);
        var service = CreateService(context, currentUser);

        var result = await service.IngestAsync(CreateInput(
            "# Article 1\r\n[[PAGE:7]]\r\nDangerous   goods require declaration."));

        Assert.False(result.Replayed);
        Assert.Equal(RegulatoryIngestionStatus.Completed, result.Status);
        var version = await context.RegulatoryDocumentVersions
            .Include(item => item.Chunks)
            .SingleAsync();
        Assert.Equal("Article 1", version.Chunks.First().SectionLabel);
        Assert.All(version.Chunks, chunk => Assert.Equal(ChunkEmbeddingStatus.Pending, chunk.EmbeddingStatus));
        Assert.Equal(Enumerable.Range(1, version.Chunks.Count), version.Chunks.Select(chunk => chunk.Sequence));
    }

    [Fact]
    public async Task ReplayReturnsExistingVersionAndNewKeyCreatesImmutableVersion()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId, RegulatoryIngestionService.TenantIngestionPermission);
        await using var context = CreateContext(currentUser);
        var service = CreateService(context, currentUser);
        var firstInput = CreateInput("# Rule\nOriginal text.");

        var first = await service.IngestAsync(firstInput);
        var replay = await service.IngestAsync(firstInput);
        var second = await service.IngestAsync(CreateInput(
            "# Rule\nChanged text.", "ingestion-002", "2026.2"));

        Assert.True(replay.Replayed);
        Assert.Equal(first.DocumentVersionId, replay.DocumentVersionId);
        Assert.NotEqual(first.DocumentVersionId, second.DocumentVersionId);
        Assert.Equal(2, await context.RegulatoryDocumentVersions.CountAsync());
        Assert.NotNull((await context.RegulatoryDocumentVersions
            .SingleAsync(item => item.Id == first.DocumentVersionId)).SupersededAt);
    }

    [Fact]
    public async Task MissingPermissionTenantAndUnsafeMetadataAreRejected()
    {
        var noPermissionUser = CurrentUser(Guid.CreateVersion7());
        await using var noPermissionContext = CreateContext(noPermissionUser);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService(noPermissionContext, noPermissionUser).IngestAsync(CreateInput("Rule text.")));

        var noTenantUser = CurrentUser(null, RegulatoryIngestionService.TenantIngestionPermission);
        await using var noTenantContext = CreateContext(noTenantUser);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(noTenantContext, noTenantUser).IngestAsync(CreateInput("Rule text.")));

        var unsafeUser = CurrentUser(
            Guid.CreateVersion7(), RegulatoryIngestionService.TenantIngestionPermission);
        await using var unsafeContext = CreateContext(unsafeUser);
        var unsafeInput = CreateInput("Rule text.") with { ContentReference = "../../etc/passwd" };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            CreateService(unsafeContext, unsafeUser).IngestAsync(unsafeInput));
    }

    [Fact]
    public async Task MalformedOversizedAndHashMismatchedContentAreRejected()
    {
        var currentUser = CurrentUser(
            Guid.CreateVersion7(), RegulatoryIngestionService.TenantIngestionPermission);
        await using var context = CreateContext(currentUser);
        var service = CreateService(context, currentUser);

        var hashMismatch = CreateInput("Rule text.") with { ContentSha256 = new string('a', 64) };
        await Assert.ThrowsAsync<ArgumentException>(() => service.IngestAsync(hashMismatch));

        var oversized = new byte[RegulatoryIngestionService.MaximumContentBytes + 1];
        var oversizedInput = CreateInput("Rule text.") with
        {
            Content = oversized,
            SizeBytes = oversized.Length,
            ContentSha256 = Sha256(oversized)
        };
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.IngestAsync(oversizedInput));

        var invalidUtf8 = new byte[] { 0xff, 0xfe };
        var malformed = CreateInput("Rule text.") with
        {
            Content = invalidUtf8,
            SizeBytes = invalidUtf8.Length,
            ContentSha256 = Sha256(invalidUtf8)
        };
        await Assert.ThrowsAsync<ArgumentException>(() => service.IngestAsync(malformed));
    }

    [Fact]
    public async Task ChunkingFailureDoesNotPersistPartialVersion()
    {
        var currentUser = CurrentUser(
            Guid.CreateVersion7(), RegulatoryIngestionService.TenantIngestionPermission);
        await using var context = CreateContext(currentUser);
        var service = new RegulatoryIngestionService(
            context, new FailingChunker(), currentUser, new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<FormatException>(() => service.IngestAsync(CreateInput("Malformed.")));

        Assert.Empty(await context.RegulatoryDocuments.ToListAsync());
        Assert.Empty(await context.RegulatoryDocumentVersions.ToListAsync());
    }

    [Fact]
    public void ChunkerIsDeterministicAndRetainsCitationLabelsAndOffsets()
    {
        var chunker = new DeterministicRegulatoryChunker();
        const string input = "# Article 4\r\n[[PAGE:12]]\r\nGoods   require a declaration.";

        var first = chunker.Chunk(input);
        var second = chunker.Chunk(input);

        Assert.Equal(first, second);
        Assert.Equal("Article 4", first[0].SectionLabel);
        Assert.Equal("12", first[^1].PageLabel);
        Assert.All(first, chunk => Assert.True(chunk.EndOffset > chunk.StartOffset));
    }

    private static RegulatoryIngestionService CreateService(
        RegulatoryComplianceDbContext context,
        ICurrentUserService currentUser) =>
        new(context, new DeterministicRegulatoryChunker(), currentUser, new FixedTimeProvider(Now));

    private static RegulatoryComplianceDbContext CreateContext(CurrentUserService currentUser)
    {
        var options = new DbContextOptionsBuilder<RegulatoryComplianceDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString())
            .Options;
        return new RegulatoryComplianceDbContext(
            options, currentUser, new AuditSaveChangesInterceptor(currentUser));
    }

    private static CurrentUserService CurrentUser(Guid? tenantId, params string[] permissions)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, null, [.. permissions]);
        return currentUser;
    }

    private static RegulatoryIngestionInput CreateInput(
        string content,
        string idempotencyKey = "ingestion-001",
        string versionLabel = "2026.1")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new RegulatoryIngestionInput(
            idempotencyKey,
            "Customs Authority",
            "Dangerous Goods Rule",
            "https://regulations.example/dangerous-goods",
            "VN",
            RegulationType.DangerousGoods,
            "en",
            versionLabel,
            Now.AddDays(-30),
            Now.AddDays(-1),
            null,
            $"regulatory/vn/{versionLabel}.md",
            $"{versionLabel}.md",
            "text/markdown",
            bytes.Length,
            Sha256(bytes),
            bytes,
            SourceVisibility.Tenant);
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed class FailingChunker : IRegulatoryChunker
    {
        public IReadOnlyList<RegulatoryChunkDraft> Chunk(string content) =>
            throw new FormatException("Malformed regulatory content.");
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}

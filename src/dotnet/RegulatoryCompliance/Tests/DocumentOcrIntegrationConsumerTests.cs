using DocumentOcr.Contracts.Events;
using Microsoft.Extensions.Logging.Abstractions;
using RegulatoryCompliance.Application.Embeddings;
using RegulatoryCompliance.Application.Events;
using RegulatoryCompliance.Application.Ingestion;
using RegulatoryCompliance.Infrastructure.Persistences;
using RegulatoryCompliance.Domain.Entities;
using RegulatoryCompliance.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Shared.Interceptors;
using Shared.Security;

namespace RegulatoryCompliance.Tests;

public sealed class DocumentOcrIntegrationConsumerTests
{
    [Fact]
    public async Task Unsupported_completed_version_is_rethrown_for_retry_or_dead_letter()
    {
        var consumer = new DocumentOcrIntegrationConsumer(
            null!,
            new DeterministicEmbeddingProvider(),
            new DeterministicRegulatoryChunker(),
            TimeProvider.System,
            NullLogger<DocumentOcrIntegrationConsumer>.Instance);

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => consumer.HandleAsync(
            new DocumentOcrCompletedEvent { ContractVersion = 1 }));

        Assert.Contains("contract version 1", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Shipment_document_purpose_does_not_parse_context_prefix()
    {
        var consumer = new DocumentOcrIntegrationConsumer(
            null!,
            new DeterministicEmbeddingProvider(),
            new DeterministicRegulatoryChunker(),
            TimeProvider.System,
            NullLogger<DocumentOcrIntegrationConsumer>.Instance);

        await consumer.HandleAsync(new DocumentOcrCompletedEvent
        {
            Purpose = DocumentOcrPurpose.ShipmentDocument,
            ExternalContextId = "knowledge:not-a-resource-id"
        });
    }

    [Fact]
    public async Task General_document_purpose_is_ignored_without_corpus_lookup()
    {
        var consumer = new DocumentOcrIntegrationConsumer(
            null!,
            new DeterministicEmbeddingProvider(),
            new DeterministicRegulatoryChunker(),
            TimeProvider.System,
            NullLogger<DocumentOcrIntegrationConsumer>.Instance);

        await consumer.HandleAsync(new DocumentOcrCompletedEvent
        {
            Purpose = DocumentOcrPurpose.GeneralDocument,
            ExternalContextId = "opaque-external-reference"
        });
    }

    [Fact]
    public async Task Knowledge_completion_uses_full_text_and_embeds_the_pending_version()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, null, ["documents:ingest"]);
        var databaseName = Guid.CreateVersion7().ToString();
        var options = new DbContextOptionsBuilder<RegulatoryComplianceDbContext>()
                .UseInMemoryDatabase(databaseName).Options;
        await using var context = new RegulatoryComplianceDbContext(
            options,
            currentUser,
            new AuditSaveChangesInterceptor(currentUser));
        var document = KnowledgeDocument.CreateTenant(
            tenantId, KnowledgeCategory.Sop, "Warehouse SOP", "https://docs.example.test/sop", "en", DateTimeOffset.UtcNow);
        var version = document.AddVersion(
            "ocr-intake-001", "1.0", new string('d', 64),
            $"tenants/{tenantId}/documents/{Guid.CreateVersion7()}/sop.pdf",
            "sop.pdf", "application/pdf", 128, DateTimeOffset.UtcNow);
        version.MarkPendingOcr(DateTimeOffset.UtcNow);
        context.KnowledgeDocuments.Add(document);
        await context.SaveChangesAsync();

        await using var processingContext = new RegulatoryComplianceDbContext(
            new DbContextOptionsBuilder<RegulatoryComplianceDbContext>()
                .UseInMemoryDatabase(databaseName).Options,
            currentUser,
            new AuditSaveChangesInterceptor(currentUser));
        Assert.Equal(1, await processingContext.KnowledgeDocumentVersions.CountAsync());

        var consumer = new DocumentOcrIntegrationConsumer(
            processingContext,
            new DeterministicEmbeddingProvider(),
            new DeterministicRegulatoryChunker(),
            TimeProvider.System,
            NullLogger<DocumentOcrIntegrationConsumer>.Instance);

        await consumer.HandleAsync(new DocumentOcrCompletedEvent
        {
            TenantId = tenantId,
            Purpose = DocumentOcrPurpose.KnowledgeCorpus,
            ExternalContextId = version.Id.ToString(),
            FullTextContent = "# Warehouse SOP\nHandle dangerous goods carefully."
        });

        var savedVersion = await processingContext.KnowledgeDocumentVersions
            .Include(item => item.Chunks)
            .SingleAsync();
        Assert.Equal(RegulatoryIngestionStatus.Completed, savedVersion.IngestionStatus);
        Assert.NotEmpty(savedVersion.Chunks);
        Assert.All(savedVersion.Chunks, chunk => Assert.Equal(ChunkEmbeddingStatus.Completed, chunk.EmbeddingStatus));
    }

    [Fact]
    public async Task Regulatory_completion_persists_chunks_for_background_embedding()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, null, ["documents:ingest"]);
        var databaseName = Guid.CreateVersion7().ToString();
        var options = new DbContextOptionsBuilder<RegulatoryComplianceDbContext>()
            .UseInMemoryDatabase(databaseName).Options;
        await using var context = new RegulatoryComplianceDbContext(
            options,
            currentUser,
            new AuditSaveChangesInterceptor(currentUser));
        var document = RegulatoryDocument.CreateTenant(
            tenantId,
            "GuardM Authority",
            "Trade Handling Standard",
            "https://docs.example.test/trade-handling",
            "VN",
            RegulationType.ImportRestriction,
            "vi",
            DateTimeOffset.UtcNow);
        var version = document.AddVersion(
            "ocr-regulatory-001",
            "1.0",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            new string('e', 64),
            $"tenants/{tenantId}/documents/{Guid.CreateVersion7()}/regulation.md",
            "regulation.md",
            "text/markdown",
            256,
            DateTimeOffset.UtcNow,
            null);
        version.MarkPendingOcr(DateTimeOffset.UtcNow);
        context.RegulatoryDocuments.Add(document);
        await context.SaveChangesAsync();

        await using var processingContext = new RegulatoryComplianceDbContext(
            new DbContextOptionsBuilder<RegulatoryComplianceDbContext>()
                .UseInMemoryDatabase(databaseName).Options,
            currentUser,
            new AuditSaveChangesInterceptor(currentUser));
        var provider = new CapturingEmbeddingProvider();
        var consumer = new DocumentOcrIntegrationConsumer(
            processingContext,
            provider,
            new DeterministicRegulatoryChunker(),
            TimeProvider.System,
            NullLogger<DocumentOcrIntegrationConsumer>.Instance);

        await consumer.HandleAsync(new DocumentOcrCompletedEvent
        {
            TenantId = tenantId,
            Purpose = DocumentOcrPurpose.RegulatoryCorpus,
            ExternalContextId = version.Id.ToString(),
            FullTextContent = "# Trade handling\nImporters must provide a valid customs declaration before release."
        });

        Assert.Empty(provider.Inputs);

        var savedVersion = await processingContext.RegulatoryDocumentVersions
            .Include(item => item.Chunks)
            .SingleAsync();
        Assert.Equal(RegulatoryIngestionStatus.Completed, savedVersion.IngestionStatus);
        Assert.NotEmpty(savedVersion.Chunks);
        Assert.All(savedVersion.Chunks, chunk => Assert.Equal(ChunkEmbeddingStatus.Pending, chunk.EmbeddingStatus));
    }

    [Fact]
    public async Task Exhausted_regulatory_completion_marks_pending_version_failed()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, null, ["documents:ingest"]);
        var databaseName = Guid.CreateVersion7().ToString();
        var options = new DbContextOptionsBuilder<RegulatoryComplianceDbContext>()
            .UseInMemoryDatabase(databaseName).Options;
        await using var context = new RegulatoryComplianceDbContext(
            options,
            currentUser,
            new AuditSaveChangesInterceptor(currentUser));
        var document = RegulatoryDocument.CreateTenant(
            tenantId,
            "GuardM Authority",
            "Trade Handling Standard",
            "https://docs.example.test/trade-handling",
            "VN",
            RegulationType.ImportRestriction,
            "vi",
            DateTimeOffset.UtcNow);
        var version = document.AddVersion(
            "ocr-regulatory-fault-001",
            "1.0",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            new string('f', 64),
            $"tenants/{tenantId}/documents/{Guid.CreateVersion7()}/regulation.md",
            "regulation.md",
            "text/markdown",
            256,
            DateTimeOffset.UtcNow,
            null);
        version.MarkPendingOcr(DateTimeOffset.UtcNow);
        context.RegulatoryDocuments.Add(document);
        await context.SaveChangesAsync();

        var consumer = new DocumentOcrIntegrationFaultConsumer(
            context,
            TimeProvider.System,
            NullLogger<DocumentOcrIntegrationFaultConsumer>.Instance);

        await consumer.HandleAsync(
            new DocumentOcrCompletedEvent
            {
                TenantId = tenantId,
                Purpose = DocumentOcrPurpose.RegulatoryCorpus,
                ExternalContextId = version.Id.ToString()
            },
            "Governance policy denied: POLICY_ERROR");

        var savedVersion = await context.RegulatoryDocumentVersions.SingleAsync();
        Assert.Equal(RegulatoryIngestionStatus.Failed, savedVersion.IngestionStatus);
        Assert.Equal("CORPUS_PIPELINE_FAILED", savedVersion.ErrorCode);
        Assert.Contains("POLICY_ERROR", savedVersion.ErrorMessage);
        Assert.NotNull(savedVersion.FailedAt);
    }

    [Fact]
    public async Task Exhausted_completion_does_not_overwrite_completed_version()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, null, ["documents:ingest"]);
        var options = new DbContextOptionsBuilder<RegulatoryComplianceDbContext>()
            .UseInMemoryDatabase(Guid.CreateVersion7().ToString()).Options;
        await using var context = new RegulatoryComplianceDbContext(
            options,
            currentUser,
            new AuditSaveChangesInterceptor(currentUser));
        var document = KnowledgeDocument.CreateTenant(
            tenantId, KnowledgeCategory.Sop, "Completed SOP", "https://docs.example.test/completed", "en", DateTimeOffset.UtcNow);
        var version = document.AddVersion(
            "ocr-knowledge-completed-001", "1.0", new string('a', 64),
            $"tenants/{tenantId}/documents/{Guid.CreateVersion7()}/completed.md",
            "completed.md", "text/markdown", 128, DateTimeOffset.UtcNow);
        version.MarkCompleted(DateTimeOffset.UtcNow);
        context.KnowledgeDocuments.Add(document);
        await context.SaveChangesAsync();

        var consumer = new DocumentOcrIntegrationFaultConsumer(
            context,
            TimeProvider.System,
            NullLogger<DocumentOcrIntegrationFaultConsumer>.Instance);

        await consumer.HandleAsync(
            new DocumentOcrCompletedEvent
            {
                TenantId = tenantId,
                Purpose = DocumentOcrPurpose.KnowledgeCorpus,
                ExternalContextId = version.Id.ToString()
            },
            "late fault");

        var savedVersion = await context.KnowledgeDocumentVersions.SingleAsync();
        Assert.Equal(RegulatoryIngestionStatus.Completed, savedVersion.IngestionStatus);
        Assert.Null(savedVersion.ErrorCode);
    }

    private sealed class CapturingEmbeddingProvider : IEmbeddingProvider
    {
        public EmbeddingModelDescriptor Model { get; } = new("capturing", "1", 4);

        public List<EmbeddingInput> Inputs { get; } = [];

        public Task<IReadOnlyList<float[]>> GenerateAsync(
            IReadOnlyList<EmbeddingInput> inputs,
            CancellationToken cancellationToken = default)
        {
            Inputs.AddRange(inputs);
            return Task.FromResult<IReadOnlyList<float[]>>(
                inputs.Select(_ => new[] { 1f, 0f, 0f, 0f }).ToArray());
        }
    }
}

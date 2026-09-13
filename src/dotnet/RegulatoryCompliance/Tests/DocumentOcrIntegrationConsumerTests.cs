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
}

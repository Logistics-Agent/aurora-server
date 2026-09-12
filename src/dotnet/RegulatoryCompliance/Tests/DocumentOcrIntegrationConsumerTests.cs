using DocumentOcr.Contracts.Events;
using Microsoft.Extensions.Logging.Abstractions;
using RegulatoryCompliance.Application.Embeddings;
using RegulatoryCompliance.Application.Events;
using RegulatoryCompliance.Application.Ingestion;
using RegulatoryCompliance.Infrastructure.Persistences;

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
}

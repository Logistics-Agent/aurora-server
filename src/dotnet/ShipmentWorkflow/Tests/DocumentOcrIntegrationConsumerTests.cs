using DocumentOcr.Contracts.Events;
using Microsoft.Extensions.Logging.Abstractions;
using ShipmentWorkflow.Application.Events;
using ShipmentWorkflow.Infrastructure.Persistences;

namespace ShipmentWorkflow.Tests;

public sealed class DocumentOcrIntegrationConsumerTests
{
    [Fact]
    public async Task Unsupported_failed_version_is_rethrown_for_retry_or_dead_letter()
    {
        var consumer = new DocumentOcrIntegrationConsumer(
            null!,
            NullLogger<DocumentOcrIntegrationConsumer>.Instance);

        await Assert.ThrowsAsync<NotSupportedException>(() => consumer.HandleAsync(
            new DocumentOcrFailedEvent { ContractVersion = 1 }));
    }

    [Fact]
    public async Task Non_shipment_purpose_is_not_applied_to_shipment_projection()
    {
        var consumer = new DocumentOcrIntegrationConsumer(
            null!,
            NullLogger<DocumentOcrIntegrationConsumer>.Instance);

        await consumer.HandleAsync(new DocumentOcrCompletedEvent
        {
            Purpose = DocumentOcrPurpose.KnowledgeCorpus,
            ExternalContextId = "not-used-by-shipment-consumer"
        });
    }
}

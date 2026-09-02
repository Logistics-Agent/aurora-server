using MassTransit;
using Shipment.Contracts.Events;

namespace Notification.Infrastructure.Messaging.Consumers;

public sealed class DocumentAttachedConsumer(NotificationEventProcessor processor) : IConsumer<DocumentAttachedEvent>
{
    public Task Consume(ConsumeContext<DocumentAttachedEvent> context) => processor.ProcessAsync(
        context.Message.EventId, context.Message.TenantId, context.Message.ShipmentId,
        "SHIPMENT_DOCUMENT_ATTACHED", "Shipment document attached",
        $"Document {context.Message.DocumentType} was attached to shipment {context.Message.FileName}.",
        null, context.Message.AttachedAt, context.CancellationToken);
}

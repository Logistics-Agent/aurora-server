using Shared.Entity;

namespace DocumentOcr.Domain.Entities;

public sealed class InboxMessage : TenantAuditableEntity
{
    private InboxMessage() { }

    public Guid SourceEventId { get; private set; }
    public string SourceEventType { get; private set; } = string.Empty;
    public int ContractVersion { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }

    public static InboxMessage Create(
        Guid tenantId,
        Guid sourceEventId,
        string sourceEventType,
        int contractVersion,
        DateTimeOffset receivedAt)
    {
        DocumentOcrValidation.RequiredId(tenantId, nameof(tenantId));
        DocumentOcrValidation.RequiredId(sourceEventId, nameof(sourceEventId));
        if (contractVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(contractVersion));
        if (receivedAt == default)
            throw new ArgumentException("ReceivedAt is required.", nameof(receivedAt));

        return new InboxMessage
        {
            TenantId = tenantId,
            SourceEventId = sourceEventId,
            SourceEventType = DocumentOcrValidation.RequiredText(sourceEventType, nameof(sourceEventType), 256),
            ContractVersion = contractVersion,
            ReceivedAt = receivedAt,
            CreatedAt = receivedAt
        };
    }
}

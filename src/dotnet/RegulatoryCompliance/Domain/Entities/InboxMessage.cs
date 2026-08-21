using Shared.Entity;

namespace RegulatoryCompliance.Domain.Entities;

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
        ComplianceValidation.RequiredId(tenantId, nameof(tenantId));
        ComplianceValidation.RequiredId(sourceEventId, nameof(sourceEventId));
        if (contractVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(contractVersion));
        ComplianceValidation.RequiredTimestamp(receivedAt, nameof(receivedAt));

        return new InboxMessage
        {
            TenantId = tenantId,
            SourceEventId = sourceEventId,
            SourceEventType = ComplianceValidation.RequiredText(
                sourceEventType, nameof(sourceEventType), 256),
            ContractVersion = contractVersion,
            ReceivedAt = receivedAt,
            CreatedAt = receivedAt
        };
    }
}

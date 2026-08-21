using Shared.Entity;

namespace GpsTracking.Domain.Entities;

public sealed class ConsumedIntegrationEvent : TenantAuditableEntity
{
    private ConsumedIntegrationEvent() { }

    public Guid SourceEventId { get; private set; }
    public string SourceEventType { get; private set; } = string.Empty;
    public int ContractVersion { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }

    public static ConsumedIntegrationEvent Create(
        Guid tenantId, Guid sourceEventId, string sourceEventType,
        int contractVersion, DateTimeOffset receivedAt)
    {
        GpsDomainValidation.RequiredId(tenantId, nameof(tenantId));
        GpsDomainValidation.RequiredId(sourceEventId, nameof(sourceEventId));
        if (contractVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(contractVersion));
        if (receivedAt == default)
            throw new ArgumentException("ReceivedAt is required.", nameof(receivedAt));
        return new ConsumedIntegrationEvent
        {
            TenantId = tenantId,
            SourceEventId = sourceEventId,
            SourceEventType = GpsDomainValidation.RequiredText(sourceEventType, nameof(sourceEventType), 256),
            ContractVersion = contractVersion,
            ReceivedAt = receivedAt,
            CreatedAt = receivedAt
        };
    }
}

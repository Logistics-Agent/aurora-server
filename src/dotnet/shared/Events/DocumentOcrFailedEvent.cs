namespace DocumentOcr.Contracts.Events;

public sealed record DocumentOcrFailedEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
    public int ContractVersion { get; init; } = 1;
    public Guid TenantId { get; init; }
    public Guid JobId { get; init; }
    public Guid? ExternalDocumentId { get; init; }
    public Guid? ExternalShipmentId { get; init; }
    public string? ExternalContextId { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; }
}

namespace DocumentOcr.Contracts.Events;

public sealed record DocumentOcrFailedEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
    public int ContractVersion { get; init; } = 2;
    public Guid TenantId { get; init; }
    public Guid JobId { get; init; }
    public Guid? ShipmentId { get; init; }
    public Guid? DocumentId { get; init; }
    public Guid CorrelationId { get; init; }
    public string Purpose { get; init; } = "SHIPMENT_DOCUMENT";
    public Guid? ExternalDocumentId { get; init; }
    public Guid? ExternalShipmentId { get; init; }
    public string? ExternalContextId { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public decimal Confidence { get; init; }
    public string? NormalizedJsonHash { get; init; }
    public string? ArtifactReference { get; init; }
    public string ReviewState { get; init; } = "FAILED";
    public DateTimeOffset OccurredAt { get; init; }
}

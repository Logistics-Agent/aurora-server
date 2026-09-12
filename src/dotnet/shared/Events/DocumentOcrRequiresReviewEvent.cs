namespace DocumentOcr.Contracts.Events;

public sealed record DocumentOcrRequiresReviewEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
    public int ContractVersion { get; init; } = 1;
    public Guid TenantId { get; init; }
    public Guid JobId { get; init; }
    public Guid? ShipmentId { get; init; }
    public Guid? DocumentId { get; init; }
    public Guid CorrelationId { get; init; }
    public DocumentOcrPurpose Purpose { get; init; } = DocumentOcrPurpose.ShipmentDocument;
    public Guid? ExternalDocumentId { get; init; }
    public Guid? ExternalShipmentId { get; init; }
    public string? ExternalContextId { get; init; }
    public string DetectedDocumentType { get; init; } = string.Empty;
    public string? NormalizedJsonHash { get; init; }
    public string? ArtifactReference { get; init; }
    public decimal Confidence { get; init; }
    public string ReviewState { get; init; } = "REQUIRES_REVIEW";
    public DateTimeOffset OccurredAt { get; init; }
}

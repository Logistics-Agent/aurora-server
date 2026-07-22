namespace DocumentOcr.Contracts.Events;

public sealed record DocumentOcrCompletedEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
    public int ContractVersion { get; init; } = 1;
    public Guid TenantId { get; init; }
    public Guid JobId { get; init; }
    public Guid? ExternalDocumentId { get; init; }
    public Guid? ExternalShipmentId { get; init; }
    public string DetectedDocumentType { get; init; } = string.Empty;
    public string NormalizedJson { get; init; } = "{}";
    public decimal Confidence { get; init; }
    public bool NeedsReview { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
}

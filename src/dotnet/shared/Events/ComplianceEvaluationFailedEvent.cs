namespace RegulatoryCompliance.Contracts.Events;

public sealed record ComplianceEvaluationFailedEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
    public int ContractVersion { get; init; } = 1;
    public Guid TenantId { get; init; }
    public Guid EvaluationId { get; init; }
    public Guid ExternalShipmentId { get; init; }
    public IReadOnlyList<Guid> ExternalDocumentIds { get; init; } = [];
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; }
}

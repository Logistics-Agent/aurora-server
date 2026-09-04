namespace RegulatoryCompliance.Contracts.Events;

public sealed record ComplianceEvaluationCompletedEvent
{
    public Guid EventId { get; init; } = Guid.CreateVersion7();
    public int ContractVersion { get; init; } = 1;
    public Guid TenantId { get; init; }
    public Guid EvaluationId { get; init; }
    public Guid ExternalShipmentId { get; init; }
    public IReadOnlyList<Guid> ExternalDocumentIds { get; init; } = [];
    public string RiskLevel { get; init; } = string.Empty;
    public string EvidenceSufficiency { get; init; } = string.Empty;
    public decimal ComplianceConfidence { get; init; }
    public int ViolationCount { get; init; }
    public IReadOnlyList<string> MissingDocuments { get; init; } = [];
    public string Summary { get; init; } = string.Empty;
    public DateTimeOffset OccurredAt { get; init; }
}

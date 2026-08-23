namespace RegulatoryCompliance.Application.Assistant;

public enum GroundedEvidenceDomain
{
    Regulatory,
    Knowledge
}

public sealed record GroundedEvidence(
    string EvidenceId,
    GroundedEvidenceDomain Domain,
    Guid SourceId,
    Guid DocumentVersionId,
    Guid ChunkId,
    string Title,
    string? SectionLabel,
    string? PageLabel,
    string Excerpt,
    decimal RelevanceScore,
    string? Authority = null,
    string? JurisdictionCode = null,
    string? RegulationType = null,
    string? CanonicalSourceUri = null,
    string? KnowledgeCategory = null,
    string? SourceReference = null);

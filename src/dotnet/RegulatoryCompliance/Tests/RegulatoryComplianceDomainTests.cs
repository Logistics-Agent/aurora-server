using RegulatoryCompliance.Domain.Entities;
using RegulatoryCompliance.Domain.Enums;

namespace RegulatoryCompliance.Tests;

public sealed class RegulatoryComplianceDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SourceVersioningPreservesScopeAndImmutableChunkIdentity()
    {
        var tenantId = Guid.CreateVersion7();
        var document = CreateTenantDocument(tenantId);
        var version = AddVersion(document);

        version.StartIngestion(Now.AddMinutes(1));
        var chunk = version.AddChunk(
            1, "Section 4", "12", "Dangerous goods require a declaration.",
            7, Hash('b'), Now.AddMinutes(2));
        version.CompleteIngestion(Now.AddMinutes(3));

        Assert.Equal(tenantId, document.ScopeKey);
        Assert.Equal(SourceVisibility.Tenant, version.Visibility);
        Assert.Equal(version.Id, chunk.RegulatoryDocumentVersionId);
        Assert.Equal(1, chunk.Sequence);
        Assert.Equal(chunk.NormalizedText.Length, chunk.CharacterCount);
        Assert.Equal(RegulatoryIngestionStatus.Completed, version.IngestionStatus);
        Assert.Equal(1, version.ChunkCount);
    }

    [Fact]
    public void SourceRejectsDuplicateVersionsAndNonDeterministicChunkSequence()
    {
        var document = CreateTenantDocument(Guid.CreateVersion7());
        var version = AddVersion(document);

        Assert.Throws<InvalidOperationException>(() => AddVersion(document));

        version.StartIngestion(Now.AddMinutes(1));
        Assert.Throws<ArgumentOutOfRangeException>(() => version.AddChunk(
            2, null, null, "Out of sequence.", 3, Hash('c'), Now.AddMinutes(2)));
        Assert.Throws<InvalidOperationException>(() => version.CompleteIngestion(Now.AddMinutes(3)));
    }

    [Fact]
    public void NewVersionRecordsExplicitSupersessionMetadata()
    {
        var document = CreateTenantDocument(Guid.CreateVersion7());
        var original = AddVersion(document);

        var replacement = document.AddVersion(
            "2026.2",
            Now.AddDays(-1),
            Now,
            null,
            Hash('e'),
            "regulations/vn/dangerous-goods-2026-2.pdf",
            "dangerous-goods-2026-2.pdf",
            "application/pdf",
            4_500,
            Now.AddHours(1),
            original.Id);

        Assert.Equal(original.Id, replacement.SupersedesVersionId);
        Assert.Equal(Now.AddHours(1), original.SupersededAt);
    }

    [Fact]
    public void EvaluationRequiresValidTransitionConfidenceAndCitations()
    {
        var evaluation = CreateEvaluation();
        evaluation.Start(Now.AddMinutes(1));
        var finding = evaluation.AddFinding(
            ComplianceFindingType.Requirement,
            "DOC-001",
            "Documents",
            "Declaration required",
            "A dangerous-goods declaration is required.",
            ComplianceRiskLevel.High,
            Now.AddMinutes(2));

        Assert.Throws<InvalidOperationException>(() => evaluation.Complete(
            ComplianceRiskLevel.High, EvidenceSufficiency.Sufficient, 0.9m, [], [], Now.AddMinutes(3)));

        finding.AddCitation(
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            "Customs Authority",
            "Dangerous Goods Rule",
            "https://regulations.example/rule",
            "2026.1",
            "Section 4",
            "12",
            Now.AddYears(-1),
            null,
            "Dangerous goods require a declaration.",
            0.95m,
            Now.AddMinutes(2));

        Assert.Throws<ArgumentOutOfRangeException>(() => evaluation.Complete(
            ComplianceRiskLevel.High, EvidenceSufficiency.Sufficient, 1.1m, [], [], Now.AddMinutes(3)));

        evaluation.Complete(
            ComplianceRiskLevel.High,
            EvidenceSufficiency.Sufficient,
            0.9m,
            ["Cargo classification was supplied by Shipment Workflow."],
            ["Safety data sheet"],
            Now.AddMinutes(3));

        Assert.Equal(ComplianceEvaluationStatus.Completed, evaluation.Status);
        Assert.Equal(0.9m, evaluation.Confidence);
        Assert.Single(evaluation.Findings);
        Assert.Single(finding.Citations);
        Assert.Throws<InvalidOperationException>(() => evaluation.Start(Now.AddMinutes(4)));
    }

    [Fact]
    public void CitationRejectsMissingEvidenceIdentityAndInvalidScore()
    {
        var evaluation = CreateEvaluation();
        evaluation.Start(Now.AddMinutes(1));
        var finding = evaluation.AddFinding(
            ComplianceFindingType.Warning,
            "WARN-001",
            "Evidence",
            "Evidence warning",
            "Evidence is incomplete.",
            ComplianceRiskLevel.Unknown,
            Now.AddMinutes(2));

        Assert.Throws<ArgumentException>(() => finding.AddCitation(
            Guid.Empty, Guid.CreateVersion7(), Guid.CreateVersion7(),
            "Authority", "Title", "https://regulations.example/source", "v1",
            null, null, Now, null, "Excerpt", 0.5m, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => finding.AddCitation(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(),
            "Authority", "Title", "https://regulations.example/source", "v1",
            null, null, Now, null, "Excerpt", -0.1m, Now));
    }

    [Fact]
    public void InvalidEvaluationTransitionsAreRejected()
    {
        var evaluation = CreateEvaluation();

        Assert.Throws<InvalidOperationException>(() => evaluation.AddFinding(
            ComplianceFindingType.Warning, "WARN", "State", "Invalid", "Not started.",
            ComplianceRiskLevel.Low, Now));
        Assert.Throws<InvalidOperationException>(() => evaluation.Fail("ERR", "Not started.", Now));

        evaluation.Start(Now.AddMinutes(1));
        evaluation.Fail("PROVIDER_ERROR", "Provider failed.", Now.AddMinutes(2));

        Assert.Equal(ComplianceEvaluationStatus.Failed, evaluation.Status);
        Assert.Equal(ComplianceRiskLevel.Unknown, evaluation.RiskLevel);
        Assert.Equal(EvidenceSufficiency.Insufficient, evaluation.EvidenceSufficiency);
        Assert.Throws<InvalidOperationException>(() => evaluation.Start(Now.AddMinutes(3)));
    }

    private static RegulatoryDocument CreateTenantDocument(Guid tenantId) =>
        RegulatoryDocument.CreateTenant(
            tenantId,
            "Customs Authority",
            "Dangerous Goods Rule",
            "https://regulations.example/rule",
            "VN",
            RegulationType.DangerousGoods,
            "en",
            Now);

    private static RegulatoryDocumentVersion AddVersion(RegulatoryDocument document) =>
        document.AddVersion(
            "2026.1",
            Now.AddDays(-30),
            Now.AddDays(-1),
            null,
            Hash('a'),
            "regulations/vn/dangerous-goods-2026.pdf",
            "dangerous-goods.pdf",
            "application/pdf",
            4_096,
            Now);

    private static ComplianceEvaluation CreateEvaluation() =>
        ComplianceEvaluation.Create(
            Guid.CreateVersion7(),
            "evaluation-001",
            Guid.CreateVersion7(),
            Hash('d'),
            "{\"cargo\":[]}",
            Now,
            Now);

    private static string Hash(char value) => new(value, 64);
}

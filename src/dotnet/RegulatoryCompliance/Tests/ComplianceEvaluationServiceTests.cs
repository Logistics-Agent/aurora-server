using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RegulatoryCompliance.Application.Evaluations;
using RegulatoryCompliance.Application.Retrieval;
using RegulatoryCompliance.Contracts.Events;
using RegulatoryCompliance.Domain.Entities;
using RegulatoryCompliance.Domain.Enums;
using RegulatoryCompliance.GrpcServices;
using RegulatoryCompliance.Infrastructure.Persistences;
using Shared.Interceptors;
using Shared.Security;

namespace RegulatoryCompliance.Tests;

public sealed class ComplianceEvaluationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SufficientEvaluationPersistsCitedFindingsTracesAndCompletedOutbox()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = CreateContext(currentUser);
        var retrieval = new FakeRetrievalService(context, tenantId, EvidenceSufficiency.Sufficient);
        var service = CreateService(context, currentUser, retrieval);

        var evaluation = await service.EvaluateAsync(Input());

        Assert.Equal(ComplianceEvaluationStatus.Completed, evaluation.Status);
        Assert.Equal(ComplianceRiskLevel.Low, evaluation.RiskLevel);
        Assert.True(evaluation.Confidence > 0m);
        Assert.NotEmpty(evaluation.Findings);
        Assert.All(evaluation.Findings, finding => Assert.NotEmpty(finding.Citations));
        Assert.NotEmpty(evaluation.RetrievalTraces);
        Assert.Equal(
            new[]
            {
                RegulationType.ImportRestriction,
                RegulationType.ExportRestriction,
                RegulationType.RequiredDocument,
                RegulationType.TransportMode,
                RegulationType.Customs
            }.Order(),
            retrieval.RequestedTypes.Distinct().Order());
        var outbox = await context.OutboxMessages.SingleAsync();
        Assert.Equal(typeof(ComplianceEvaluationCompletedEvent).FullName, outbox.EventType);
        Assert.Contains(evaluation.Id.ToString(), outbox.Content);

        var mapped = RegulatoryComplianceGrpcService.MapEvaluation(evaluation);
        Assert.Equal(evaluation.Id.ToString(), mapped.EvaluationId);
        Assert.Equal(evaluation.Findings.Count, mapped.Findings.Count);
        Assert.All(mapped.Findings, finding => Assert.NotEmpty(finding.Citations));
    }

    [Fact]
    public async Task ReplayIsIdempotentAndChangedPayloadIsRejected()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = CreateContext(currentUser);
        var retrieval = new FakeRetrievalService(context, tenantId, EvidenceSufficiency.Sufficient);
        var service = CreateService(context, currentUser, retrieval);
        var input = Input();

        var first = await service.EvaluateAsync(input);
        var replay = await service.EvaluateAsync(input);

        Assert.Equal(first.Id, replay.Id);
        Assert.Single(await context.OutboxMessages.ToListAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.EvaluateAsync(input with { TransportMode = "Air" }));
    }

    [Theory]
    [InlineData(EvidenceSufficiency.Insufficient)]
    [InlineData(EvidenceSufficiency.Conflicting)]
    public async Task WeakEvidenceProducesUnknownRiskAndManualReview(
        EvidenceSufficiency sufficiency)
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = CreateContext(currentUser);
        var service = CreateService(
            context, currentUser, new FakeRetrievalService(context, tenantId, sufficiency));

        var evaluation = await service.EvaluateAsync(Input());

        Assert.Equal(ComplianceEvaluationStatus.Completed, evaluation.Status);
        Assert.Equal(ComplianceRiskLevel.Unknown, evaluation.RiskLevel);
        Assert.Equal(sufficiency, evaluation.EvidenceSufficiency);
        Assert.Contains("manual review", evaluation.AssumptionsJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DangerousCargoMissingDocumentsProducesHighRiskWithCitedWarning()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = CreateContext(currentUser);
        var retrieval = new FakeRetrievalService(
            context, tenantId, EvidenceSufficiency.Sufficient);
        var service = CreateService(
            context,
            currentUser,
            retrieval);
        var dangerousCargo = new CargoEvaluationSnapshot(
            "Lithium batteries", "850760", 10, "boxes", 120m, 1.2m, true, "UN3480", "box");

        var evaluation = await service.EvaluateAsync(Input() with
        {
            Cargo = [dangerousCargo],
            Documents = []
        });

        Assert.Equal(ComplianceRiskLevel.High, evaluation.RiskLevel);
        Assert.Contains("DangerousGoodsDeclaration", evaluation.MissingDocumentsJson);
        var missing = Assert.Single(
            evaluation.Findings, finding => finding.Code == "MISSING-DOCUMENTS");
        Assert.NotEmpty(missing.Citations);
        Assert.Contains(RegulationType.DangerousGoods, retrieval.RequestedTypes);
    }

    [Fact]
    public async Task RetrievalFailurePersistsFailedEvaluationAndFailureOutbox()
    {
        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = CreateContext(currentUser);
        var service = CreateService(context, currentUser, new FailingRetrievalService());

        var evaluation = await service.EvaluateAsync(Input());

        Assert.Equal(ComplianceEvaluationStatus.Failed, evaluation.Status);
        Assert.Equal(ComplianceRiskLevel.Unknown, evaluation.RiskLevel);
        var outbox = await context.OutboxMessages.SingleAsync();
        Assert.Equal(typeof(ComplianceEvaluationFailedEvent).FullName, outbox.EventType);
        Assert.Contains("EVALUATION_FAILED", outbox.Content);
    }

    [Fact]
    public async Task TenantAndSnapshotValidationAreEnforced()
    {
        await using var missingContext = CreateContext(new CurrentUserService());
        var missingTenantService = CreateService(
            missingContext, new CurrentUserService(), new FailingRetrievalService());
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            missingTenantService.EvaluateAsync(Input()));

        var tenantId = Guid.CreateVersion7();
        var currentUser = CurrentUser(tenantId);
        await using var context = CreateContext(currentUser);
        var service = CreateService(context, currentUser, new FailingRetrievalService());
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.EvaluateAsync(Input() with { Cargo = [] }));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.EvaluateAsync(Input() with
            {
                Documents =
                [
                    new OcrEvaluationSnapshot(
                        Guid.CreateVersion7(), "CommercialInvoice", "not-json", 0.9m, false)
                ]
            }));
    }

    [Fact]
    public async Task GetEvaluationDoesNotLeakAcrossTenants()
    {
        var databaseName = $"compliance-evaluation-tenant-{Guid.CreateVersion7()}";
        var tenantA = Guid.CreateVersion7();
        var tenantAUser = CurrentUser(tenantA);
        Guid evaluationId;
        await using (var context = CreateContext(tenantAUser, databaseName))
        {
            var evaluation = await CreateService(
                context,
                tenantAUser,
                new FakeRetrievalService(context, tenantA, EvidenceSufficiency.Sufficient))
                .EvaluateAsync(Input());
            evaluationId = evaluation.Id;
        }

        var tenantBUser = CurrentUser(Guid.CreateVersion7());
        await using var tenantBContext = CreateContext(tenantBUser, databaseName);
        var tenantBService = CreateService(
            tenantBContext, tenantBUser, new FailingRetrievalService());

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            tenantBService.GetAsync(evaluationId));
    }

    private static ComplianceEvaluationService CreateService(
        RegulatoryComplianceDbContext context,
        ICurrentUserService currentUser,
        IRegulationRetrievalService retrieval) =>
        new(context, retrieval, currentUser, new FixedTimeProvider(Now));

    private static ComplianceEvaluationInput Input() =>
        new(
            "evaluation-001",
            Guid.Parse("01900000-0000-7000-8000-000000000001"),
            [new CargoEvaluationSnapshot("Machinery", "847989", 1, "unit", 500m, 2m, false, null, "crate")],
            "VN",
            "SG",
            ["VN"],
            "Sea",
            [
                Document("CommercialInvoice"),
                Document("PackingList")
            ],
            Now);

    private static OcrEvaluationSnapshot Document(string type) =>
        new(Guid.CreateVersion7(), type, "{\"fields\":{}}", 0.95m, false);

    private static RegulatoryComplianceDbContext CreateContext(
        CurrentUserService currentUser,
        string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<RegulatoryComplianceDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.CreateVersion7().ToString())
            .Options;
        return new RegulatoryComplianceDbContext(
            options, currentUser, new AuditSaveChangesInterceptor(currentUser));
    }

    private static CurrentUserService CurrentUser(Guid tenantId)
    {
        var currentUser = new CurrentUserService();
        currentUser.Populate(Guid.CreateVersion7(), tenantId, null, null, [], []);
        return currentUser;
    }

    private sealed class FakeRetrievalService(
        RegulatoryComplianceDbContext context,
        Guid tenantId,
        EvidenceSufficiency sufficiency) : IRegulationRetrievalService
    {
        public List<RegulationType> RequestedTypes { get; } = [];

        public Task<RegulationQueryResult> QueryAsync(
            RegulationQueryInput input,
            CancellationToken cancellationToken = default)
        {
            RequestedTypes.Add(input.RegulationTypes.Single());
            var trace = RetrievalTrace.Create(
                tenantId,
                null,
                new string('a', 64),
                input.JurisdictionCode,
                input.EffectiveAt,
                input.LanguageCode,
                JsonSerializer.Serialize(input.RegulationTypes),
                "fake:1",
                input.TopK,
                input.MinimumRelevanceScore,
                "[]",
                "[]",
                sufficiency,
                Now);
            context.RetrievalTraces.Add(trace);
            var evidence = sufficiency == EvidenceSufficiency.Insufficient
                ? []
                : new[]
                {
                    new RegulationEvidenceResult(
                        Guid.CreateVersion7(),
                        Guid.CreateVersion7(),
                        Guid.CreateVersion7(),
                        input.RegulationTypes.Single(),
                        input.JurisdictionCode,
                        input.LanguageCode,
                        "Authority",
                        $"Rule {input.RegulationTypes.Single()}",
                        "https://regulations.example/rule",
                        "1",
                        "Article 1",
                        "1",
                        Now.AddYears(-1),
                        null,
                        "Applicable regulatory evidence.",
                        0.9m)
                };
            return Task.FromResult(new RegulationQueryResult(
                trace.Id, sufficiency, evidence, "Evidence-backed test result."));
        }
    }

    private sealed class FailingRetrievalService : IRegulationRetrievalService
    {
        public Task<RegulationQueryResult> QueryAsync(
            RegulationQueryInput input,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Retrieval provider unavailable.");
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}

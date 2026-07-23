using System.Security.Cryptography;
using System.Text;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using RegulatoryCompliance.Application.Evaluations;
using RegulatoryCompliance.Application.Ingestion;
using RegulatoryCompliance.Application.Retrieval;
using RegulatoryCompliance.Domain.Entities;
using RegulatoryCompliance.Domain.Enums;
using RegulatoryCompliance.GrpcServices;
using ComplianceGrpc = RegulatoryCompliance.Grpc;

namespace RegulatoryCompliance.Tests.Grpc;

public sealed class RegulatoryComplianceGrpcServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 23, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task IngestMapsControlledContentAndVisibility()
    {
        var fake = new FakeIngestionService();
        var service = Service(ingestion: fake);
        var content = Encoding.UTF8.GetBytes("# Rule\nCargo declaration required.");

        var response = await service.IngestRegulatorySource(
            new ComplianceGrpc.IngestRegulatorySourceRequest
            {
                IdempotencyKey = "grpc-ingestion",
                Authority = "Authority",
                Title = "Rule",
                CanonicalSourceUri = "https://regulations.example/rule",
                JurisdictionCode = "VN",
                RegulationType = ComplianceGrpc.RegulationType.Customs,
                LanguageCode = "en",
                VersionLabel = "1",
                PublishedAt = Timestamp.FromDateTimeOffset(Now.AddDays(-2)),
                EffectiveFrom = Timestamp.FromDateTimeOffset(Now.AddDays(-1)),
                ContentReference = "regulatory/vn/rule.md",
                FileName = "rule.md",
                MimeType = "text/markdown",
                SizeBytes = content.Length,
                ContentSha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
                Content = ByteString.CopyFrom(content),
                Visibility = ComplianceGrpc.RegulatorySourceVisibility.Tenant
            },
            TestServerCallContext.Create());

        Assert.Equal(SourceVisibility.Tenant, fake.LastInput!.Visibility);
        Assert.Equal(RegulationType.Customs, fake.LastInput.RegulationType);
        Assert.Equal(content, fake.LastInput.Content.ToArray());
        Assert.Equal(fake.Result.DocumentVersionId.ToString(), response.DocumentVersionId);
        Assert.Equal(ComplianceGrpc.RegulatoryIngestionStatus.Completed, response.Status);
    }

    [Fact]
    public async Task QueryMapsFiltersAndCompleteCitation()
    {
        var fake = new FakeRetrievalService();
        var service = Service(retrieval: fake);

        var response = await service.QueryRegulations(
            new ComplianceGrpc.QueryRegulationsRequest
            {
                Query = "customs requirements",
                JurisdictionCode = "VN",
                EffectiveAt = Timestamp.FromDateTimeOffset(Now),
                LanguageCode = "en",
                TopK = 5,
                MinimumRelevanceScore = 0.4,
                RegulationTypes = { ComplianceGrpc.RegulationType.Customs }
            },
            TestServerCallContext.Create());

        Assert.Equal(5, fake.LastInput!.TopK);
        Assert.Equal(0.4m, fake.LastInput.MinimumRelevanceScore);
        var evidence = Assert.Single(response.Evidence);
        Assert.Equal("Authority", evidence.Citation.Authority);
        Assert.Equal("Rule 4", evidence.Citation.SectionLabel);
        Assert.Equal(ComplianceGrpc.RegulationType.Customs, evidence.RegulationType);
    }

    [Fact]
    public async Task EvaluateAndGetMapSnapshotsAndEvaluationResponse()
    {
        var fake = new FakeEvaluationService();
        var service = Service(evaluation: fake);
        var shipmentId = Guid.CreateVersion7();
        var documentId = Guid.CreateVersion7();
        var request = new ComplianceGrpc.EvaluateComplianceRequest
        {
            IdempotencyKey = "grpc-evaluation",
            ExternalShipmentId = shipmentId.ToString(),
            OriginCountryCode = "VN",
            DestinationCountryCode = "SG",
            TransportMode = "Sea",
            EffectiveAt = Timestamp.FromDateTimeOffset(Now),
            Cargo =
            {
                new ComplianceGrpc.CargoSnapshot
                {
                    Name = "Machinery",
                    HsCode = "847989",
                    Quantity = 1,
                    Unit = "unit",
                    WeightKg = 500,
                    VolumeM3 = 2
                }
            },
            Documents =
            {
                new ComplianceGrpc.OcrDocumentSnapshot
                {
                    ExternalDocumentId = documentId.ToString(),
                    DocumentType = "CommercialInvoice",
                    NormalizedJson = "{}",
                    ExtractionConfidence = 0.95
                }
            },
            JurisdictionCodes = { "VN" }
        };

        var evaluated = await service.EvaluateCompliance(request, TestServerCallContext.Create());
        var fetched = await service.GetComplianceEvaluation(
            new ComplianceGrpc.GetComplianceEvaluationRequest
            {
                EvaluationId = fake.Evaluation.Id.ToString()
            },
            TestServerCallContext.Create());

        Assert.Equal(shipmentId, fake.LastInput!.ExternalShipmentId);
        Assert.Equal(documentId, Assert.Single(fake.LastInput.Documents).ExternalDocumentId);
        Assert.Equal("847989", Assert.Single(fake.LastInput.Cargo).HsCode);
        Assert.Equal(fake.Evaluation.Id.ToString(), evaluated.EvaluationId);
        Assert.Equal(fake.Evaluation.Id, fake.LastGetId);
        Assert.Equal(evaluated, fetched);
    }

    [Fact]
    public async Task MissingTenantErrorsAreMappedToUnauthenticated()
    {
        var service = Service(
            ingestion: new ThrowingIngestionService(),
            retrieval: new ThrowingRetrievalService(),
            evaluation: new ThrowingEvaluationService());

        var evaluate = await Assert.ThrowsAsync<RpcException>(() => service.EvaluateCompliance(
            new ComplianceGrpc.EvaluateComplianceRequest
            {
                ExternalShipmentId = Guid.CreateVersion7().ToString(),
                EffectiveAt = Timestamp.FromDateTimeOffset(Now)
            }, TestServerCallContext.Create()));
        var query = await Assert.ThrowsAsync<RpcException>(() => service.QueryRegulations(
            new ComplianceGrpc.QueryRegulationsRequest
            {
                EffectiveAt = Timestamp.FromDateTimeOffset(Now)
            }, TestServerCallContext.Create()));
        var ingest = await Assert.ThrowsAsync<RpcException>(() => service.IngestRegulatorySource(
            new ComplianceGrpc.IngestRegulatorySourceRequest
            {
                PublishedAt = Timestamp.FromDateTimeOffset(Now),
                EffectiveFrom = Timestamp.FromDateTimeOffset(Now),
                RegulationType = ComplianceGrpc.RegulationType.Customs,
                Visibility = ComplianceGrpc.RegulatorySourceVisibility.Tenant
            }, TestServerCallContext.Create()));
        var get = await Assert.ThrowsAsync<RpcException>(() => service.GetComplianceEvaluation(
            new ComplianceGrpc.GetComplianceEvaluationRequest
            {
                EvaluationId = Guid.CreateVersion7().ToString()
            }, TestServerCallContext.Create()));

        Assert.All([evaluate, query, ingest, get], exception =>
            Assert.Equal(StatusCode.Unauthenticated, exception.StatusCode));
    }

    private static RegulatoryComplianceGrpcService Service(
        IRegulatoryIngestionService? ingestion = null,
        IRegulationRetrievalService? retrieval = null,
        IComplianceEvaluationService? evaluation = null) =>
        new(
            ingestion ?? new FakeIngestionService(),
            retrieval ?? new FakeRetrievalService(),
            evaluation ?? new FakeEvaluationService());

    private sealed class FakeIngestionService : IRegulatoryIngestionService
    {
        public RegulatoryIngestionInput? LastInput { get; private set; }
        public RegulatoryIngestionResult Result { get; } = new(
            Guid.CreateVersion7(), Guid.CreateVersion7(), RegulatoryIngestionStatus.Completed, 2, false, Now);

        public Task<RegulatoryIngestionResult> IngestAsync(
            RegulatoryIngestionInput input,
            CancellationToken cancellationToken = default)
        {
            LastInput = input;
            return Task.FromResult(Result);
        }
    }

    private sealed class FakeRetrievalService : IRegulationRetrievalService
    {
        public RegulationQueryInput? LastInput { get; private set; }

        public Task<RegulationQueryResult> QueryAsync(
            RegulationQueryInput input,
            CancellationToken cancellationToken = default)
        {
            LastInput = input;
            return Task.FromResult(new RegulationQueryResult(
                Guid.CreateVersion7(),
                EvidenceSufficiency.Sufficient,
                [new RegulationEvidenceResult(
                    Guid.CreateVersion7(),
                    Guid.CreateVersion7(),
                    Guid.CreateVersion7(),
                    RegulationType.Customs,
                    "VN",
                    "en",
                    "Authority",
                    "Customs Rule",
                    "https://regulations.example/customs",
                    "1",
                    "Rule 4",
                    "12",
                    Now.AddDays(-1),
                    null,
                    "Declaration is required.",
                    0.9m)],
                "Retrieved one passage."));
        }
    }

    private sealed class FakeEvaluationService : IComplianceEvaluationService
    {
        public ComplianceEvaluationInput? LastInput { get; private set; }
        public Guid LastGetId { get; private set; }
        public ComplianceEvaluation Evaluation { get; } = CreateEvaluation();

        public Task<ComplianceEvaluation> EvaluateAsync(
            ComplianceEvaluationInput input,
            CancellationToken cancellationToken = default)
        {
            LastInput = input;
            return Task.FromResult(Evaluation);
        }

        public Task<ComplianceEvaluation> GetAsync(
            Guid evaluationId,
            CancellationToken cancellationToken = default)
        {
            LastGetId = evaluationId;
            return Task.FromResult(Evaluation);
        }

        private static ComplianceEvaluation CreateEvaluation()
        {
            var evaluation = ComplianceEvaluation.Create(
                Guid.CreateVersion7(),
                "grpc-evaluation",
                Guid.CreateVersion7(),
                new string('a', 64),
                "{}",
                Now,
                Now);
            evaluation.Start(Now);
            evaluation.Complete(
                ComplianceRiskLevel.Low,
                EvidenceSufficiency.Sufficient,
                0.9m,
                [],
                [],
                Now);
            return evaluation;
        }
    }

    private sealed class ThrowingIngestionService : IRegulatoryIngestionService
    {
        public Task<RegulatoryIngestionResult> IngestAsync(
            RegulatoryIngestionInput input,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Tenant context is required.");
    }

    private sealed class ThrowingRetrievalService : IRegulationRetrievalService
    {
        public Task<RegulationQueryResult> QueryAsync(
            RegulationQueryInput input,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Tenant context is required.");
    }

    private sealed class ThrowingEvaluationService : IComplianceEvaluationService
    {
        public Task<ComplianceEvaluation> EvaluateAsync(
            ComplianceEvaluationInput input,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Tenant context is required.");

        public Task<ComplianceEvaluation> GetAsync(
            Guid evaluationId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Tenant context is required.");
    }
}

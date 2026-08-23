using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using RegulatoryCompliance.Application.Ingestion;
using RegulatoryCompliance.Application.Retrieval;
using RegulatoryCompliance.Application.Evaluations;
using RegulatoryCompliance.Domain.Entities;
using System.Text.Json;
using RegulatoryCompliance.Application.Assistant;
using ComplianceGrpc = RegulatoryCompliance.Grpc;
using DomainRegulationType = RegulatoryCompliance.Domain.Enums.RegulationType;
using DomainVisibility = RegulatoryCompliance.Domain.Enums.SourceVisibility;

namespace RegulatoryCompliance.GrpcServices;

public sealed class RegulatoryComplianceGrpcService(
    IRegulatoryIngestionService ingestionService,
    IKnowledgeIngestionService knowledgeIngestionService,
    IRegulationRetrievalService retrievalService,
    IComplianceEvaluationService evaluationService,
    IGroundedAnswerService? groundedAnswerService = null,
    IDeterministicCitationValidator? citationValidator = null)
    : ComplianceGrpc.RegulatoryComplianceService.RegulatoryComplianceServiceBase
{
    public override async Task<ComplianceGrpc.ComplianceEvaluationResponse> EvaluateCompliance(
        ComplianceGrpc.EvaluateComplianceRequest request,
        ServerCallContext context)
    {
        if (request.EffectiveAt is null)
            throw InvalidArgument("EffectiveAt is required.");
        try
        {
            var input = new ComplianceEvaluationInput(
                request.IdempotencyKey,
                ParseRequiredId(request.ExternalShipmentId, "ExternalShipmentId"),
                request.Cargo.Select(cargo => new CargoEvaluationSnapshot(
                    cargo.Name,
                    string.IsNullOrWhiteSpace(cargo.HsCode) ? null : cargo.HsCode,
                    cargo.Quantity,
                    cargo.Unit,
                    Convert.ToDecimal(cargo.WeightKg),
                    Convert.ToDecimal(cargo.VolumeM3),
                    cargo.IsDangerousGoods,
                    string.IsNullOrWhiteSpace(cargo.DangerousGoodsCode) ? null : cargo.DangerousGoodsCode,
                    string.IsNullOrWhiteSpace(cargo.PackageType) ? null : cargo.PackageType)).ToArray(),
                request.OriginCountryCode,
                request.DestinationCountryCode,
                request.JurisdictionCodes.ToArray(),
                request.TransportMode,
                request.Documents.Select(document => new OcrEvaluationSnapshot(
                    ParseRequiredId(document.ExternalDocumentId, "ExternalDocumentId"),
                    document.DocumentType,
                    document.NormalizedJson,
                    Convert.ToDecimal(document.ExtractionConfidence),
                    document.NeedsReview)).ToArray(),
                request.EffectiveAt.ToDateTimeOffset());
            return MapEvaluation(await evaluationService.EvaluateAsync(input, context.CancellationToken));
        }
        catch (InvalidOperationException exception) when (
            exception.Message.Contains("Tenant context", StringComparison.Ordinal))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            throw new RpcException(new Status(StatusCode.AlreadyExists, exception.Message));
        }
        catch (ArgumentException exception)
        {
            throw InvalidArgument(exception.Message);
        }
        catch (OverflowException)
        {
            throw InvalidArgument("Evaluation contains a number outside the supported range.");
        }
    }

    public override async Task<ComplianceGrpc.ComplianceEvaluationResponse> GetComplianceEvaluation(
        ComplianceGrpc.GetComplianceEvaluationRequest request,
        ServerCallContext context)
    {
        try
        {
            return MapEvaluation(await evaluationService.GetAsync(
                ParseRequiredId(request.EvaluationId, "EvaluationId"),
                context.CancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, exception.Message));
        }
        catch (KeyNotFoundException exception)
        {
            throw new RpcException(new Status(StatusCode.NotFound, exception.Message));
        }
        catch (ArgumentException exception)
        {
            throw InvalidArgument(exception.Message);
        }
    }

    public override async Task<ComplianceGrpc.QueryRegulationsResponse> QueryRegulations(
        ComplianceGrpc.QueryRegulationsRequest request,
        ServerCallContext context)
    {
        if (request.EffectiveAt is null)
            throw InvalidArgument("EffectiveAt is required.");
        try
        {
            var result = await retrievalService.QueryAsync(
                new RegulationQueryInput(
                    request.Query,
                    request.JurisdictionCode,
                    request.EffectiveAt.ToDateTimeOffset(),
                    request.LanguageCode,
                    request.RegulationTypes.Select(MapRegulationType).ToArray(),
                    request.TopK,
                    Convert.ToDecimal(request.MinimumRelevanceScore)),
                context.CancellationToken);
            var response = new ComplianceGrpc.QueryRegulationsResponse
            {
                RetrievalTraceId = result.RetrievalTraceId.ToString(),
                EvidenceSufficiency = (ComplianceGrpc.EvidenceSufficiency)(int)result.EvidenceSufficiency,
                GeneratedExplanation = result.GeneratedExplanation
            };
            response.Evidence.AddRange(result.Evidence.Select(MapEvidence));
            return response;
        }
        catch (InvalidOperationException exception) when (
            exception.Message.Contains("Tenant context", StringComparison.Ordinal))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, exception.Message));
        }
        catch (ArgumentException exception)
        {
            throw InvalidArgument(exception.Message);
        }
        catch (OverflowException)
        {
            throw InvalidArgument("MinimumRelevanceScore is outside the supported range.");
        }
    }

    public override async Task<ComplianceGrpc.IngestRegulatorySourceResponse> IngestRegulatorySource(
        ComplianceGrpc.IngestRegulatorySourceRequest request,
        ServerCallContext context)
    {
        if (request.PublishedAt is null || request.EffectiveFrom is null)
            throw InvalidArgument("PublishedAt and EffectiveFrom are required.");

        try
        {
            var result = await ingestionService.IngestAsync(
                new RegulatoryIngestionInput(
                    request.IdempotencyKey,
                    request.Authority,
                    request.Title,
                    request.CanonicalSourceUri,
                    request.JurisdictionCode,
                    MapRegulationType(request.RegulationType),
                    request.LanguageCode,
                    request.VersionLabel,
                    request.PublishedAt.ToDateTimeOffset(),
                    request.EffectiveFrom.ToDateTimeOffset(),
                    request.EffectiveTo?.ToDateTimeOffset(),
                    request.ContentReference,
                    request.FileName,
                    request.MimeType,
                    request.SizeBytes,
                    request.ContentSha256,
                    request.Content.Memory,
                    MapVisibility(request.Visibility)),
                context.CancellationToken);
            return new ComplianceGrpc.IngestRegulatorySourceResponse
            {
                RegulatoryDocumentId = result.RegulatoryDocumentId.ToString(),
                DocumentVersionId = result.DocumentVersionId.ToString(),
                Status = (ComplianceGrpc.RegulatoryIngestionStatus)(int)result.Status,
                ChunkCount = result.ChunkCount,
                Replayed = result.Replayed,
                ReceivedAt = Timestamp.FromDateTimeOffset(result.ReceivedAt)
            };
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, exception.Message));
        }
        catch (InvalidOperationException exception) when (
            exception.Message.Contains("Tenant context", StringComparison.Ordinal))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            throw new RpcException(new Status(StatusCode.AlreadyExists, exception.Message));
        }
        catch (ArgumentException exception)
        {
            throw InvalidArgument(exception.Message);
        }
    }

    public override async Task<ComplianceGrpc.IngestKnowledgeSourceResponse> IngestKnowledgeDocument(
        ComplianceGrpc.IngestKnowledgeSourceRequest request,
        ServerCallContext context)
    {
        try
        {
            var result = await knowledgeIngestionService.IngestAsync(
                new KnowledgeIngestionInput(
                    request.IdempotencyKey,
                    request.Title,
                    (RegulatoryCompliance.Domain.Enums.KnowledgeCategory)(int)request.Category,
                    request.SourceReference,
                    request.LanguageCode,
                    request.VersionLabel,
                    request.ContentReference,
                    request.FileName,
                    request.MimeType,
                    request.SizeBytes,
                    request.ContentSha256,
                    request.Content.Memory,
                    MapVisibility(request.Visibility)),
                context.CancellationToken);

            return new ComplianceGrpc.IngestKnowledgeSourceResponse
            {
                KnowledgeDocumentId = result.KnowledgeDocumentId.ToString(),
                DocumentVersionId = result.DocumentVersionId.ToString(),
                Status = (ComplianceGrpc.RegulatoryIngestionStatus)(int)result.Status,
                ChunkCount = result.ChunkCount,
                Replayed = result.Replayed,
                ReceivedAt = Timestamp.FromDateTimeOffset(result.ReceivedAt)
            };
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, exception.Message));
        }
        catch (InvalidOperationException exception) when (
            exception.Message.Contains("Tenant context", StringComparison.Ordinal) ||
            exception.Message.Contains("Tenant ID", StringComparison.Ordinal))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            throw new RpcException(new Status(StatusCode.AlreadyExists, exception.Message));
        }
        catch (ArgumentException exception)
        {
            throw InvalidArgument(exception.Message);
        }
    }

    public override async Task<ComplianceGrpc.QueryKnowledgeResponse> QueryKnowledge(
        ComplianceGrpc.QueryKnowledgeRequest request,
        ServerCallContext context)
    {
        try
        {
            var categories = request.Categories
                .Select(c => (RegulatoryCompliance.Domain.Enums.KnowledgeCategory)(int)c)
                .ToList();

            var results = await knowledgeIngestionService.QueryAsync(
                request.Query,
                categories,
                request.TopK > 0 ? request.TopK : 10,
                Convert.ToDecimal(request.MinimumRelevanceScore),
                context.CancellationToken);

            var response = new ComplianceGrpc.QueryKnowledgeResponse
            {
                RetrievalTraceId = Guid.NewGuid().ToString()
            };

            foreach (var item in results)
            {
                response.Evidence.Add(new ComplianceGrpc.KnowledgeEvidence
                {
                    KnowledgeDocumentId = item.KnowledgeDocumentId.ToString(),
                    DocumentVersionId = item.DocumentVersionId.ToString(),
                    ChunkId = item.ChunkId.ToString(),
                    Title = item.Title,
                    Category = (ComplianceGrpc.KnowledgeCategory)(int)item.Category,
                    SectionLabel = item.SectionLabel ?? string.Empty,
                    PageLabel = item.PageLabel ?? string.Empty,
                    Excerpt = item.Excerpt,
                    RelevanceScore = Convert.ToDouble(item.RelevanceScore)
                });
            }

            return response;
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, exception.Message));
        }
        catch (ArgumentException exception)
        {
            throw InvalidArgument(exception.Message);
        }
    }

    public override async Task<ComplianceGrpc.GenerateGroundedAnswerResponse> GenerateGroundedAnswer(
        ComplianceGrpc.GenerateGroundedAnswerRequest request,
        ServerCallContext context)
    {
        if (groundedAnswerService is null)
            throw new RpcException(new Status(StatusCode.Unimplemented, "GroundedAnswerService is not configured."));

        try
        {
            var mode = request.Mode switch
            {
                ComplianceGrpc.AssistantSearchMode.Regulatory => AssistantSearchMode.Regulatory,
                ComplianceGrpc.AssistantSearchMode.Knowledge => AssistantSearchMode.Knowledge,
                _ => AssistantSearchMode.All
            };

            var regTypes = request.RegulationTypes
                .Select(t => (DomainRegulationType)(int)t)
                .ToList();

            var categories = request.Categories
                .Select(c => (RegulatoryCompliance.Domain.Enums.KnowledgeCategory)(int)c)
                .ToList();

            var effectiveAt = request.EffectiveAt?.ToDateTimeOffset();

            var result = await groundedAnswerService.GenerateAnswerAsync(
                new GroundedAnswerInput(
                    request.Query,
                    mode,
                    request.JurisdictionCode,
                    effectiveAt,
                    regTypes,
                    categories,
                    request.TopK > 0 ? request.TopK : 10,
                    Convert.ToDecimal(request.MinimumRelevanceScore)),
                context.CancellationToken);

            var response = new ComplianceGrpc.GenerateGroundedAnswerResponse
            {
                Query = result.Query,
                Answer = result.Answer,
                InsufficientEvidence = result.InsufficientEvidence,
                RetrievalTraceId = result.RetrievalTraceId.ToString(),
                Governance = new ComplianceGrpc.AssistantGovernanceMetadata
                {
                    DecisionId = result.Governance.DecisionId,
                    AutomationLevel = result.Governance.AutomationLevel,
                    RequiresApproval = result.Governance.RequiresApproval,
                    CapabilityCode = result.Governance.CapabilityCode,
                    TotalTokens = result.Governance.TotalTokens
                }
            };

            response.MissingInformation.AddRange(result.MissingInformation);

            foreach (var reg in result.RegulatoryCitations)
            {
                response.RegulatoryCitations.Add(new ComplianceGrpc.RegulatoryCitation
                {
                    EvidenceId = reg.EvidenceId,
                    SourceId = reg.SourceId.ToString(),
                    DocumentVersionId = reg.DocumentVersionId.ToString(),
                    ChunkId = reg.ChunkId.ToString(),
                    Title = reg.Title,
                    Authority = reg.Authority,
                    Jurisdiction = reg.Jurisdiction,
                    RegulationType = reg.RegulationType,
                    Section = reg.Section ?? string.Empty,
                    Page = reg.Page ?? string.Empty,
                    Excerpt = reg.Excerpt,
                    CanonicalSourceUri = reg.CanonicalSourceUri ?? string.Empty,
                    Score = reg.Score
                });
            }

            foreach (var know in result.KnowledgeReferences)
            {
                response.KnowledgeReferences.Add(new ComplianceGrpc.KnowledgeReference
                {
                    EvidenceId = know.EvidenceId,
                    SourceId = know.SourceId.ToString(),
                    DocumentVersionId = know.DocumentVersionId.ToString(),
                    ChunkId = know.ChunkId.ToString(),
                    Title = know.Title,
                    Category = know.Category,
                    Section = know.Section ?? string.Empty,
                    Page = know.Page ?? string.Empty,
                    Excerpt = know.Excerpt,
                    Score = know.Score
                });
            }

            foreach (var conf in result.Conflicts)
            {
                response.Conflicts.Add(new ComplianceGrpc.GroundedConflict
                {
                    RegulatoryEvidenceId = conf.RegulatoryEvidenceId,
                    KnowledgeEvidenceId = conf.KnowledgeEvidenceId,
                    Description = conf.Description
                });
            }

            return response;
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new RpcException(new Status(StatusCode.PermissionDenied, exception.Message));
        }
        catch (ArgumentException exception)
        {
            throw InvalidArgument(exception.Message);
        }
    }

    public override Task<ComplianceGrpc.ValidateGroundedEvidenceResponse> ValidateGroundedEvidence(
        ComplianceGrpc.ValidateGroundedEvidenceRequest request,
        ServerCallContext context)
    {
        var validator = citationValidator ?? new DeterministicCitationValidator();

        var regList = new List<GroundedEvidence>();
        foreach (var r in request.AvailableRegulatoryEvidence)
        {
            regList.Add(new GroundedEvidence(
                EvidenceId: r.EvidenceId,
                Domain: GroundedEvidenceDomain.Regulatory,
                SourceId: Guid.TryParse(r.SourceId, out var sid) ? sid : Guid.Empty,
                DocumentVersionId: Guid.TryParse(r.DocumentVersionId, out var vid) ? vid : Guid.Empty,
                ChunkId: Guid.TryParse(r.ChunkId, out var cid) ? cid : Guid.Empty,
                Title: r.Title,
                SectionLabel: r.Section,
                PageLabel: r.Page,
                Excerpt: r.Excerpt,
                RelevanceScore: Convert.ToDecimal(r.Score),
                Authority: r.Authority,
                JurisdictionCode: r.Jurisdiction,
                RegulationType: r.RegulationType,
                CanonicalSourceUri: r.CanonicalSourceUri));
        }

        var knowList = new List<GroundedEvidence>();
        foreach (var k in request.AvailableKnowledgeEvidence)
        {
            knowList.Add(new GroundedEvidence(
                EvidenceId: k.EvidenceId,
                Domain: GroundedEvidenceDomain.Knowledge,
                SourceId: Guid.TryParse(k.SourceId, out var sid) ? sid : Guid.Empty,
                DocumentVersionId: Guid.TryParse(k.DocumentVersionId, out var vid) ? vid : Guid.Empty,
                ChunkId: Guid.TryParse(k.ChunkId, out var cid) ? cid : Guid.Empty,
                Title: k.Title,
                SectionLabel: k.Section,
                PageLabel: k.Page,
                Excerpt: k.Excerpt,
                RelevanceScore: Convert.ToDecimal(k.Score),
                KnowledgeCategory: k.Category));
        }

        var evidenceContext = new EvidenceContext(regList, knowList);

        var rawLlm = new LlmParsedResponse(
            Answer: request.Answer,
            Citations: request.Citations.Select(c => new LlmCitationItem(c.EvidenceId)).ToList(),
            KnowledgeReferences: request.KnowledgeReferences.Select(k => new LlmKnowledgeItem(k.EvidenceId)).ToList(),
            Conflicts: request.Conflicts.Select(c => new LlmConflictItem(c.RegulatoryEvidenceId, c.KnowledgeEvidenceId, c.Description)).ToList(),
            InsufficientEvidence: request.InsufficientEvidence,
            MissingInformation: request.MissingInformation.ToList());

        var validated = validator.Validate(rawLlm, evidenceContext);

        var response = new ComplianceGrpc.ValidateGroundedEvidenceResponse
        {
            SanitizedAnswer = validated.Answer,
            InsufficientEvidence = validated.InsufficientEvidence
        };

        response.MissingInformation.AddRange(validated.MissingInformation);

        foreach (var reg in validated.ValidatedRegulatoryCitations)
        {
            response.ValidatedRegulatoryCitations.Add(new ComplianceGrpc.RegulatoryCitation
            {
                EvidenceId = reg.EvidenceId,
                SourceId = reg.SourceId.ToString(),
                DocumentVersionId = reg.DocumentVersionId.ToString(),
                ChunkId = reg.ChunkId.ToString(),
                Title = reg.Title,
                Authority = reg.Authority ?? string.Empty,
                Jurisdiction = reg.JurisdictionCode ?? string.Empty,
                RegulationType = reg.RegulationType ?? string.Empty,
                Section = reg.SectionLabel ?? string.Empty,
                Page = reg.PageLabel ?? string.Empty,
                Excerpt = reg.Excerpt,
                CanonicalSourceUri = reg.CanonicalSourceUri ?? string.Empty,
                Score = Convert.ToDouble(reg.RelevanceScore)
            });
        }

        foreach (var know in validated.ValidatedKnowledgeReferences)
        {
            response.ValidatedKnowledgeReferences.Add(new ComplianceGrpc.KnowledgeReference
            {
                EvidenceId = know.EvidenceId,
                SourceId = know.SourceId.ToString(),
                DocumentVersionId = know.DocumentVersionId.ToString(),
                ChunkId = know.ChunkId.ToString(),
                Title = know.Title,
                Category = know.KnowledgeCategory ?? string.Empty,
                Section = know.SectionLabel ?? string.Empty,
                Page = know.PageLabel ?? string.Empty,
                Excerpt = know.Excerpt,
                Score = Convert.ToDouble(know.RelevanceScore)
            });
        }

        foreach (var conf in validated.ValidatedConflicts)
        {
            response.ValidatedConflicts.Add(new ComplianceGrpc.GroundedConflict
            {
                RegulatoryEvidenceId = conf.RegulatoryEvidence.EvidenceId,
                KnowledgeEvidenceId = conf.KnowledgeEvidence.EvidenceId,
                Description = conf.Description
            });
        }

        return Task.FromResult(response);
    }

    private static DomainRegulationType MapRegulationType(ComplianceGrpc.RegulationType value)
    {
        var mapped = (DomainRegulationType)(int)value;
        return System.Enum.IsDefined(mapped)
            ? mapped
            : throw InvalidArgument("RegulationType is invalid.");
    }

    private static ComplianceGrpc.RegulationEvidence MapEvidence(RegulationEvidenceResult evidence)
    {
        var citation = new ComplianceGrpc.RegulationCitation
        {
            RegulatoryDocumentId = evidence.RegulatoryDocumentId.ToString(),
            DocumentVersionId = evidence.DocumentVersionId.ToString(),
            ChunkId = evidence.ChunkId.ToString(),
            Authority = evidence.Authority,
            Title = evidence.Title,
            CanonicalSourceUri = evidence.CanonicalSourceUri,
            VersionLabel = evidence.VersionLabel,
            SectionLabel = evidence.SectionLabel ?? string.Empty,
            PageLabel = evidence.PageLabel ?? string.Empty,
            EffectiveFrom = Timestamp.FromDateTimeOffset(evidence.EffectiveFrom),
            Excerpt = evidence.Excerpt,
            RelevanceScore = Convert.ToDouble(evidence.RelevanceScore)
        };
        if (evidence.EffectiveTo.HasValue)
            citation.EffectiveTo = Timestamp.FromDateTimeOffset(evidence.EffectiveTo.Value);
        return new ComplianceGrpc.RegulationEvidence
        {
            Citation = citation,
            RegulationType = (ComplianceGrpc.RegulationType)(int)evidence.RegulationType,
            JurisdictionCode = evidence.JurisdictionCode,
            LanguageCode = evidence.LanguageCode
        };
    }

    public static ComplianceGrpc.ComplianceEvaluationResponse MapEvaluation(
        ComplianceEvaluation evaluation)
    {
        var response = new ComplianceGrpc.ComplianceEvaluationResponse
        {
            EvaluationId = evaluation.Id.ToString(),
            ExternalShipmentId = evaluation.ExternalShipmentId.ToString(),
            Status = (ComplianceGrpc.ComplianceEvaluationStatus)(int)evaluation.Status,
            RiskLevel = evaluation.RiskLevel.HasValue
                ? (ComplianceGrpc.ComplianceRiskLevel)(int)evaluation.RiskLevel.Value
                : ComplianceGrpc.ComplianceRiskLevel.Unspecified,
            ComplianceConfidence = Convert.ToDouble(evaluation.Confidence ?? 0m),
            EvidenceSufficiency = evaluation.EvidenceSufficiency.HasValue
                ? (ComplianceGrpc.EvidenceSufficiency)(int)evaluation.EvidenceSufficiency.Value
                : ComplianceGrpc.EvidenceSufficiency.Unspecified,
            RequestedAt = Timestamp.FromDateTimeOffset(evaluation.RequestedAt),
            ErrorCode = evaluation.ErrorCode ?? string.Empty,
            ErrorMessage = evaluation.ErrorMessage ?? string.Empty
        };
        if (evaluation.CompletedAt.HasValue)
            response.CompletedAt = Timestamp.FromDateTimeOffset(evaluation.CompletedAt.Value);
        response.Assumptions.AddRange(DeserializeStrings(evaluation.AssumptionsJson));
        response.MissingDocuments.AddRange(DeserializeStrings(evaluation.MissingDocumentsJson));
        response.Findings.AddRange(evaluation.Findings.Select(MapFinding));
        return response;
    }

    private static ComplianceGrpc.ComplianceFinding MapFinding(ComplianceFinding finding)
    {
        var response = new ComplianceGrpc.ComplianceFinding
        {
            FindingId = finding.Id.ToString(),
            Type = (ComplianceGrpc.ComplianceFindingType)(int)finding.Type,
            Code = finding.Code,
            Category = finding.Category,
            Title = finding.Title,
            Description = finding.Description,
            Severity = (ComplianceGrpc.ComplianceRiskLevel)(int)finding.Severity
        };
        response.Citations.AddRange(finding.Citations.Select(citation =>
        {
            var mapped = new ComplianceGrpc.RegulationCitation
            {
                RegulatoryDocumentId = citation.RegulatoryDocumentId.ToString(),
                DocumentVersionId = citation.RegulatoryDocumentVersionId.ToString(),
                ChunkId = citation.RegulatoryChunkId.ToString(),
                Authority = citation.Authority,
                Title = citation.Title,
                CanonicalSourceUri = citation.CanonicalSourceUri,
                VersionLabel = citation.VersionLabel,
                SectionLabel = citation.SectionLabel ?? string.Empty,
                PageLabel = citation.PageLabel ?? string.Empty,
                EffectiveFrom = Timestamp.FromDateTimeOffset(citation.EffectiveFrom),
                Excerpt = citation.Excerpt,
                RelevanceScore = Convert.ToDouble(citation.RelevanceScore)
            };
            if (citation.EffectiveTo.HasValue)
                mapped.EffectiveTo = Timestamp.FromDateTimeOffset(citation.EffectiveTo.Value);
            return mapped;
        }));
        return response;
    }

    private static string[] DeserializeStrings(string json) =>
        JsonSerializer.Deserialize<string[]>(json) ?? [];

    private static Guid ParseRequiredId(string value, string fieldName) =>
        Guid.TryParse(value, out var parsed) && parsed != Guid.Empty
            ? parsed
            : throw InvalidArgument($"{fieldName} is invalid.");

    private static DomainVisibility MapVisibility(ComplianceGrpc.RegulatorySourceVisibility value) =>
        value switch
        {
            ComplianceGrpc.RegulatorySourceVisibility.Tenant => DomainVisibility.Tenant,
            ComplianceGrpc.RegulatorySourceVisibility.Platform => DomainVisibility.Platform,
            _ => throw InvalidArgument("Visibility is invalid.")
        };

    private static RpcException InvalidArgument(string message) =>
        new(new Status(StatusCode.InvalidArgument, message));
}

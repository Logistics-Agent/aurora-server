using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using RegulatoryCompliance.Application.Ingestion;
using RegulatoryCompliance.Application.Retrieval;
using RegulatoryCompliance.Application.Evaluations;
using RegulatoryCompliance.Domain.Entities;
using System.Text.Json;
using ComplianceGrpc = RegulatoryCompliance.Grpc;
using DomainRegulationType = RegulatoryCompliance.Domain.Enums.RegulationType;
using DomainVisibility = RegulatoryCompliance.Domain.Enums.SourceVisibility;

namespace RegulatoryCompliance.GrpcServices;

public sealed class RegulatoryComplianceGrpcService(
    IRegulatoryIngestionService ingestionService,
    IRegulationRetrievalService retrievalService,
    IComplianceEvaluationService evaluationService)
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

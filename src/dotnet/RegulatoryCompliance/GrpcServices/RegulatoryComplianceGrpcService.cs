using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using RegulatoryCompliance.Application.Ingestion;
using RegulatoryCompliance.Application.Retrieval;
using ComplianceGrpc = RegulatoryCompliance.Grpc;
using DomainRegulationType = RegulatoryCompliance.Domain.Enums.RegulationType;
using DomainVisibility = RegulatoryCompliance.Domain.Enums.SourceVisibility;

namespace RegulatoryCompliance.GrpcServices;

public sealed class RegulatoryComplianceGrpcService(
    IRegulatoryIngestionService ingestionService,
    IRegulationRetrievalService retrievalService)
    : ComplianceGrpc.RegulatoryComplianceService.RegulatoryComplianceServiceBase
{
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

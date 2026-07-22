using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using RegulatoryCompliance.Application.Ingestion;
using ComplianceGrpc = RegulatoryCompliance.Grpc;
using DomainRegulationType = RegulatoryCompliance.Domain.Enums.RegulationType;
using DomainVisibility = RegulatoryCompliance.Domain.Enums.SourceVisibility;

namespace RegulatoryCompliance.GrpcServices;

public sealed class RegulatoryComplianceGrpcService(IRegulatoryIngestionService ingestionService)
    : ComplianceGrpc.RegulatoryComplianceService.RegulatoryComplianceServiceBase
{
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

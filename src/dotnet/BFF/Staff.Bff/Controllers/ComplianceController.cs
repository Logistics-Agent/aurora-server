using Asp.Versioning;
using BuildingBlocks.BFF.Attributes;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using RegulatoryCompliance.Grpc;
using Shared.Constants;
using Shared.Security;

namespace StaffBff.Controllers;

/// <summary>
/// Đánh giá tuân thủ hải quan và Trợ lý RAG Pháp lý (Regulatory Compliance Service).
/// Route: /api/v1/compliance
/// </summary>
[ApiVersion("1.0")]
public class ComplianceController(
    RegulatoryComplianceService.RegulatoryComplianceServiceClient complianceClient,
    ICurrentUserService currentUser,
    ILogger<ComplianceController> logger) : StaffControllerBase
{
    [HttpPost("evaluations")]
    [RequirePermission(PermissionConstants.Compliance.Read)]
    public async Task<IActionResult> EvaluateCompliance(
        [FromBody] EvaluateComplianceBody body,
        CancellationToken ct = default)
    {
        try
        {
            var shipmentIdStr = Guid.TryParse(body.ExternalShipmentId, out var parsedShipmentGuid) && parsedShipmentGuid != Guid.Empty
                ? parsedShipmentGuid.ToString()
                : Guid.NewGuid().ToString();

            var req = new EvaluateComplianceRequest
            {
                IdempotencyKey = body.IdempotencyKey ?? Guid.NewGuid().ToString(),
                ExternalShipmentId = shipmentIdStr,
                OriginCountryCode = body.OriginCountryCode ?? string.Empty,
                DestinationCountryCode = body.DestinationCountryCode ?? string.Empty,
                TransportMode = body.TransportMode ?? string.Empty,
                EffectiveAt = Timestamp.FromDateTimeOffset(body.EffectiveAt ?? DateTimeOffset.UtcNow)
            };

            if (body.JurisdictionCodes != null)
            {
                req.JurisdictionCodes.AddRange(body.JurisdictionCodes);
            }

            if (body.Cargo != null)
            {
                req.Cargo.AddRange(body.Cargo.Select(c => new CargoSnapshot
                {
                    Name = c.Name ?? string.Empty,
                    HsCode = c.HsCode ?? string.Empty,
                    Quantity = c.Quantity,
                    Unit = c.Unit ?? string.Empty,
                    WeightKg = c.WeightKg,
                    VolumeM3 = c.VolumeM3,
                    IsDangerousGoods = c.IsDangerousGoods,
                    DangerousGoodsCode = c.DangerousGoodsCode ?? string.Empty,
                    PackageType = c.PackageType ?? string.Empty
                }));
            }

            if (body.Documents != null)
            {
                req.Documents.AddRange(body.Documents.Select(d =>
                {
                    var docIdStr = Guid.TryParse(d.ExternalDocumentId, out var parsedDocGuid) && parsedDocGuid != Guid.Empty
                        ? parsedDocGuid.ToString()
                        : Guid.NewGuid().ToString();

                    return new OcrDocumentSnapshot
                    {
                        ExternalDocumentId = docIdStr,
                        DocumentType = d.DocumentType ?? string.Empty,
                        NormalizedJson = d.NormalizedJson ?? "{}",
                        ExtractionConfidence = d.ExtractionConfidence,
                        NeedsReview = d.NeedsReview
                    };
                }));
            }

            var response = await complianceClient.EvaluateComplianceAsync(req, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
    }

    [HttpGet("evaluations/{id}")]
    [RequirePermission(PermissionConstants.Compliance.Read)]
    public async Task<IActionResult> GetComplianceEvaluation(
        [FromRoute] string id,
        CancellationToken ct = default)
    {
        try
        {
            var req = new GetComplianceEvaluationRequest { EvaluationId = id };
            var response = await complianceClient.GetComplianceEvaluationAsync(req, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
        {
            return NotFound(new { detail = ex.Status.Detail });
        }
    }

    [HttpPost("copilot/ask")]
    [RequirePermission(PermissionConstants.Compliance.Read)]
    public async Task<IActionResult> AskComplianceCopilot(
        [FromBody] AskComplianceCopilotBody body,
        CancellationToken ct = default)
    {
        try
        {
            var req = new GenerateGroundedAnswerRequest
            {
                Query = body.Query ?? string.Empty,
                Mode = (AssistantSearchMode)body.Mode,
                JurisdictionCode = body.JurisdictionCode ?? string.Empty,
                EffectiveAt = Timestamp.FromDateTimeOffset(body.EffectiveAt ?? DateTimeOffset.UtcNow),
                TopK = body.TopK > 0 ? body.TopK : 5,
                MinimumRelevanceScore = body.MinimumRelevanceScore
            };

            var response = await complianceClient.GenerateGroundedAnswerAsync(req, cancellationToken: ct);
            return Ok(new AssistantQueryResponse(
                response.Query,
                response.Answer,
                response.RegulatoryCitations.Select(r => new AssistantRegulatoryCitation(
                    r.EvidenceId, r.SourceId, r.DocumentVersionId, r.ChunkId, r.Title,
                    r.Authority, r.Jurisdiction, r.RegulationType, r.Section, r.Page,
                    r.Excerpt, r.CanonicalSourceUri, r.Score)).ToList(),
                response.KnowledgeReferences.Select(k => new AssistantKnowledgeReference(
                    k.EvidenceId, k.SourceId, k.DocumentVersionId, k.ChunkId, k.Title,
                    k.Category, k.Section, k.Page, k.Excerpt, k.Score)).ToList(),
                response.Conflicts.Select(c => new AssistantConflict(
                    c.RegulatoryEvidenceId, c.KnowledgeEvidenceId, c.Description)).ToList(),
                response.InsufficientEvidence,
                response.MissingInformation.ToList(),
                new AssistantGovernanceSummary(
                    response.Governance?.DecisionId ?? string.Empty,
                    response.Governance?.AutomationLevel ?? "ASSISTED",
                    response.Governance?.RequiresApproval ?? false,
                    response.Governance?.CapabilityCode ?? "compliance.answer",
                    response.Governance?.TotalTokens ?? 0),
                response.RetrievalTraceId));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.PermissionDenied)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "PERMISSION_DENIED",
                Detail = ex.Status.Detail,
                Status = StatusCodes.Status403Forbidden
            });
        }
    }
}

// ── DTOs ───────────────────────────────────────────────────────────────────

public record EvaluateComplianceBody(
    string? IdempotencyKey,
    string? ExternalShipmentId,
    string? OriginCountryCode,
    string? DestinationCountryCode,
    string? TransportMode,
    DateTimeOffset? EffectiveAt,
    List<string>? JurisdictionCodes,
    List<CargoSnapshotDto>? Cargo,
    List<OcrDocumentSnapshotDto>? Documents);

public record CargoSnapshotDto(
    string? Name,
    string? HsCode,
    int Quantity,
    string? Unit,
    double WeightKg,
    double VolumeM3,
    bool IsDangerousGoods,
    string? DangerousGoodsCode,
    string? PackageType);

public record OcrDocumentSnapshotDto(
    string? ExternalDocumentId,
    string? DocumentType,
    string? NormalizedJson,
    double ExtractionConfidence,
    bool NeedsReview);

public record AskComplianceCopilotBody(
    string? Query,
    int Mode,
    string? JurisdictionCode,
    DateTimeOffset? EffectiveAt,
    int TopK,
    double MinimumRelevanceScore);

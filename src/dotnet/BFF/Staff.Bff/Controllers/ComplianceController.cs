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
    [RequirePermission(PermissionConstants.Modules.Compliance, PermissionConstants.Create)]
    public async Task<IActionResult> EvaluateCompliance(
        [FromBody] EvaluateComplianceBody body,
        CancellationToken ct = default)
    {
        try
        {
            var req = new EvaluateComplianceRequest
            {
                IdempotencyKey = body.IdempotencyKey ?? Guid.NewGuid().ToString(),
                ExternalShipmentId = body.ExternalShipmentId ?? string.Empty,
                OriginCountryCode = body.OriginCountryCode ?? string.Empty,
                DestinationCountryCode = body.DestinationCountryCode ?? string.Empty,
                TransportMode = body.TransportMode ?? string.Empty,
                EffectiveAt = body.EffectiveAt.HasValue ? Timestamp.FromDateTimeOffset(body.EffectiveAt.Value) : null
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
                req.Documents.AddRange(body.Documents.Select(d => new OcrDocumentSnapshot
                {
                    ExternalDocumentId = d.ExternalDocumentId ?? string.Empty,
                    DocumentType = d.DocumentType ?? string.Empty,
                    NormalizedJson = d.NormalizedJson ?? "{}",
                    ExtractionConfidence = d.ExtractionConfidence,
                    NeedsReview = d.NeedsReview
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
    [RequirePermission(PermissionConstants.Modules.Compliance, PermissionConstants.Read)]
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
    [RequirePermission(PermissionConstants.Modules.Compliance, PermissionConstants.Read)]
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
                EffectiveAt = body.EffectiveAt.HasValue ? Timestamp.FromDateTimeOffset(body.EffectiveAt.Value) : null,
                TopK = body.TopK > 0 ? body.TopK : 5,
                MinimumRelevanceScore = body.MinimumRelevanceScore
            };

            var response = await complianceClient.GenerateGroundedAnswerAsync(req, cancellationToken: ct);
            return Ok(response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new { detail = ex.Status.Detail });
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

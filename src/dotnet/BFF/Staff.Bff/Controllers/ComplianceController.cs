using Asp.Versioning;
using BuildingBlocks.BFF.Attributes;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using RegulatoryCompliance.Grpc;
using Shared.Constants;
using Shared.Security;
using StaffBff.Services;

namespace StaffBff.Controllers;

/// <summary>
/// Đánh giá tuân thủ hải quan và Trợ lý RAG Pháp lý (Regulatory Compliance Service).
/// Route: /api/v1/compliance
/// </summary>
[ApiVersion("1.0")]
public class ComplianceController(
    RegulatoryComplianceService.RegulatoryComplianceServiceClient complianceClient,
    ComplianceSnapshotComposer snapshotComposer,
    ICurrentUserService currentUser,
    ILogger<ComplianceController> logger) : StaffControllerBase
{
    [HttpPost("~/api/v{version:apiVersion}/shipments/{shipmentId}/compliance-evaluations")]
    [RequirePermission(PermissionConstants.Compliance.Read)]
    public async Task<IActionResult> StartEvaluation(
        [FromRoute] string shipmentId,
        [FromBody] StartComplianceEvaluationRequest body,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(body.IdempotencyKey) || body.IdempotencyKey.Trim().Length > 150)
                throw new ArgumentException("idempotencyKey is required.", nameof(body.IdempotencyKey));

            var snapshot = await snapshotComposer.ComposeAsync(
                shipmentId,
                body.EffectiveAt ?? DateTimeOffset.UtcNow,
                ct);

            var req = new EvaluateComplianceRequest
            {
                IdempotencyKey = body.IdempotencyKey.Trim(),
                ExternalShipmentId = snapshot.ShipmentId.ToString(),
                OriginCountryCode = snapshot.OriginCountryCode,
                DestinationCountryCode = snapshot.DestinationCountryCode,
                TransportMode = snapshot.TransportMode,
                EffectiveAt = Timestamp.FromDateTimeOffset(snapshot.EffectiveAt),
                ShipmentVersion = snapshot.ShipmentVersion
            };

            req.JurisdictionCodes.AddRange(snapshot.JurisdictionCodes);
            req.Cargo.AddRange(snapshot.Cargo.Select(c => new CargoSnapshot
            {
                Name = c.Name,
                HsCode = c.HsCode ?? string.Empty,
                Quantity = c.Quantity,
                Unit = c.Unit,
                WeightKg = c.WeightKg,
                VolumeM3 = c.VolumeM3,
                IsDangerousGoods = c.IsDangerousGoods,
                DangerousGoodsCode = c.DangerousGoodsCode ?? string.Empty,
                PackageType = c.PackageType ?? string.Empty
            }));
            req.Documents.AddRange(snapshot.Documents.Select(d => new OcrDocumentSnapshot
            {
                ExternalDocumentId = d.ExternalDocumentId.ToString(),
                DocumentType = d.DocumentType,
                NormalizedJson = d.NormalizedJson,
                ExtractionConfidence = d.ExtractionConfidence,
                NeedsReview = d.NeedsReview
            }));

            var response = await complianceClient.EvaluateComplianceAsync(req, cancellationToken: ct);
            var location = $"/api/v1/compliance/evaluations/{response.EvaluationId}";
            logger.LogInformation(
                "Compliance evaluation {EvaluationId} accepted for shipment {ShipmentId} in tenant {TenantId}",
                response.EvaluationId,
                snapshot.ShipmentId,
                currentUser.TenantId);
            return Accepted(location, response);
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "INVALID_REQUEST",
                Detail = ex.Status.Detail,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (ComplianceSnapshotIncompleteException ex)
        {
            var problem = new ProblemDetails
            {
                Title = "SHIPMENT_SNAPSHOT_INCOMPLETE",
                Detail = "Required shipment data is missing.",
                Status = StatusCodes.Status409Conflict
            };
            problem.Extensions["missingFields"] = ex.MissingFields;
            return Conflict(problem);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "INVALID_REQUEST",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title = "SHIPMENT_NOT_FOUND",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
    }

    [HttpGet("evaluations")]
    [RequirePermission(PermissionConstants.Compliance.Read)]
    public async Task<IActionResult> ListComplianceEvaluations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        var request = new ListComplianceEvaluationsRequest
        {
            Page = page,
            PageSize = pageSize
        };
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!System.Enum.TryParse<ComplianceEvaluationStatus>(status, true, out var parsedStatus))
                return BadRequest(new ProblemDetails
                {
                    Title = "INVALID_REQUEST",
                    Detail = "status is invalid.",
                    Status = StatusCodes.Status400BadRequest
                });
            request.Status = parsedStatus;
        }

        try
        {
            var response = await complianceClient.ListComplianceEvaluationsAsync(
                request,
                cancellationToken: ct);
            return Ok(new
            {
                items = response.Items,
                page = response.Page,
                pageSize = response.PageSize,
                totalCount = response.TotalCount
            });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Unauthenticated)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "TENANT_CONTEXT_REQUIRED",
                Detail = ex.Status.Detail,
                Status = StatusCodes.Status401Unauthorized
            });
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

public record StartComplianceEvaluationRequest(string? IdempotencyKey, DateTimeOffset? EffectiveAt);

public record AskComplianceCopilotBody(
    string? Query,
    int Mode,
    string? JurisdictionCode,
    DateTimeOffset? EffectiveAt,
    int TopK,
    double MinimumRelevanceScore);

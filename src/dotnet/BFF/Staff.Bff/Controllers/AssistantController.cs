using System.Net;
using Asp.Versioning;
using BuildingBlocks.BFF.Attributes;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegulatoryCompliance.Grpc;
using Shared.Constants;
using ShipmentWorkflow.Grpc;

namespace StaffBff.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/assistant")]
[Route("api/assistant")]
[Authorize]
[RequirePermission(PermissionConstants.Assistant.Query, PermissionConstants.Compliance.Read)]
public sealed class AssistantController(
    RegulatoryComplianceService.RegulatoryComplianceServiceClient regulatoryClient,
    ShipmentWorkflowService.ShipmentWorkflowServiceClient shipmentClient,
    ILogger<AssistantController> logger)
    : ControllerBase
{
    /// <summary>
    /// Grounded Tenant Assistant Query API:
    /// Synthesizes verified, citation-backed answers grounded strictly in Regulatory and Knowledge evidence.
    /// Preserves legal authority distinction and identifies potential conflicts with internal SOPs.
    /// </summary>
    [HttpPost("query")]
    [ProducesResponseType(typeof(AssistantQueryResponse), 200)]
    public async Task<IActionResult> QueryAssistant(
        [FromBody] AssistantQueryRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new ProblemDetails
            {
                Title = "INVALID_QUERY",
                Detail = "Query text is required.",
                Status = (int)HttpStatusCode.BadRequest
            });

        var modeName = string.IsNullOrWhiteSpace(request.Mode) ? "ALL" : request.Mode.Trim().ToUpperInvariant();
        var mode = modeName switch
        {
            "REGULATORY" => AssistantSearchMode.Regulatory,
            "KNOWLEDGE" => AssistantSearchMode.Knowledge,
            "ALL" => AssistantSearchMode.All,
            _ => (AssistantSearchMode)(-1)
        };
        if (!System.Enum.IsDefined(mode))
            return BadRequest(new ProblemDetails
            {
                Title = "INVALID_MODE",
                Detail = "Mode must be REGULATORY, KNOWLEDGE, or ALL.",
                Status = (int)HttpStatusCode.BadRequest
            });

        if (request.Query.Trim().Length > 2_000)
            return BadRequest(new ProblemDetails
            {
                Title = "INVALID_QUERY",
                Detail = "Query must contain at most 2,000 characters.",
                Status = (int)HttpStatusCode.BadRequest
            });
        if (request.TopK is < 0 or > 20)
            return BadRequest(new ProblemDetails
            {
                Title = "INVALID_TOP_K",
                Detail = "TopK must be between 1 and 20.",
                Status = (int)HttpStatusCode.BadRequest
            });
        if (request.MinimumScore is < 0 or > 1)
            return BadRequest(new ProblemDetails
            {
                Title = "INVALID_SCORE",
                Detail = "MinimumScore must be between 0 and 1.",
                Status = (int)HttpStatusCode.BadRequest
            });

        var rpcRequest = new GenerateGroundedAnswerRequest
        {
            Query = request.Query.Trim(),
            Mode = mode,
            JurisdictionCode = string.IsNullOrWhiteSpace(request.JurisdictionCode)
                ? "VN"
                : request.JurisdictionCode.Trim(),
            EffectiveAt = request.EffectiveAt.HasValue
                ? Timestamp.FromDateTimeOffset(request.EffectiveAt.Value)
                : Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
            TopK = request.TopK > 0 ? request.TopK : 10,
            MinimumRelevanceScore = (double)(request.MinimumScore > 0 ? request.MinimumScore : 0.4m)
        };

        if (request.Context is not null)
        {
            if (!TryParseContextId(request.Context.EvaluationId, out var evaluationId))
                return BadRequest(new ProblemDetails
                {
                    Title = "INVALID_ASSISTANT_CONTEXT",
                    Detail = "Assistant context evaluationId must be a valid UUID.",
                    Status = (int)HttpStatusCode.BadRequest
                });

            Guid? shipmentId = null;
            string? shipmentSummary = null;
            var rawShipmentId = request.Context.ShipmentId?.Trim();
            if (!string.IsNullOrWhiteSpace(rawShipmentId))
            {
                if (rawShipmentId.Length > 128 ||
                    (Guid.TryParse(rawShipmentId, out var parsedEmptyCheck) && parsedEmptyCheck == Guid.Empty))
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "INVALID_ASSISTANT_CONTEXT",
                        Detail = "Assistant context shipmentId must be a valid shipment UUID or shipment code.",
                        Status = (int)HttpStatusCode.BadRequest
                    });
                }

                ShipmentResponse shipment;
                try
                {
                    shipment = await shipmentClient.GetShipmentAsync(
                        new GetShipmentRequest { Id = rawShipmentId },
                        cancellationToken: cancellationToken);
                }
                catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
                {
                    return NotFound(new ProblemDetails
                    {
                        Title = "SHIPMENT_NOT_FOUND",
                        Detail = "The requested shipment context was not found.",
                        Status = (int)HttpStatusCode.NotFound
                    });
                }
                catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "INVALID_ASSISTANT_CONTEXT",
                        Detail = string.IsNullOrWhiteSpace(ex.Status.Detail)
                            ? "Assistant context shipmentId is invalid."
                            : ex.Status.Detail,
                        Status = (int)HttpStatusCode.BadRequest
                    });
                }
                catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.PermissionDenied)
                {
                    return StatusCode((int)HttpStatusCode.Forbidden, new ProblemDetails
                    {
                        Title = "PERMISSION_DENIED",
                        Detail = ex.Status.Detail,
                        Status = (int)HttpStatusCode.Forbidden
                    });
                }
                catch (RpcException ex) when (ex.StatusCode is Grpc.Core.StatusCode.Unavailable or Grpc.Core.StatusCode.DeadlineExceeded)
                {
                    logger.LogWarning(ex, "Shipment workflow service is unavailable or timed out for assistant context lookup.");
                    return StatusCode((int)HttpStatusCode.ServiceUnavailable, new ProblemDetails
                    {
                        Title = "SHIPMENT_SERVICE_UNAVAILABLE",
                        Detail = ex.Status.Detail,
                        Status = (int)HttpStatusCode.ServiceUnavailable
                    });
                }
                catch (RpcException ex)
                {
                    logger.LogError(ex, "Shipment lookup gRPC call failed for assistant context: StatusCode={StatusCode}, Detail={Detail}", ex.StatusCode, ex.Status.Detail);
                    return StatusCode((int)HttpStatusCode.BadGateway, new ProblemDetails
                    {
                        Title = "SHIPMENT_SERVICE_ERROR",
                        Detail = string.IsNullOrWhiteSpace(ex.Status.Detail) ? "Failed to verify shipment context." : ex.Status.Detail,
                        Status = (int)HttpStatusCode.BadGateway
                    });
                }

                if (!Guid.TryParse(shipment.Id, out var resolvedShipmentId) || resolvedShipmentId == Guid.Empty)
                {
                    return BadRequest(new ProblemDetails
                    {
                        Title = "INVALID_ASSISTANT_CONTEXT",
                        Detail = "Resolved shipment context did not contain a valid shipment UUID.",
                        Status = (int)HttpStatusCode.BadRequest
                    });
                }

                shipmentId = resolvedShipmentId;
                shipmentSummary = BuildVerifiedShipmentSummary(shipment);
            }

            if (shipmentId.HasValue || evaluationId.HasValue || !string.IsNullOrWhiteSpace(shipmentSummary))
            {
                rpcRequest.Context = new AssistantContext
                {
                    ShipmentId = shipmentId?.ToString() ?? string.Empty,
                    EvaluationId = evaluationId?.ToString() ?? string.Empty,
                    ShipmentSummary = shipmentSummary ?? string.Empty
                };
            }
        }

        if (request.RegulationTypes != null)
        {
            foreach (var t in request.RegulationTypes)
            {
                if (!System.Enum.IsDefined(typeof(RegulationType), t) || t == (int)RegulationType.Unspecified)
                    return BadRequest(new ProblemDetails { Title = "INVALID_REGULATION_TYPE", Detail = $"Unknown regulation type: {t}." });
                rpcRequest.RegulationTypes.Add((RegulationType)t);
            }
        }

        if (request.Categories != null)
        {
            foreach (var c in request.Categories)
            {
                if (!System.Enum.IsDefined(typeof(KnowledgeCategory), c) || c == (int)KnowledgeCategory.Unspecified)
                    return BadRequest(new ProblemDetails { Title = "INVALID_CATEGORY", Detail = $"Unknown knowledge category: {c}." });
                rpcRequest.Categories.Add((KnowledgeCategory)c);
            }
        }

        try
        {
            var response = await regulatoryClient.GenerateGroundedAnswerAsync(
                rpcRequest,
                cancellationToken: cancellationToken);

            var regCitations = response.RegulatoryCitations.Select(r => new AssistantRegulatoryCitation(
                EvidenceId: r.EvidenceId,
                SourceId: r.SourceId,
                DocumentVersionId: r.DocumentVersionId,
                ChunkId: r.ChunkId,
                Title: r.Title,
                Authority: r.Authority,
                Jurisdiction: r.Jurisdiction,
                RegulationType: r.RegulationType,
                Section: r.Section,
                Page: r.Page,
                Excerpt: r.Excerpt,
                CanonicalSourceUri: r.CanonicalSourceUri,
                Score: ClampScore(r.Score))).ToList();

            var knowReferences = response.KnowledgeReferences.Select(k => new AssistantKnowledgeReference(
                EvidenceId: k.EvidenceId,
                SourceId: k.SourceId,
                DocumentVersionId: k.DocumentVersionId,
                ChunkId: k.ChunkId,
                Title: k.Title,
                Category: k.Category,
                Section: k.Section,
                Page: k.Page,
                Excerpt: k.Excerpt,
                Score: ClampScore(k.Score))).ToList();

            var conflicts = response.Conflicts.Select(c => new AssistantConflict(
                RegulatoryEvidenceId: c.RegulatoryEvidenceId,
                KnowledgeEvidenceId: c.KnowledgeEvidenceId,
                Description: c.Description)).ToList();

            var governance = new AssistantGovernanceSummary(
                DecisionId: response.Governance?.DecisionId ?? string.Empty,
                AutomationLevel: response.Governance?.AutomationLevel ?? "ASSISTED",
                RequiresApproval: response.Governance?.RequiresApproval ?? false,
                CapabilityCode: response.Governance?.CapabilityCode ?? "compliance.answer",
                TotalTokens: Math.Max(0L, response.Governance?.TotalTokens ?? 0));

            var contextSummary = response.Context is null
                ? null
                : new AssistantContextSummary(
                    ShipmentId: ParseOptionalGuid(response.Context.ShipmentId)?.ToString() ?? string.Empty,
                    EvaluationId: ParseOptionalGuid(response.Context.EvaluationId)?.ToString() ?? string.Empty,
                    Freshness: response.Context.Freshness ?? string.Empty,
                    SnapshotHash: response.Context.SnapshotHash ?? string.Empty);

            return Ok(new AssistantQueryResponse(
                Query: response.Query,
                Answer: response.Answer,
                RegulatoryCitations: regCitations,
                KnowledgeReferences: knowReferences,
                Conflicts: conflicts,
                InsufficientEvidence: response.InsufficientEvidence,
                MissingInformation: response.MissingInformation.ToList(),
                Governance: governance,
                RetrievalTraceId: response.RetrievalTraceId,
                Context: contextSummary));
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.PermissionDenied)
        {
            return StatusCode((int)HttpStatusCode.Forbidden, new ProblemDetails
            {
                Title = "PERMISSION_DENIED",
                Detail = ex.Status.Detail,
                Status = (int)HttpStatusCode.Forbidden
            });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.FailedPrecondition)
        {
            var isContextUnavailable = ex.Status.Detail.StartsWith(
                "ASSISTANT_CONTEXT_UNAVAILABLE:", StringComparison.OrdinalIgnoreCase);
            return StatusCode((int)HttpStatusCode.PreconditionFailed, new ProblemDetails
            {
                Title = isContextUnavailable ? "ASSISTANT_CONTEXT_UNAVAILABLE" : "GOVERNANCE_BLOCKED",
                Detail = ex.Status.Detail,
                Status = (int)HttpStatusCode.PreconditionFailed
            });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.Aborted)
        {
            return Conflict(new ProblemDetails
            {
                Title = "ASSISTANT_CONTEXT_MISMATCH",
                Detail = ex.Status.Detail,
                Status = (int)HttpStatusCode.Conflict
            });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "INVALID_REQUEST",
                Detail = ex.Status.Detail,
                Status = (int)HttpStatusCode.BadRequest
            });
        }
        catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.ResourceExhausted)
        {
            return StatusCode((int)HttpStatusCode.TooManyRequests, new ProblemDetails
            {
                Title = "AI_QUOTA_EXCEEDED",
                Detail = ex.Status.Detail,
                Status = (int)HttpStatusCode.TooManyRequests
            });
        }
        catch (RpcException ex) when (ex.StatusCode is Grpc.Core.StatusCode.Unavailable or Grpc.Core.StatusCode.DeadlineExceeded)
        {
            logger.LogWarning(ex, "Regulatory compliance service is unavailable or timed out for assistant query.");
            return StatusCode((int)HttpStatusCode.ServiceUnavailable, new ProblemDetails
            {
                Title = "AI_SERVICE_UNAVAILABLE",
                Detail = ex.Status.Detail,
                Status = (int)HttpStatusCode.ServiceUnavailable
            });
        }
        catch (RpcException ex)
        {
            logger.LogError(ex, "Regulatory compliance gRPC call failed: StatusCode={StatusCode}, Detail={Detail}", ex.StatusCode, ex.Status.Detail);
            return StatusCode((int)HttpStatusCode.BadGateway, new ProblemDetails
            {
                Title = "ASSISTANT_SERVICE_ERROR",
                Detail = string.IsNullOrWhiteSpace(ex.Status.Detail) ? "Assistant service failed to process the query." : ex.Status.Detail,
                Status = (int)HttpStatusCode.BadGateway
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error processing assistant query.");
            return StatusCode((int)HttpStatusCode.InternalServerError, new ProblemDetails
            {
                Title = "INTERNAL_SERVER_ERROR",
                Detail = "An unexpected error occurred while processing assistant query.",
                Status = (int)HttpStatusCode.InternalServerError
            });
        }
    }

    private static bool TryParseContextId(string? value, out Guid? parsed)
    {
        parsed = null;
        if (string.IsNullOrWhiteSpace(value))
            return true;
        if (!Guid.TryParse(value, out var result) || result == Guid.Empty)
            return false;
        parsed = result;
        return true;
    }

    private static Guid? ParseOptionalGuid(string value) =>
        Guid.TryParse(value, out var parsed) && parsed != Guid.Empty ? parsed : null;

    private static double ClampScore(double score) =>
        double.IsNaN(score) || double.IsInfinity(score) ? 0d : Math.Clamp(score, 0d, 1d);

    private static string BuildVerifiedShipmentSummary(ShipmentResponse shipment)
    {
        var lines = new List<string>
        {
            $"ShipmentNo: {OrUnknown(shipment.ShipmentNo)}",
            $"ShipmentId: {shipment.Id}"
        };

        if (!string.IsNullOrWhiteSpace(shipment.OrderId))
            lines.Add($"OrderId: {shipment.OrderId}");

        lines.Add($"Status: {OrUnknown(shipment.Status)}");

        if (shipment.CreatedAt is not null)
            lines.Add($"CreatedAt: {FormatTimestamp(shipment.CreatedAt)}");
        if (shipment.UpdatedAt is not null)
            lines.Add($"UpdatedAt: {FormatTimestamp(shipment.UpdatedAt)}");
        if (!string.IsNullOrWhiteSpace(shipment.CustomerName))
            lines.Add($"CustomerName: {shipment.CustomerName}");

        var origin = FormatLocation(shipment.OriginAddress, shipment.OriginCountry);
        if (!string.IsNullOrEmpty(origin))
            lines.Add($"Origin: {origin}");

        var destination = FormatLocation(shipment.DestinationAddress, shipment.DestinationCountry);
        if (!string.IsNullOrEmpty(destination))
            lines.Add($"Destination: {destination}");

        if (!string.IsNullOrWhiteSpace(shipment.TransportMode))
            lines.Add($"TransportMode: {shipment.TransportMode}");
        if (!string.IsNullOrWhiteSpace(shipment.Priority))
            lines.Add($"Priority: {shipment.Priority}");

        if (shipment.EstimatedPickupTime is not null)
            lines.Add($"EstimatedPickupTime: {FormatTimestamp(shipment.EstimatedPickupTime)}");
        if (shipment.EstimatedDeliveryTime is not null)
            lines.Add($"EstimatedDeliveryTime: {FormatTimestamp(shipment.EstimatedDeliveryTime)}");
        if (shipment.ActualPickupTime is not null)
            lines.Add($"ActualPickupTime: {FormatTimestamp(shipment.ActualPickupTime)}");
        if (shipment.ActualDeliveryTime is not null)
            lines.Add($"ActualDeliveryTime: {FormatTimestamp(shipment.ActualDeliveryTime)}");

        if (shipment.CargoItems.Count > 0)
        {
            var cargoItems = shipment.CargoItems
                .Take(10)
                .Select(c =>
                {
                    var hs = string.IsNullOrWhiteSpace(c.HsCode) ? string.Empty : $" (HS: {c.HsCode})";
                    var dg = c.IsDangerousGoods ? " [DG]" : string.Empty;
                    return $"{c.Name} x{c.Quantity}, {c.WeightKg}kg{hs}{dg}";
                });
            lines.Add($"CargoItems ({shipment.CargoItems.Count}): {string.Join("; ", cargoItems)}");
        }

        if (shipment.Documents.Count > 0)
        {
            var docTypes = shipment.Documents
                .Take(10)
                .Select(d => string.IsNullOrWhiteSpace(d.DocumentType) ? d.FileName : d.DocumentType)
                .Where(d => !string.IsNullOrWhiteSpace(d));
            lines.Add($"Documents ({shipment.Documents.Count}): {string.Join(", ", docTypes)}");
        }

        if (shipment.Milestones.Count > 0)
        {
            var latestMilestone = shipment.Milestones
                .OrderByDescending(m => m.RecordedAt?.ToDateTimeOffset() ?? DateTimeOffset.MinValue)
                .First();
            var recorded = latestMilestone.RecordedAt is not null
                ? $" at {FormatTimestamp(latestMilestone.RecordedAt)}"
                : string.Empty;
            var desc = string.IsNullOrWhiteSpace(latestMilestone.Description)
                ? string.Empty
                : $" - {latestMilestone.Description}";
            lines.Add($"LatestMilestone: {latestMilestone.Status}{recorded}{desc}");
        }

        if (!string.IsNullOrWhiteSpace(shipment.Notes))
        {
            var trimmedNotes = shipment.Notes.Trim();
            var cappedNotes = trimmedNotes.Length > 300 ? $"{trimmedNotes[..300]}..." : trimmedNotes;
            lines.Add($"Notes: {cappedNotes}");
        }

        return string.Join('\n', lines);
    }

    private static string FormatTimestamp(Timestamp timestamp)
    {
        var utc = timestamp.ToDateTimeOffset().ToUniversalTime();
        var vn = utc.ToOffset(TimeSpan.FromHours(7));
        return $"{utc:yyyy-MM-dd HH:mm:ss} UTC ({vn:yyyy-MM-dd HH:mm:ss} +07:00)";
    }

    private static string FormatLocation(string? address, string? country)
    {
        var parts = new[] { address?.Trim(), country?.Trim() }
            .Where(part => !string.IsNullOrWhiteSpace(part));
        return string.Join(", ", parts);
    }

    private static string OrUnknown(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "N/A" : value.Trim();
}

public sealed record AssistantQueryRequest(
    string Query,
    string? Mode,                          // REGULATORY | KNOWLEDGE | ALL
    string? JurisdictionCode,
    DateTimeOffset? EffectiveAt,
    IReadOnlyList<int>? RegulationTypes,
    IReadOnlyList<int>? Categories,
    int TopK,
    decimal MinimumScore,
    AssistantContextRequest? Context = null);

public sealed record AssistantContextRequest(string? ShipmentId, string? EvaluationId);

public sealed record AssistantQueryResponse(
    string Query,
    string Answer,
    IReadOnlyList<AssistantRegulatoryCitation> RegulatoryCitations,
    IReadOnlyList<AssistantKnowledgeReference> KnowledgeReferences,
    IReadOnlyList<AssistantConflict> Conflicts,
    bool InsufficientEvidence,
    IReadOnlyList<string> MissingInformation,
    AssistantGovernanceSummary Governance,
    string RetrievalTraceId,
    AssistantContextSummary? Context = null);

public sealed record AssistantRegulatoryCitation(
    string EvidenceId,
    string SourceId,
    string DocumentVersionId,
    string ChunkId,
    string Title,
    string Authority,
    string Jurisdiction,
    string RegulationType,
    string Section,
    string Page,
    string Excerpt,
    string CanonicalSourceUri,
    double Score);

public sealed record AssistantKnowledgeReference(
    string EvidenceId,
    string SourceId,
    string DocumentVersionId,
    string ChunkId,
    string Title,
    string Category,
    string Section,
    string Page,
    string Excerpt,
    double Score);

public sealed record AssistantConflict(
    string RegulatoryEvidenceId,
    string KnowledgeEvidenceId,
    string Description);

public sealed record AssistantGovernanceSummary(
    string DecisionId,
    string AutomationLevel,
    bool RequiresApproval,
    string CapabilityCode,
    long TotalTokens);

public sealed record AssistantContextSummary(
    string ShipmentId,
    string EvaluationId,
    string Freshness,
    string SnapshotHash);

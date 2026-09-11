using System.Net;
using Asp.Versioning;
using BuildingBlocks.BFF.Attributes;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegulatoryCompliance.Grpc;
using Shared.Constants;

namespace StaffBff.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/assistant")]
[Route("api/assistant")]
[Authorize]
[RequirePermission(PermissionConstants.Assistant.Query, PermissionConstants.Compliance.Read)]
public sealed class AssistantController(
    RegulatoryComplianceService.RegulatoryComplianceServiceClient regulatoryClient,
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
                Score: r.Score)).ToList();

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
                Score: k.Score)).ToList();

            var conflicts = response.Conflicts.Select(c => new AssistantConflict(
                RegulatoryEvidenceId: c.RegulatoryEvidenceId,
                KnowledgeEvidenceId: c.KnowledgeEvidenceId,
                Description: c.Description)).ToList();

            var governance = new AssistantGovernanceSummary(
                DecisionId: response.Governance?.DecisionId ?? string.Empty,
                AutomationLevel: response.Governance?.AutomationLevel ?? "ASSISTED",
                RequiresApproval: response.Governance?.RequiresApproval ?? false,
                CapabilityCode: response.Governance?.CapabilityCode ?? "compliance.answer",
                TotalTokens: response.Governance?.TotalTokens ?? 0);

            return Ok(new AssistantQueryResponse(
                Query: response.Query,
                Answer: response.Answer,
                RegulatoryCitations: regCitations,
                KnowledgeReferences: knowReferences,
                Conflicts: conflicts,
                InsufficientEvidence: response.InsufficientEvidence,
                MissingInformation: response.MissingInformation.ToList(),
                Governance: governance,
                RetrievalTraceId: response.RetrievalTraceId));
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
            return StatusCode((int)HttpStatusCode.PreconditionFailed, new ProblemDetails
            {
                Title = "GOVERNANCE_BLOCKED",
                Detail = ex.Status.Detail,
                Status = (int)HttpStatusCode.PreconditionFailed
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
}

public sealed record AssistantQueryRequest(
    string Query,
    string? Mode,                          // REGULATORY | KNOWLEDGE | ALL
    string? JurisdictionCode,
    DateTimeOffset? EffectiveAt,
    IReadOnlyList<int>? RegulationTypes,
    IReadOnlyList<int>? Categories,
    int TopK,
    decimal MinimumScore);

public sealed record AssistantQueryResponse(
    string Query,
    string Answer,
    IReadOnlyList<AssistantRegulatoryCitation> RegulatoryCitations,
    IReadOnlyList<AssistantKnowledgeReference> KnowledgeReferences,
    IReadOnlyList<AssistantConflict> Conflicts,
    bool InsufficientEvidence,
    IReadOnlyList<string> MissingInformation,
    AssistantGovernanceSummary Governance,
    string RetrievalTraceId);

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

using System.Net;
using Asp.Versioning;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegulatoryCompliance.Grpc;

namespace StaffBff.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/assistant")]
[Route("api/assistant")]
[Authorize]
public sealed class AssistantController(
    RegulatoryComplianceService.RegulatoryComplianceServiceClient regulatoryClient)
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

        var mode = (request.Mode ?? "ALL").ToUpperInvariant() switch
        {
            "REGULATORY" => AssistantSearchMode.Regulatory,
            "KNOWLEDGE" => AssistantSearchMode.Knowledge,
            _ => AssistantSearchMode.All
        };

        var rpcRequest = new GenerateGroundedAnswerRequest
        {
            Query = request.Query.Trim(),
            Mode = mode,
            JurisdictionCode = request.JurisdictionCode ?? string.Empty,
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
                rpcRequest.RegulationTypes.Add((RegulationType)t);
            }
        }

        if (request.Categories != null)
        {
            foreach (var c in request.Categories)
            {
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

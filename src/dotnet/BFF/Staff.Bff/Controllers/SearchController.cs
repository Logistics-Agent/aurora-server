using Asp.Versioning;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RegulatoryCompliance.Grpc;

namespace StaffBff.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/search")]
[Route("api/search")]
[Authorize]
public sealed class SearchController(
    RegulatoryComplianceService.RegulatoryComplianceServiceClient regulatoryClient)
    : ControllerBase
{
    /// <summary>
    /// Unified Evidence Search API: Parallel or targeted retrieval across Regulatory and Knowledge corpora.
    /// Modes: REGULATORY | KNOWLEDGE | ALL
    /// Evidence merger preserves distinct domain identities and legal hierarchies without conflating vector scores.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(GroupedUnifiedSearchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromBody] UnifiedSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new ProblemDetails { Title = "INVALID_QUERY", Detail = "Query text is required." });

        var mode = (request.Mode ?? "ALL").ToUpperInvariant();
        var topK = request.TopK > 0 ? request.TopK : 10;
        var minScore = (double)(request.MinimumScore > 0 ? request.MinimumScore : 0.4m);

        Task<QueryRegulationsResponse>? regulatoryTask = null;
        Task<QueryKnowledgeResponse>? knowledgeTask = null;

        if (mode is "REGULATORY" or "ALL")
        {
            var regReq = new QueryRegulationsRequest
            {
                Query = request.Query,
                JurisdictionCode = request.JurisdictionCode ?? string.Empty,
                EffectiveAt = request.EffectiveAt.HasValue
                    ? Timestamp.FromDateTimeOffset(request.EffectiveAt.Value)
                    : Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
                TopK = topK,
                MinimumRelevanceScore = minScore
            };

            if (request.RegulationTypes != null)
            {
                foreach (var t in request.RegulationTypes)
                {
                    regReq.RegulationTypes.Add((RegulationType)t);
                }
            }

            regulatoryTask = regulatoryClient.QueryRegulationsAsync(regReq, cancellationToken: cancellationToken).ResponseAsync;
        }

        if (mode is "KNOWLEDGE" or "ALL")
        {
            var knowReq = new QueryKnowledgeRequest
            {
                Query = request.Query,
                TopK = topK,
                MinimumRelevanceScore = minScore
            };

            if (request.Categories != null)
            {
                foreach (var c in request.Categories)
                {
                    knowReq.Categories.Add((KnowledgeCategory)c);
                }
            }

            knowledgeTask = regulatoryClient.QueryKnowledgeAsync(knowReq, cancellationToken: cancellationToken).ResponseAsync;
        }

        if (regulatoryTask != null) await regulatoryTask;
        if (knowledgeTask != null) await knowledgeTask;

        var regulatoryResults = new List<RegulatorySearchResultItem>();
        var knowledgeResults = new List<KnowledgeSearchResultItem>();

        if (regulatoryTask?.Result != null)
        {
            foreach (var item in regulatoryTask.Result.Evidence)
            {
                regulatoryResults.Add(new RegulatorySearchResultItem(
                    Domain: "REGULATORY",
                    SourceId: item.Citation.RegulatoryDocumentId,
                    DocumentVersionId: item.Citation.DocumentVersionId,
                    ChunkId: item.Citation.ChunkId,
                    Title: item.Citation.Title,
                    Authority: item.Citation.Authority,
                    Jurisdiction: item.JurisdictionCode,
                    RegulationType: item.RegulationType.ToString(),
                    Section: item.Citation.SectionLabel,
                    Page: item.Citation.PageLabel,
                    Excerpt: item.Citation.Excerpt,
                    Score: item.Citation.RelevanceScore,
                    Citation: new CitationDetails(item.Citation.DocumentVersionId, item.Citation.ChunkId, item.Citation.CanonicalSourceUri)));
            }
        }

        if (knowledgeTask?.Result != null)
        {
            foreach (var item in knowledgeTask.Result.Evidence)
            {
                knowledgeResults.Add(new KnowledgeSearchResultItem(
                    Domain: "KNOWLEDGE",
                    SourceId: item.KnowledgeDocumentId,
                    DocumentVersionId: item.DocumentVersionId,
                    ChunkId: item.ChunkId,
                    Title: item.Title,
                    Category: item.Category.ToString(),
                    Section: item.SectionLabel,
                    Page: item.PageLabel,
                    Excerpt: item.Excerpt,
                    Score: item.RelevanceScore,
                    Reference: new CitationDetails(item.DocumentVersionId, item.ChunkId, string.Empty)));
            }
        }

        var totalCount = regulatoryResults.Count + knowledgeResults.Count;

        return Ok(new GroupedUnifiedSearchResponse(
            Query: request.Query,
            Mode: mode,
            TotalResults: totalCount,
            Regulatory: regulatoryResults,
            Knowledge: knowledgeResults));
    }
}

public sealed record UnifiedSearchRequest(
    string Query,
    string? Mode,                          // REGULATORY | KNOWLEDGE | ALL
    string? JurisdictionCode,
    DateTimeOffset? EffectiveAt,
    IReadOnlyList<int>? RegulationTypes,
    IReadOnlyList<int>? Categories,
    int TopK,
    decimal MinimumScore);

public sealed record GroupedUnifiedSearchResponse(
    string Query,
    string Mode,
    int TotalResults,
    IReadOnlyList<RegulatorySearchResultItem> Regulatory,
    IReadOnlyList<KnowledgeSearchResultItem> Knowledge);

public sealed record RegulatorySearchResultItem(
    string Domain,
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
    double Score,
    CitationDetails Citation);

public sealed record KnowledgeSearchResultItem(
    string Domain,
    string SourceId,
    string DocumentVersionId,
    string ChunkId,
    string Title,
    string Category,
    string Section,
    string Page,
    string Excerpt,
    double Score,
    CitationDetails Reference);

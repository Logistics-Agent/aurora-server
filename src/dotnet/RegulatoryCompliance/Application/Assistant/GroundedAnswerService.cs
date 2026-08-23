using System.Text.Json;
using System.Text.RegularExpressions;
using AiGovernance.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using RegulatoryCompliance.Application.Ingestion;
using RegulatoryCompliance.Application.Retrieval;
using RegulatoryCompliance.Domain.Enums;
using RegulatoryCompliance.Infrastructure.Persistences;
using Shared.Security;

namespace RegulatoryCompliance.Application.Assistant;

public enum AssistantSearchMode
{
    All = 1,
    Regulatory = 2,
    Knowledge = 3
}

public sealed record GroundedAnswerInput(
    string Query,
    AssistantSearchMode Mode,
    string? JurisdictionCode,
    DateTimeOffset? EffectiveAt,
    IReadOnlyCollection<RegulationType>? RegulationTypes,
    IReadOnlyCollection<KnowledgeCategory>? KnowledgeCategories,
    int TopK = 10,
    decimal MinimumRelevanceScore = 0.4m);

public sealed record GroundedAnswerResult(
    string Query,
    string Answer,
    IReadOnlyList<RegulatoryCitationResult> RegulatoryCitations,
    IReadOnlyList<KnowledgeReferenceResult> KnowledgeReferences,
    IReadOnlyList<GroundedConflictResult> Conflicts,
    bool InsufficientEvidence,
    IReadOnlyList<string> MissingInformation,
    AssistantGovernanceResult Governance,
    Guid RetrievalTraceId);

public sealed record RegulatoryCitationResult(
    string EvidenceId,
    Guid SourceId,
    Guid DocumentVersionId,
    Guid ChunkId,
    string Title,
    string Authority,
    string Jurisdiction,
    string RegulationType,
    string? Section,
    string? Page,
    string Excerpt,
    string? CanonicalSourceUri,
    double Score);

public sealed record KnowledgeReferenceResult(
    string EvidenceId,
    Guid SourceId,
    Guid DocumentVersionId,
    Guid ChunkId,
    string Title,
    string Category,
    string? Section,
    string? Page,
    string Excerpt,
    double Score);

public sealed record GroundedConflictResult(
    string RegulatoryEvidenceId,
    string KnowledgeEvidenceId,
    string Description);

public sealed record AssistantGovernanceResult(
    string DecisionId,
    string AutomationLevel,
    bool RequiresApproval,
    string CapabilityCode,
    long TotalTokens);

public interface IGroundedAnswerService
{
    Task<GroundedAnswerResult> GenerateAnswerAsync(
        GroundedAnswerInput input,
        CancellationToken cancellationToken = default);
}

public sealed class GroundedAnswerService(
    IRegulationRetrievalService regulationRetrievalService,
    IKnowledgeIngestionService knowledgeIngestionService,
    AiExecutionService.AiExecutionServiceClient aiExecutionClient,
    IGroundedAnswerPromptBuilder promptBuilder,
    IDeterministicCitationValidator citationValidator,
    ICurrentUserService currentUser,
    ILogger<GroundedAnswerService> logger) : IGroundedAnswerService
{
    private const string CapabilityCode = "compliance.answer";

    public async Task<GroundedAnswerResult> GenerateAnswerAsync(
        GroundedAnswerInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (string.IsNullOrWhiteSpace(input.Query))
            throw new ArgumentException("Query cannot be empty.", nameof(input.Query));

        var traceId = Guid.NewGuid();
        var topK = Math.Clamp(input.TopK, 1, 20);
        var minScore = input.MinimumRelevanceScore > 0 ? input.MinimumRelevanceScore : 0.4m;
        var effectiveAt = input.EffectiveAt ?? DateTimeOffset.UtcNow;
        var jurisdiction = input.JurisdictionCode ?? string.Empty;

        Task<RegulationQueryResult>? regTask = null;
        Task<IReadOnlyList<KnowledgeEvidenceResult>>? knowTask = null;

        // 1. Parallel Domain Retrieval
        if (input.Mode is AssistantSearchMode.All or AssistantSearchMode.Regulatory)
        {
            var regTypes = input.RegulationTypes != null && input.RegulationTypes.Count > 0
                ? input.RegulationTypes
                : [RegulationType.ImportRestriction, RegulationType.ExportRestriction, RegulationType.DangerousGoods, RegulationType.RequiredDocument, RegulationType.Customs];

            regTask = regulationRetrievalService.QueryAsync(
                new RegulationQueryInput(
                    input.Query,
                    jurisdiction,
                    effectiveAt,
                    "vi",
                    regTypes,
                    topK,
                    minScore,
                    PersistTrace: true),
                cancellationToken);
        }

        if (input.Mode is AssistantSearchMode.All or AssistantSearchMode.Knowledge)
        {
            var categories = input.KnowledgeCategories?.ToList() ?? [];
            knowTask = knowledgeIngestionService.QueryAsync(
                input.Query,
                categories,
                topK,
                minScore,
                cancellationToken);
        }

        if (regTask != null) await regTask;
        if (knowTask != null) await knowTask;

        // 2. Build GroundedEvidence Collections with Stable IDs (R1..Rn, K1..Km)
        var regEvidenceList = new List<GroundedEvidence>();
        if (regTask?.Result?.Evidence != null)
        {
            var idx = 1;
            foreach (var item in regTask.Result.Evidence)
            {
                regEvidenceList.Add(new GroundedEvidence(
                    EvidenceId: $"R{idx++}",
                    Domain: GroundedEvidenceDomain.Regulatory,
                    SourceId: item.RegulatoryDocumentId,
                    DocumentVersionId: item.DocumentVersionId,
                    ChunkId: item.ChunkId,
                    Title: item.Title,
                    SectionLabel: item.SectionLabel,
                    PageLabel: item.PageLabel,
                    Excerpt: item.Excerpt,
                    RelevanceScore: item.RelevanceScore,
                    Authority: item.Authority,
                    JurisdictionCode: item.JurisdictionCode,
                    RegulationType: item.RegulationType.ToString(),
                    CanonicalSourceUri: item.CanonicalSourceUri));
            }
        }

        var knowEvidenceList = new List<GroundedEvidence>();
        if (knowTask?.Result != null)
        {
            var idx = 1;
            foreach (var item in knowTask.Result)
            {
                knowEvidenceList.Add(new GroundedEvidence(
                    EvidenceId: $"K{idx++}",
                    Domain: GroundedEvidenceDomain.Knowledge,
                    SourceId: item.KnowledgeDocumentId,
                    DocumentVersionId: item.DocumentVersionId,
                    ChunkId: item.ChunkId,
                    Title: item.Title,
                    SectionLabel: item.SectionLabel,
                    PageLabel: item.PageLabel,
                    Excerpt: item.Excerpt,
                    RelevanceScore: item.RelevanceScore,
                    KnowledgeCategory: item.Category.ToString()));
            }
        }

        var evidenceContext = new EvidenceContext(regEvidenceList, knowEvidenceList);

        // 3. Short-circuit if No Evidence Exists (Cost Optimization & Anti-Hallucination)
        if (evidenceContext.IsEmpty)
        {
            logger.LogInformation("No evidence found for query '{Query}'. Skipping LLM generation.", input.Query);

            return new GroundedAnswerResult(
                Query: input.Query,
                Answer: "No authoritative regulatory sources or tenant company knowledge were found matching your query.",
                RegulatoryCitations: [],
                KnowledgeReferences: [],
                Conflicts: [],
                InsufficientEvidence: true,
                MissingInformation: ["No applicable regulatory source or company SOP found matching the specified parameters."],
                Governance: new AssistantGovernanceResult("none", "DETERMINISTIC_FALLBACK", false, CapabilityCode, 0),
                RetrievalTraceId: traceId);
        }

        // 4. Construct Governed Prompt
        var prompt = promptBuilder.BuildPrompt(input.Query, evidenceContext);

        // 5. Call AiGovernance.Generate
        var generateRequest = new AiGenerateRequest
        {
            CapabilityCode = CapabilityCode,
            Prompt = prompt,
            MaxOutputTokens = 2048,
            EstimatedInputTokens = Math.Max(100, prompt.Length / 4)
        };

        var headers = new Metadata
        {
            { "x-service-id", "regulatory-compliance-rag" }
        };

        if (currentUser.TenantId.HasValue)
            headers.Add("x-tenant-id", currentUser.TenantId.Value.ToString());
        if (currentUser.UserId.HasValue)
            headers.Add("x-user-id", currentUser.UserId.Value.ToString());
        if (!string.IsNullOrEmpty(currentUser.TraceId))
            headers.Add("x-trace-id", currentUser.TraceId);

        AiGenerateResponse generateResponse;
        try
        {
            generateResponse = await aiExecutionClient.GenerateAsync(
                generateRequest,
                headers,
                deadline: DateTime.UtcNow.AddSeconds(45),
                cancellationToken: cancellationToken);
        }
        catch (RpcException ex)
        {
            logger.LogError(ex, "AiGovernance.Generate failed for assistant query.");
            throw;
        }

        // 6. Parse Structured Output
        var parsedLlm = ParseLlmResponse(generateResponse.Content);

        // 7. Deterministic Citation Validation
        var validated = citationValidator.Validate(parsedLlm, evidenceContext);

        // 8. Map to Final Grounded Result
        var mappedRegCitations = validated.ValidatedRegulatoryCitations.Select(r => new RegulatoryCitationResult(
            EvidenceId: r.EvidenceId,
            SourceId: r.SourceId,
            DocumentVersionId: r.DocumentVersionId,
            ChunkId: r.ChunkId,
            Title: r.Title,
            Authority: r.Authority ?? string.Empty,
            Jurisdiction: r.JurisdictionCode ?? string.Empty,
            RegulationType: r.RegulationType ?? string.Empty,
            Section: r.SectionLabel,
            Page: r.PageLabel,
            Excerpt: r.Excerpt,
            CanonicalSourceUri: r.CanonicalSourceUri,
            Score: Convert.ToDouble(r.RelevanceScore))).ToList();

        var mappedKnowReferences = validated.ValidatedKnowledgeReferences.Select(k => new KnowledgeReferenceResult(
            EvidenceId: k.EvidenceId,
            SourceId: k.SourceId,
            DocumentVersionId: k.DocumentVersionId,
            ChunkId: k.ChunkId,
            Title: k.Title,
            Category: k.KnowledgeCategory ?? string.Empty,
            Section: k.SectionLabel,
            Page: k.PageLabel,
            Excerpt: k.Excerpt,
            Score: Convert.ToDouble(k.RelevanceScore))).ToList();

        var mappedConflicts = validated.ValidatedConflicts.Select(c => new GroundedConflictResult(
            RegulatoryEvidenceId: c.RegulatoryEvidence.EvidenceId,
            KnowledgeEvidenceId: c.KnowledgeEvidence.EvidenceId,
            Description: c.Description)).ToList();

        var governanceResult = new AssistantGovernanceResult(
            DecisionId: generateResponse.DecisionId,
            AutomationLevel: generateResponse.AutomationLevel,
            RequiresApproval: generateResponse.RequiresApproval,
            CapabilityCode: CapabilityCode,
            TotalTokens: generateResponse.InputTokens + generateResponse.OutputTokens);

        return new GroundedAnswerResult(
            Query: input.Query,
            Answer: validated.Answer,
            RegulatoryCitations: mappedRegCitations,
            KnowledgeReferences: mappedKnowReferences,
            Conflicts: mappedConflicts,
            InsufficientEvidence: validated.InsufficientEvidence,
            MissingInformation: validated.MissingInformation,
            Governance: governanceResult,
            RetrievalTraceId: traceId);
    }

    private static LlmParsedResponse ParseLlmResponse(string rawContent)
    {
        if (string.IsNullOrWhiteSpace(rawContent))
            return new LlmParsedResponse(string.Empty, [], [], [], true, ["LLM returned empty content."]);

        var clean = rawContent.Trim();

        // Strip markdown code fences if model returned ```json ... ```
        if (clean.StartsWith("```"))
        {
            var match = Regex.Match(clean, @"```(?:json)?\s*(.*?)\s*```", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (match.Success)
            {
                clean = match.Groups[1].Value.Trim();
            }
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            return JsonSerializer.Deserialize<LlmParsedResponse>(clean, options)
                   ?? new LlmParsedResponse(rawContent, [], [], [], false, []);
        }
        catch (JsonException)
        {
            // Fallback: If model did not produce valid JSON, use raw prose as answer
            return new LlmParsedResponse(rawContent, [], [], [], false, []);
        }
    }
}

using System.Text.Json;
using System.Text.RegularExpressions;
using AiGovernance.Grpc;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using RegulatoryCompliance.Application.Ingestion;
using RegulatoryCompliance.Application.Retrieval;
using RegulatoryCompliance.Application.Evaluations;
using RegulatoryCompliance.Domain.Entities;
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
    decimal MinimumRelevanceScore = 0.4m,
    VerifiedAssistantContextInput? Context = null);

public sealed record VerifiedAssistantContextInput(Guid? ShipmentId, Guid? EvaluationId);

public sealed record VerifiedAssistantContextSummary(
    Guid? ShipmentId,
    Guid? EvaluationId,
    string Freshness,
    string? SnapshotHash);

public sealed record VerifiedAssistantFinding(
    string Code,
    string Title,
    string Description,
    string Severity);

public sealed record VerifiedAssistantContext(
    Guid? ShipmentId,
    Guid? EvaluationId,
    string Freshness,
    string? SnapshotHash,
    ComplianceRiskLevel? RiskLevel,
    EvidenceSufficiency? EvidenceSufficiency,
    IReadOnlyList<VerifiedAssistantFinding> Findings);

public sealed class AssistantContextMismatchException(string message) : Exception(message);

public sealed class AssistantContextUnavailableException(string message) : Exception(message);

public sealed record GroundedAnswerResult(
    string Query,
    string Answer,
    IReadOnlyList<RegulatoryCitationResult> RegulatoryCitations,
    IReadOnlyList<KnowledgeReferenceResult> KnowledgeReferences,
    IReadOnlyList<GroundedConflictResult> Conflicts,
    bool InsufficientEvidence,
    IReadOnlyList<string> MissingInformation,
    AssistantGovernanceResult Governance,
    Guid RetrievalTraceId,
    VerifiedAssistantContextSummary? Context = null);

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
    ILogger<GroundedAnswerService> logger,
    IComplianceEvaluationService? complianceEvaluationService = null) : IGroundedAnswerService
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
        var verifiedContext = await ResolveContextAsync(input.Context, cancellationToken);

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
            if (verifiedContext is not null)
                return BuildDeterministicFallback(input.Query, evidenceContext, traceId, verifiedContext);

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
                RetrievalTraceId: traceId,
                Context: null);
        }

        // 4. Construct Governed Prompt
        var prompt = promptBuilder.BuildPrompt(input.Query, evidenceContext, verifiedContext);

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

        AiGenerateResponse? generateResponse = null;
        try
        {
            if (aiExecutionClient != null)
            {
                generateResponse = await aiExecutionClient.GenerateAsync(
                    generateRequest,
                    headers,
                    deadline: DateTime.UtcNow.AddSeconds(45),
                    cancellationToken: cancellationToken);
            }
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.PermissionDenied)
        {
            logger.LogWarning(ex, "AiGovernance denied generation due to policy: {Detail}", ex.Status.Detail);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AiGovernance.Generate call failed or is unavailable for assistant query. Performing deterministic evidence grounding.");
        }

        if (generateResponse != null && !string.IsNullOrWhiteSpace(generateResponse.Content))
        {
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
                RetrievalTraceId: traceId,
                Context: ToSummary(verifiedContext));
        }

        // Fallback: Deterministic grounding directly synthesized from evidenceContext
        return BuildDeterministicFallback(input.Query, evidenceContext, traceId, verifiedContext);
    }

    private async Task<VerifiedAssistantContext?> ResolveContextAsync(
        VerifiedAssistantContextInput? input,
        CancellationToken cancellationToken)
    {
        if (input is null || (input.ShipmentId is null && input.EvaluationId is null))
            return null;

        if (input.ShipmentId is Guid shipmentId && shipmentId == Guid.Empty ||
            input.EvaluationId is Guid evaluationId && evaluationId == Guid.Empty)
            throw new AssistantContextMismatchException("Assistant context identifiers must not be empty.");

        if (input.EvaluationId is not Guid requestedEvaluationId)
        {
            return new VerifiedAssistantContext(
                input.ShipmentId,
                null,
                "CURRENT",
                null,
                null,
                null,
                []);
        }

        if (complianceEvaluationService is null)
            throw new AssistantContextUnavailableException(
                "ASSISTANT_CONTEXT_UNAVAILABLE: Verified compliance context is unavailable.");

        ComplianceEvaluation evaluation;
        try
        {
            evaluation = await complianceEvaluationService.GetAsync(requestedEvaluationId, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            throw new AssistantContextUnavailableException(
                "ASSISTANT_CONTEXT_UNAVAILABLE: Verified compliance evaluation was not found.");
        }

        if (evaluation.Status != ComplianceEvaluationStatus.Completed)
            throw new AssistantContextUnavailableException(
                "ASSISTANT_CONTEXT_UNAVAILABLE: The compliance evaluation is not completed.");

        if (input.ShipmentId is Guid requestedShipmentId &&
            evaluation.ExternalShipmentId != requestedShipmentId)
            throw new AssistantContextMismatchException(
                "The compliance evaluation does not belong to the requested shipment.");

        return new VerifiedAssistantContext(
            input.ShipmentId ?? evaluation.ExternalShipmentId,
            evaluation.Id,
            "CURRENT",
            evaluation.RequestHash,
            evaluation.RiskLevel,
            evaluation.EvidenceSufficiency,
            evaluation.Findings
                .Select(finding => new VerifiedAssistantFinding(
                    finding.Code,
                    finding.Title,
                    finding.Description,
                    finding.Severity.ToString()))
                .ToArray());
    }

    private static VerifiedAssistantContextSummary? ToSummary(VerifiedAssistantContext? context) =>
        context is null
            ? null
            : new VerifiedAssistantContextSummary(
                context.ShipmentId,
                context.EvaluationId,
                context.Freshness,
                context.SnapshotHash);

    private static GroundedAnswerResult BuildDeterministicFallback(
        string query,
        EvidenceContext evidenceContext,
        Guid traceId,
        VerifiedAssistantContext? verifiedContext = null)
    {
        var regCitations = evidenceContext.RegulatoryEvidence.Select(r => new RegulatoryCitationResult(
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

        var knowReferences = evidenceContext.KnowledgeEvidence.Select(k => new KnowledgeReferenceResult(
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

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Dưới đây là các tài liệu và bằng chứng đã được tìm thấy liên quan đến câu hỏi của bạn:\n");

        if (verifiedContext is not null)
        {
            sb.AppendLine($"Đánh giá tuân thủ đã xác minh: {verifiedContext.EvaluationId?.ToString() ?? "chưa có"}.");
            sb.AppendLine($"Mức rủi ro đã lưu: {verifiedContext.RiskLevel?.ToString() ?? "chưa xác định"}; trạng thái dữ liệu: {verifiedContext.Freshness}.");
            foreach (var finding in verifiedContext.Findings)
                sb.AppendLine($"- {finding.Code}: {finding.Title} — {finding.Description}");
            sb.AppendLine();
        }

        if (knowReferences.Count > 0)
        {
            sb.AppendLine("### Tài liệu & Quy trình nội bộ (Knowledge Base):");
            foreach (var k in knowReferences)
            {
                sb.AppendLine($"- **[{k.EvidenceId}] {k.Title}** ({k.Category} - {k.Section ?? "Chung"}):");
                sb.AppendLine($"  {k.Excerpt.Trim()}\n");
            }
        }

        if (regCitations.Count > 0)
        {
            sb.AppendLine("### Quy định pháp lý & Tuân thủ (Regulatory):");
            foreach (var r in regCitations)
            {
                sb.AppendLine($"- **[{r.EvidenceId}] {r.Title}** ({r.Authority} - {r.Jurisdiction}):");
                sb.AppendLine($"  {r.Excerpt.Trim()}\n");
            }
        }

        return new GroundedAnswerResult(
            Query: query,
            Answer: sb.ToString().Trim(),
            RegulatoryCitations: regCitations,
            KnowledgeReferences: knowReferences,
            Conflicts: [],
            InsufficientEvidence: verifiedContext?.EvaluationId is null,
            MissingInformation: [],
            Governance: new AssistantGovernanceResult("deterministic-fallback-" + traceId.ToString("N"), "DETERMINISTIC_FALLBACK", false, CapabilityCode, 0),
            RetrievalTraceId: traceId,
            Context: ToSummary(verifiedContext));
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
            // Do not expose unvalidated model prose as a grounded answer.
            return new LlmParsedResponse(
                "The assistant response could not be validated against the retrieved evidence.",
                [],
                [],
                [],
                true,
                ["The AI response was not valid structured output."]);
        }
    }
}

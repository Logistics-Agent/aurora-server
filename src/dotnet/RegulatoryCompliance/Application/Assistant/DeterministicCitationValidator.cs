using System.Text.Json.Serialization;

namespace RegulatoryCompliance.Application.Assistant;

public sealed record LlmParsedResponse(
    [property: JsonPropertyName("answer")] string? Answer,
    [property: JsonPropertyName("citations")] IReadOnlyList<LlmCitationItem>? Citations,
    [property: JsonPropertyName("knowledgeReferences")] IReadOnlyList<LlmKnowledgeItem>? KnowledgeReferences,
    [property: JsonPropertyName("conflicts")] IReadOnlyList<LlmConflictItem>? Conflicts,
    [property: JsonPropertyName("insufficientEvidence")] bool InsufficientEvidence,
    [property: JsonPropertyName("missingInformation")] IReadOnlyList<string>? MissingInformation);

public sealed record LlmCitationItem(
    [property: JsonPropertyName("evidenceId")] string? EvidenceId);

public sealed record LlmKnowledgeItem(
    [property: JsonPropertyName("evidenceId")] string? EvidenceId);

public sealed record LlmConflictItem(
    [property: JsonPropertyName("regulatoryEvidenceId")] string? RegulatoryEvidenceId,
    [property: JsonPropertyName("knowledgeEvidenceId")] string? KnowledgeEvidenceId,
    [property: JsonPropertyName("description")] string? Description);

public sealed record ValidatedGroundedResult(
    string Answer,
    IReadOnlyList<GroundedEvidence> ValidatedRegulatoryCitations,
    IReadOnlyList<GroundedEvidence> ValidatedKnowledgeReferences,
    IReadOnlyList<ValidatedConflict> ValidatedConflicts,
    bool InsufficientEvidence,
    IReadOnlyList<string> MissingInformation);

public sealed record ValidatedConflict(
    GroundedEvidence RegulatoryEvidence,
    GroundedEvidence KnowledgeEvidence,
    string Description);

public interface IDeterministicCitationValidator
{
    ValidatedGroundedResult Validate(LlmParsedResponse rawLlm, EvidenceContext context);
}

public sealed class DeterministicCitationValidator : IDeterministicCitationValidator
{
    public ValidatedGroundedResult Validate(LlmParsedResponse rawLlm, EvidenceContext context)
    {
        ArgumentNullException.ThrowIfNull(rawLlm);
        ArgumentNullException.ThrowIfNull(context);

        var answer = rawLlm.Answer?.Trim() ?? string.Empty;
        var validRegCitations = new List<GroundedEvidence>();
        var validKnowReferences = new List<GroundedEvidence>();
        var validConflicts = new List<ValidatedConflict>();
        var missingInfo = new List<string>(rawLlm.MissingInformation ?? []);

        // 1. Validate Regulatory Citations
        if (rawLlm.Citations != null)
        {
            foreach (var item in rawLlm.Citations)
            {
                if (string.IsNullOrWhiteSpace(item.EvidenceId))
                    continue;

                var evidence = context.Find(item.EvidenceId);
                // Must exist in provided context and must be REGULATORY domain
                if (evidence != null && evidence.Domain == GroundedEvidenceDomain.Regulatory)
                {
                    if (!validRegCitations.Any(r => r.EvidenceId.Equals(evidence.EvidenceId, StringComparison.OrdinalIgnoreCase)))
                    {
                        validRegCitations.Add(evidence);
                    }
                }
            }
        }

        // 2. Validate Knowledge References
        if (rawLlm.KnowledgeReferences != null)
        {
            foreach (var item in rawLlm.KnowledgeReferences)
            {
                if (string.IsNullOrWhiteSpace(item.EvidenceId))
                    continue;

                var evidence = context.Find(item.EvidenceId);
                // Must exist in provided context and must be KNOWLEDGE domain
                if (evidence != null && evidence.Domain == GroundedEvidenceDomain.Knowledge)
                {
                    if (!validKnowReferences.Any(k => k.EvidenceId.Equals(evidence.EvidenceId, StringComparison.OrdinalIgnoreCase)))
                    {
                        validKnowReferences.Add(evidence);
                    }
                }
            }
        }

        // 3. Validate Conflicts
        if (rawLlm.Conflicts != null)
        {
            foreach (var conf in rawLlm.Conflicts)
            {
                if (string.IsNullOrWhiteSpace(conf.RegulatoryEvidenceId) ||
                    string.IsNullOrWhiteSpace(conf.KnowledgeEvidenceId))
                    continue;

                var regEvidence = context.Find(conf.RegulatoryEvidenceId);
                var knowEvidence = context.Find(conf.KnowledgeEvidenceId);

                if (regEvidence != null && regEvidence.Domain == GroundedEvidenceDomain.Regulatory &&
                    knowEvidence != null && knowEvidence.Domain == GroundedEvidenceDomain.Knowledge)
                {
                    validConflicts.Add(new ValidatedConflict(
                        regEvidence,
                        knowEvidence,
                        conf.Description?.Trim() ?? "Potential conflict between regulatory requirement and internal procedure."));
                }
            }
        }

        var isInsufficient = rawLlm.InsufficientEvidence;
        if (string.IsNullOrWhiteSpace(answer) && validRegCitations.Count == 0 && validKnowReferences.Count == 0)
        {
            isInsufficient = true;
            if (missingInfo.Count == 0)
                missingInfo.Add("No authoritative evidence found to formulate an answer.");
        }

        return new ValidatedGroundedResult(
            answer,
            validRegCitations.AsReadOnly(),
            validKnowReferences.AsReadOnly(),
            validConflicts.AsReadOnly(),
            isInsufficient,
            missingInfo.AsReadOnly());
    }
}

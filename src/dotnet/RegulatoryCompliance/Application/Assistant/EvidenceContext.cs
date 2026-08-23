namespace RegulatoryCompliance.Application.Assistant;

public sealed class EvidenceContext
{
    public const int DefaultMaxRegulatoryEvidence = 8;
    public const int DefaultMaxKnowledgeEvidence = 6;
    public const int DefaultMaxEvidenceCharacters = 16_000;

    private readonly Dictionary<string, GroundedEvidence> _evidenceById = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<GroundedEvidence> RegulatoryEvidence { get; }
    public IReadOnlyList<GroundedEvidence> KnowledgeEvidence { get; }

    public int TotalCount => RegulatoryEvidence.Count + KnowledgeEvidence.Count;
    public bool IsEmpty => TotalCount == 0;

    public EvidenceContext(
        IEnumerable<GroundedEvidence> regulatoryEvidence,
        IEnumerable<GroundedEvidence> knowledgeEvidence,
        int maxRegulatory = DefaultMaxRegulatoryEvidence,
        int maxKnowledge = DefaultMaxKnowledgeEvidence,
        int maxTotalCharacters = DefaultMaxEvidenceCharacters)
    {
        ArgumentNullException.ThrowIfNull(regulatoryEvidence);
        ArgumentNullException.ThrowIfNull(knowledgeEvidence);

        var regList = new List<GroundedEvidence>();
        var knowList = new List<GroundedEvidence>();
        var currentChars = 0;

        // 1. Process Regulatory Evidence (Priority)
        foreach (var item in regulatoryEvidence.Take(maxRegulatory))
        {
            var itemLength = item.Excerpt.Length + item.Title.Length + 100;
            if (currentChars + itemLength > maxTotalCharacters && regList.Count > 0)
                break;

            regList.Add(item);
            _evidenceById[item.EvidenceId] = item;
            currentChars += itemLength;
        }

        // 2. Process Knowledge Evidence
        foreach (var item in knowledgeEvidence.Take(maxKnowledge))
        {
            var itemLength = item.Excerpt.Length + item.Title.Length + 100;
            if (currentChars + itemLength > maxTotalCharacters && knowList.Count > 0)
                break;

            knowList.Add(item);
            _evidenceById[item.EvidenceId] = item;
            currentChars += itemLength;
        }

        RegulatoryEvidence = regList.AsReadOnly();
        KnowledgeEvidence = knowList.AsReadOnly();
    }

    public GroundedEvidence? Find(string evidenceId)
    {
        if (string.IsNullOrWhiteSpace(evidenceId))
            return null;

        return _evidenceById.TryGetValue(evidenceId.Trim(), out var evidence) ? evidence : null;
    }

    public bool Contains(string evidenceId)
    {
        if (string.IsNullOrWhiteSpace(evidenceId))
            return false;

        return _evidenceById.ContainsKey(evidenceId.Trim());
    }
}

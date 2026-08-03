package com.aurora.devopsagent.Domain.ValueObject;

import java.time.Instant;

public record KnowledgeEntry(
    String knowledgeId,
    String title,
    String rootCauseSummary,
    String remediationSummary,
    double relevanceScore,
    Instant lastVerifiedAt
) {}

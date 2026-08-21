package com.aurora.devopsagent.Domain.ValueObject;

public enum RecommendationSource {
    RULE_ENGINE,    // Deterministic rule match
    AI_RCA,         // LLM-generated analysis
    HUMAN_OVERRIDE  // Manually created by operator
}

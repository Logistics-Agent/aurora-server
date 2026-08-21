package com.aurora.devopsagent.Domain.ValueObject;

public record MatchingRule(
    String ruleId,
    String ruleName,
    String actionType,
    double matchConfidence
) {}

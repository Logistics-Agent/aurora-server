package com.aurora.devopsagent.Domain.ValueObject;

public record RunbookRef(
    String runbookId,
    String title,
    String url,
    double relevanceScore
) {}

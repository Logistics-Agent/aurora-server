package com.aurora.devopsagent.Domain.ValueObject;

import java.time.Instant;

public record TimelineEvent(
    Instant timestamp,
    String sourceType,                   // "log", "metric", "k8s_event", "deployment", "business_event"
    String summary,
    double relevanceScore,
    String rawData                       // For AI consumption (redacted)
) {}

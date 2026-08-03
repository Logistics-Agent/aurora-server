package com.aurora.devopsagent.Domain.ValueObject;

import java.time.Instant;

public record EvidenceRef(
    String sourceType,                  // "log", "metric", "k8s_event", "deployment", "trace", "rag"
    String summary,                     // One-line summary
    double relevanceScore,              // 0.0 – 1.0
    Instant timestamp,
    String rawRef                       // Optional: pointer to original data
) {}

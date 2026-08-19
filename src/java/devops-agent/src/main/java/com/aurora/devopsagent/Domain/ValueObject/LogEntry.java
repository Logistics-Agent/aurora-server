package com.aurora.devopsagent.Domain.ValueObject;

import java.time.Instant;

public record LogEntry(
    Instant timestamp,
    String level,                        // ERROR, WARN, INFO
    String message,                      // Redacted
    String service,
    String namespace,
    String source,                       // "loki", "azure_monitor"
    double relevanceScore
) {}

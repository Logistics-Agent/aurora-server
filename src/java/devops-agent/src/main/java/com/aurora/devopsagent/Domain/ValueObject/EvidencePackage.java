package com.aurora.devopsagent.Domain.ValueObject;

import java.util.List;

public record EvidencePackage(
    List<LogEntry> logs,                 // Filtered, redacted, ranked
    List<MetricSnapshot> metrics,        // Aggregated time-series snapshots
    List<TraceSpan> traces,              // Distributed trace spans
    List<KubernetesEvent> clusterEvents, // K8s Warning events
    PodStatus podStatus                  // Current pod state (if applicable)
) {}

package com.aurora.devopsagent.Domain.ValueObject;

import java.time.Instant;

public record MetricSnapshot(
    String metricName,
    Instant timestamp,
    double value,
    double baselineValue,                // Normal value for comparison
    double deviationPercent,             // How far from baseline
    String unit                          // "percent", "bytes", "ms", "count"
) {}

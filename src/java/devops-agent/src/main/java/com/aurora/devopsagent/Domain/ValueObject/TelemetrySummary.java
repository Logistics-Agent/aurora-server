package com.aurora.devopsagent.Domain.ValueObject;

public record TelemetrySummary(
    double cpuPercent,
    double memoryPercent,
    double diskPercent,
    double latencyP95Ms,
    double latencyP99Ms,
    double requestsPerSecond,
    double errorRatePercent,
    int podRestartCount,
    int activeReplicas,
    int desiredReplicas
) {}

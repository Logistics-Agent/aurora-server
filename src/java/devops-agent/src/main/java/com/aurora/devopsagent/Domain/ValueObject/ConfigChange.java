package com.aurora.devopsagent.Domain.ValueObject;

import java.time.Instant;

public record ConfigChange(
    Instant timestamp,
    String configType,                   // ConfigMap, Secret, HelmValues
    String configName,
    String changedBy,
    String diffSummary                   // Redacted summary
) {}

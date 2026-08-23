package com.aurora.devopsagent.Domain.ValueObject;

import java.util.Map;

public record ConfigSnapshot(
    String serviceName,
    Map<String, String> activeConfig,    // Redacted key-value pairs
    String helmChartVersion,
    String imageTag
) {}

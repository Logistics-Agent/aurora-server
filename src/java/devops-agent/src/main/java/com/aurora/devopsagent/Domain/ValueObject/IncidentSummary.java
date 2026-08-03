package com.aurora.devopsagent.Domain.ValueObject;

import com.aurora.devopsagent.Domain.Enums.Severity;

public record IncidentSummary(
    String correlationId,
    String errorSignature,
    Severity severity,
    String source,
    String affectedService,
    String affectedNamespace,
    String affectedTenantId,
    double impactScore,
    String environment,
    String category
) {}

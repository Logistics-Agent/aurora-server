package com.aurora.devopsagent.Domain.ValueObject;

import com.aurora.devopsagent.Domain.Enums.Severity;
import java.time.Instant;

public record PastIncidentRef(
    String correlationId,
    String errorSignature,
    Severity severity,
    String resolution,
    Instant resolvedAt,
    double similarity                    // 0.0–1.0
) {}

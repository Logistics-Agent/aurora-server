package com.aurora.aigovernance.governance.infrastructure.messaging;

import com.aurora.aigovernance.shared.domain.AiOperation;

import java.io.Serializable;
import java.time.Instant;
import java.util.UUID;

public record AiUsageEvent(
        String eventId,
        UUID tenantId,
        UUID userId,
        String callerServiceId,
        String capabilityCode,
        AiOperation operation,
        String provider,
        String slotAlias,
        long inputTokens,
        long outputTokens,
        long durationMs,
        boolean success,
        Instant timestamp
) implements Serializable {}

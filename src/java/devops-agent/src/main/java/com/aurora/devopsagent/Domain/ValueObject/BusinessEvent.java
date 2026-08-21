package com.aurora.devopsagent.Domain.ValueObject;

import java.time.Instant;
import java.util.Map;

public record BusinessEvent(
    Instant timestamp,
    String eventType,                    // PaymentFailed, ShipmentCreated, etc.
    String service,
    String tenantId,
    Map<String, Object> metadata         // Sanitized
) {}

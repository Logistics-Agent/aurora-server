package com.aurora.devopsagent.Domain.ValueObject;

import java.time.Duration;
import java.time.Instant;
import java.util.Map;

public record TraceSpan(
    String traceId,
    String spanId,
    String operationName,
    String serviceName,
    Instant startTime,
    Duration duration,
    String status,                       // OK, ERROR
    Map<String, String> attributes
) {}

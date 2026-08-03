package com.aurora.devopsagent.Application.Services;

import java.time.Duration;
import java.util.Map;

public record VerificationResult(
    VerificationStatus status,  // PASSED, FAILED, TIMEOUT, SKIPPED
    String detail,
    Duration duration,
    Map<String, Object> metrics  // health_check, error_rate, latency_p95, etc.
) {}

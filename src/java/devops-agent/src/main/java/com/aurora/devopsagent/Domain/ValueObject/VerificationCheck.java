package com.aurora.devopsagent.Domain.ValueObject;

public record VerificationCheck(
    VerificationCheckType type,         // HEALTH, READINESS, LIVENESS, METRIC, ERROR_RATE, BUSINESS_KPI, SMOKE_TEST
    String target,                      // Service or endpoint to check
    String expectedCondition,           // "error_rate < 1%", "pod_status = Running"
    int timeoutSeconds
) {}

package com.aurora.devopsagent.Domain.ValueObject;

public enum VerificationCheckType {
    HEALTH,
    READINESS,
    LIVENESS,
    METRIC,
    ERROR_RATE,
    BUSINESS_KPI,
    SMOKE_TEST,
    SLA
}

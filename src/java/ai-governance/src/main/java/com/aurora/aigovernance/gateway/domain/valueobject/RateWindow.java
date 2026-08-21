package com.aurora.aigovernance.gateway.domain.valueobject;

public record RateWindow(
        String bucketKey,
        long ttlSeconds
) {}

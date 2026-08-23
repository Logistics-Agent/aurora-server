package com.aurora.aigovernance.gateway.domain.valueobject;

public record AiGenerateResult(
        String content,
        long inputTokens,
        long outputTokens,
        String model,
        String provider
) {}

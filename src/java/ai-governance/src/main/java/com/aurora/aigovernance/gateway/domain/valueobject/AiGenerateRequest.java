package com.aurora.aigovernance.gateway.domain.valueobject;

import java.util.Map;

public record AiGenerateRequest(
        String capabilityCode,
        String prompt,
        long maxOutputTokens,
        long estimatedInputTokens,
        Map<String, String> parameters
) {}

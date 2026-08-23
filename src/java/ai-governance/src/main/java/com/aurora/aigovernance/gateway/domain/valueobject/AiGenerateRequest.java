package com.aurora.aigovernance.gateway.domain.valueobject;

import java.util.Collections;
import java.util.List;
import java.util.Map;

public record AiGenerateRequest(
        String capabilityCode,
        String prompt,
        long maxOutputTokens,
        long estimatedInputTokens,
        Map<String, String> parameters,
        List<MultimodalPart> inputParts
) {
    public AiGenerateRequest(
            String capabilityCode,
            String prompt,
            long maxOutputTokens,
            long estimatedInputTokens,
            Map<String, String> parameters) {
        this(capabilityCode, prompt, maxOutputTokens, estimatedInputTokens, parameters, Collections.emptyList());
    }
}

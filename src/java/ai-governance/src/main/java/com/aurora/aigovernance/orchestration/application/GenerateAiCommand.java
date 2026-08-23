package com.aurora.aigovernance.orchestration.application;

import com.aurora.aigovernance.gateway.domain.valueobject.MultimodalPart;
import com.aurora.aigovernance.shared.domain.TokenBudget;

import java.util.Collections;
import java.util.List;
import java.util.Map;
import java.util.UUID;

/**
 * Decoupled application command for AI Generation with typed multimodal support.
 */
public record GenerateAiCommand(
        UUID tenantId,
        UUID userId,                        // nullable for internal automation
        String callerServiceId,             // from CurrentServiceContext
        String capabilityCode,
        String prompt,
        TokenBudget tokenBudget,
        Map<String, String> parameters,
        List<MultimodalPart> inputParts
) {
    public GenerateAiCommand(
            UUID tenantId,
            UUID userId,
            String callerServiceId,
            String capabilityCode,
            String prompt,
            TokenBudget tokenBudget,
            Map<String, String> parameters) {
        this(tenantId, userId, callerServiceId, capabilityCode, prompt, tokenBudget, parameters, Collections.emptyList());
    }
}

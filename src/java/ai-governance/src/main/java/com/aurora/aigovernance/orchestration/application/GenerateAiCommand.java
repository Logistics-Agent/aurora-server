package com.aurora.aigovernance.orchestration.application;

import com.aurora.aigovernance.shared.domain.TokenBudget;

import java.util.Map;
import java.util.UUID;

/**
 * Decoupled application command for AI Generation.
 * <p>
 * Contains callerServiceId explicitly extracted from transport context (ThreadLocal)
 * at the gRPC handler boundary.
 */
public record GenerateAiCommand(
        UUID tenantId,
        UUID userId,                        // nullable for internal automation
        String callerServiceId,             // from CurrentServiceContext
        String capabilityCode,
        String prompt,
        TokenBudget tokenBudget,
        Map<String, String> parameters
) {}

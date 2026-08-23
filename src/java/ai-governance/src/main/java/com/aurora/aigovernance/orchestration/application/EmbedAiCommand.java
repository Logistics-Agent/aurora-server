package com.aurora.aigovernance.orchestration.application;

import java.util.UUID;

/**
 * Decoupled application command for AI Embedding.
 */
public record EmbedAiCommand(
        UUID tenantId,
        UUID userId,                        // nullable
        String callerServiceId,             // from CurrentServiceContext
        String capabilityCode,
        String content,
        Integer dimensions,
        long estimatedInputTokens
) {}

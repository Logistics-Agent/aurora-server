package com.aurora.aigovernance.gateway.domain.valueobject;

import java.util.List;

public record AiEmbeddingRequest(
        String capabilityCode,
        String content,
        Integer dimensions,
        long estimatedInputTokens
) {}

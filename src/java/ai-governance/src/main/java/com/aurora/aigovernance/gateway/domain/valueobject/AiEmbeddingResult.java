package com.aurora.aigovernance.gateway.domain.valueobject;

import java.util.List;

public record AiEmbeddingResult(
        List<Float> vector,
        long inputTokens,
        String model,
        String provider
) {}

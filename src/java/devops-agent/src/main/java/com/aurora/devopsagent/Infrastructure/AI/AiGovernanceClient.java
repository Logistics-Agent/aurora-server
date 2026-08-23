package com.aurora.devopsagent.Infrastructure.AI;

import java.util.Map;

/**
 * Domain-facing abstraction for Governed LLM Generation.
 * All LLM operations in DevOps-Agent route through this client to AiGovernance.
 */
public interface AiGovernanceClient {

    record GenerateCommand(
            String capabilityCode,
            String prompt,
            int maxOutputTokens,
            long estimatedInputTokens,
            Map<String, String> parameters
    ) {}

    record GenerateResult(
            String content,
            long inputTokens,
            long outputTokens,
            String decisionId,
            String automationLevel,
            boolean requiresApproval,
            String model,
            String provider
    ) {}

    GenerateResult generate(GenerateCommand command);
}

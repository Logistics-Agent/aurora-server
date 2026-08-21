package com.aurora.devopsagent.Domain.ValueObject;

import java.time.Instant;

/**
 * RedactedIncidentContext: Strongly-typed marker wrapper guaranteeing
 * that all sensitive tokens, credentials, connection strings, and PII
 * have been sanitized before being passed to RAG or AiGovernance.
 */
public record RedactedIncidentContext(
        String correlationId,
        String errorSignature,
        String affectedService,
        String sanitizedContextJson,
        String sanitizedPrompt,
        Instant redactedAt
) {
    public static RedactedIncidentContext of(
            String correlationId,
            String errorSignature,
            String affectedService,
            String sanitizedContextJson,
            String sanitizedPrompt) {
        return new RedactedIncidentContext(
                correlationId,
                errorSignature,
                affectedService,
                sanitizedContextJson,
                sanitizedPrompt,
                Instant.now()
        );
    }
}

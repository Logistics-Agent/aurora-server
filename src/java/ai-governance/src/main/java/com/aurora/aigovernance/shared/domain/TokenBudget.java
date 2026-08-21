package com.aurora.aigovernance.shared.domain;

/**
 * Token budget for provider capacity reservation.
 * <p>
 * Lives in shared domain (not governance-specific) because both Governance (tenant quota)
 * and Gateway (provider capacity) use it. Avoids undesirable Gateway→Governance dependency.
 * <p>
 * <b>Generation</b>: {@code reservationTokens() = estimatedInputTokens + maxOutputTokens}<br/>
 * <b>Embedding</b>: {@code maxOutputTokens = 0}, so {@code reservationTokens() = estimatedInputTokens}
 *
 * @param estimatedInputTokens estimated input token count (caller-provided in V1)
 * @param maxOutputTokens      maximum allowed output tokens (0 for embeddings)
 */
public record TokenBudget(
        long estimatedInputTokens,
        long maxOutputTokens
) {
    /**
     * Total tokens to reserve for TPM capacity accounting.
     * <p>
     * For generation: input + maxOutput exposure.<br/>
     * For embedding: input only (maxOutputTokens = 0).
     */
    public long reservationTokens() {
        return estimatedInputTokens + maxOutputTokens;
    }

    /**
     * Convenience factory for embedding operations (no output tokens).
     */
    public static TokenBudget forEmbedding(long estimatedInputTokens) {
        return new TokenBudget(estimatedInputTokens, 0);
    }
}

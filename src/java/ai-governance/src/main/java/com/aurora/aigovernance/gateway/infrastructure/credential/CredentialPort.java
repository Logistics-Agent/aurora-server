package com.aurora.aigovernance.gateway.infrastructure.credential;

/**
 * Port for resolving secrets (API keys) by secret reference from Key Vault.
 * <p>
 * IMPORTANT: This interface is ONLY injected into Gateway infrastructure provider clients.
 * It is never exposed to Application/Orchestration or Governance layers.
 */
public interface CredentialPort {

    /**
     * Resolve plaintext secret for a secretRef.
     */
    String resolveSecret(String secretRef);

    /**
     * Evict cached secret (e.g. on key rotation).
     */
    void evict(String secretRef);
}

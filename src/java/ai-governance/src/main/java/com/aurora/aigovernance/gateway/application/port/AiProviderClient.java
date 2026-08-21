package com.aurora.aigovernance.gateway.application.port;

import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.AiEmbeddingRequest;
import com.aurora.aigovernance.gateway.domain.valueobject.AiEmbeddingResult;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateRequest;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateResult;

/**
 * Port interface for provider-specific LLM execution.
 * <p>
 * Implementations (Gemini, AzureOpenAI) resolve credentials internally via {@code CredentialPort}.
 * Plaintext credentials never cross into the Application layer.
 */
public interface AiProviderClient {

    AiGenerateResult generate(ProviderSlot slot, AiGenerateRequest request);

    AiEmbeddingResult embed(ProviderSlot slot, AiEmbeddingRequest request);
}

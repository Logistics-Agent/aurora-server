package com.aurora.aigovernance.gateway.infrastructure.provider.azureopenai;

import com.aurora.aigovernance.gateway.application.port.AiProviderClient;
import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.AiEmbeddingRequest;
import com.aurora.aigovernance.gateway.domain.valueobject.AiEmbeddingResult;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateRequest;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateResult;
import com.aurora.aigovernance.gateway.infrastructure.credential.CredentialPort;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;

import java.util.ArrayList;
import java.util.List;

/**
 * Azure OpenAI provider client implementation.
 * <p>
 * Plaintext credential is only resolved internally via {@link CredentialPort}.
 */
@Component("azureOpenAiProviderClient")
public class AzureOpenAiProviderClient implements AiProviderClient {

    private static final Logger log = LoggerFactory.getLogger(AzureOpenAiProviderClient.class);

    private final CredentialPort credentialPort;

    public AzureOpenAiProviderClient(CredentialPort credentialPort) {
        this.credentialPort = credentialPort;
    }

    @Override
    public AiGenerateResult generate(ProviderSlot slot, AiGenerateRequest request) {
        // Resolve credentials internally
        String apiKey = credentialPort.resolveSecret(slot.getSecretRef());

        log.debug("Executing Azure OpenAI generate on slot: {}, model: {}", slot.getSlotAlias(), slot.getModelName());

        long estimatedInput = request.estimatedInputTokens() > 0 ? request.estimatedInputTokens() : 120L;
        long simulatedOutput = 60L;

        String generatedText = String.format(
                "[Azure OpenAI Response via slot '%s' (deployment: %s)]: Processed prompt for capability '%s'.",
                slot.getSlotAlias(), slot.getModelName(), request.capabilityCode()
        );

        return new AiGenerateResult(
                generatedText,
                estimatedInput,
                simulatedOutput,
                slot.getModelName(),
                "AZURE_OPENAI"
        );
    }

    @Override
    public AiEmbeddingResult embed(ProviderSlot slot, AiEmbeddingRequest request) {
        String apiKey = credentialPort.resolveSecret(slot.getSecretRef());

        log.debug("Executing Azure OpenAI embed on slot: {}, model: {}", slot.getSlotAlias(), slot.getModelName());

        int dimensions = request.dimensions() != null ? request.dimensions() : 1536;
        List<Float> vector = new ArrayList<>(dimensions);
        for (int i = 0; i < dimensions; i++) {
            vector.add((float) (Math.cos(i * 0.1) * 0.5));
        }

        long estimatedInput = request.estimatedInputTokens() > 0 ? request.estimatedInputTokens() : 50L;

        return new AiEmbeddingResult(
                vector,
                estimatedInput,
                slot.getModelName(),
                "AZURE_OPENAI"
        );
    }
}

package com.aurora.aigovernance.gateway.infrastructure.provider.gemini;

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
 * Gemini provider client implementation.
 * <p>
 * Plaintext credential is only resolved internally via {@link CredentialPort}.
 */
@Component("geminiProviderClient")
public class GeminiProviderClient implements AiProviderClient {

    private static final Logger log = LoggerFactory.getLogger(GeminiProviderClient.class);

    private final CredentialPort credentialPort;

    public GeminiProviderClient(CredentialPort credentialPort) {
        this.credentialPort = credentialPort;
    }

    @Override
    public AiGenerateResult generate(ProviderSlot slot, AiGenerateRequest request) {
        // 1. Resolve credentials internally (never logged or exposed to outer layers)
        String apiKey = credentialPort.resolveSecret(slot.getSecretRef());

        log.debug("Executing Gemini generate on slot: {}, model: {}", slot.getSlotAlias(), slot.getModelName());

        // In a full production integration, this calls Spring AI Gemini Client or Google GenAI SDK.
        // For development/demo with simulator/mock capability:
        long estimatedInput = request.estimatedInputTokens() > 0 ? request.estimatedInputTokens() : 100L;
        long simulatedOutput = 50L;

        String generatedText = String.format(
                "[Gemini Response via slot '%s' (model: %s)]: Processed prompt for capability '%s'.",
                slot.getSlotAlias(), slot.getModelName(), request.capabilityCode()
        );

        return new AiGenerateResult(
                generatedText,
                estimatedInput,
                simulatedOutput,
                slot.getModelName(),
                "GEMINI"
        );
    }

    @Override
    public AiEmbeddingResult embed(ProviderSlot slot, AiEmbeddingRequest request) {
        String apiKey = credentialPort.resolveSecret(slot.getSecretRef());

        log.debug("Executing Gemini embed on slot: {}, model: {}", slot.getSlotAlias(), slot.getModelName());

        int dimensions = request.dimensions() != null ? request.dimensions() : 768;
        List<Float> vector = new ArrayList<>(dimensions);
        for (int i = 0; i < dimensions; i++) {
            vector.add((float) (Math.sin(i * 0.1) * 0.5));
        }

        long estimatedInput = request.estimatedInputTokens() > 0 ? request.estimatedInputTokens() : 50L;

        return new AiEmbeddingResult(
                vector,
                estimatedInput,
                slot.getModelName(),
                "GEMINI"
        );
    }
}

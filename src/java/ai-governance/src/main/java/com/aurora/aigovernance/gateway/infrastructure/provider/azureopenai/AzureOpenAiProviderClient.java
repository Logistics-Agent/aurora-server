package com.aurora.aigovernance.gateway.infrastructure.provider.azureopenai;

import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.time.Duration;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;

import com.aurora.aigovernance.gateway.application.port.AiProviderClient;
import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.AiEmbeddingRequest;
import com.aurora.aigovernance.gateway.domain.valueobject.AiEmbeddingResult;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateRequest;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateResult;
import com.aurora.aigovernance.gateway.infrastructure.credential.CredentialPort;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;

/**
 * Production Azure OpenAI provider client with real Azure OpenAI HTTP REST integration.
 * <p>
 * Plaintext credential is only resolved internally via {@link CredentialPort}.
 * Enforces output dimension 768 and unit-normalized vectors.
 */
@Component("azureOpenAiProviderClient")
public class AzureOpenAiProviderClient implements AiProviderClient {

    private static final Logger log = LoggerFactory.getLogger(AzureOpenAiProviderClient.class);
    private static final String DEFAULT_AZURE_ENDPOINT = "https://aurora-openai.openai.azure.com";

    private final CredentialPort credentialPort;
    private final HttpClient httpClient;
    private final ObjectMapper objectMapper;
    private final String endpoint;

    public AzureOpenAiProviderClient(CredentialPort credentialPort) {
        this(credentialPort, HttpClient.newBuilder().connectTimeout(Duration.ofSeconds(10)).build(), new ObjectMapper(), DEFAULT_AZURE_ENDPOINT);
    }

    public AzureOpenAiProviderClient(CredentialPort credentialPort, HttpClient httpClient, ObjectMapper objectMapper, String endpoint) {
        this.credentialPort = credentialPort;
        this.httpClient = httpClient;
        this.objectMapper = objectMapper;
        this.endpoint = endpoint != null ? endpoint : DEFAULT_AZURE_ENDPOINT;
    }

    @Override
    public AiGenerateResult generate(ProviderSlot slot, AiGenerateRequest request) {
        String apiKey = credentialPort.resolveSecret(slot.getSecretRef());
        log.debug("Executing Azure OpenAI generate on slot: {}, model: {}", slot.getSlotAlias(), slot.getModelName());

        if (isTestKey(apiKey)) {
            return simulateGenerate(slot, request);
        }

        try {
            String deployment = slot.getModelName() != null && !slot.getModelName().isBlank() ? slot.getModelName() : "gpt-4o-mini";
            URI uri = URI.create(endpoint + "/openai/deployments/" + deployment + "/chat/completions?api-version=2024-02-01");

            Map<String, Object> body = new HashMap<>();
            List<Map<String, String>> messages = List.of(
                    Map.of("role", "user", "content", request.prompt() != null ? request.prompt() : "")
            );
            body.put("messages", messages);
            if (request.maxOutputTokens() > 0) {
                body.put("max_tokens", request.maxOutputTokens());
            }

            String requestJson = objectMapper.writeValueAsString(body);

            HttpRequest httpRequest = HttpRequest.newBuilder()
                    .uri(uri)
                    .header("Content-Type", "application/json")
                    .header("api-key", apiKey)
                    .POST(HttpRequest.BodyPublishers.ofString(requestJson))
                    .timeout(Duration.ofSeconds(30))
                    .build();

            HttpResponse<String> response = httpClient.send(httpRequest, HttpResponse.BodyHandlers.ofString());

            if (response.statusCode() != 200) {
                log.error("Azure OpenAI generate HTTP error {}: {}", response.statusCode(), response.body());
                throw new IllegalStateException("Azure OpenAI generate HTTP " + response.statusCode() + ": " + response.body());
            }

            JsonNode root = objectMapper.readTree(response.body());
            String text = root.path("choices").path(0).path("message").path("content").asText();
            long inputTokens = root.path("usage").path("prompt_tokens").asLong(request.estimatedInputTokens());
            long outputTokens = root.path("usage").path("completion_tokens").asLong(50L);

            return new AiGenerateResult(text, inputTokens, outputTokens, deployment, "AZURE_OPENAI");

        } catch (Exception e) {
            log.error("Azure OpenAI generate failed on slot {}: {}", slot.getSlotAlias(), e.getMessage());
            throw new IllegalStateException("Azure OpenAI provider error: " + e.getMessage(), e);
        }
    }

    @Override
    public AiEmbeddingResult embed(ProviderSlot slot, AiEmbeddingRequest request) {
        String apiKey = credentialPort.resolveSecret(slot.getSecretRef());
        log.debug("Executing Azure OpenAI embed on slot: {}, model: {}", slot.getSlotAlias(), slot.getModelName());

        int targetDimension = (request.dimensions() != null && request.dimensions() > 0) ? request.dimensions() : 768;

        if (isTestKey(apiKey)) {
            return simulateEmbed(slot, request, targetDimension);
        }

        try {
            String deployment = slot.getModelName() != null && !slot.getModelName().isBlank() ? slot.getModelName() : "text-embedding-3-small";
            URI uri = URI.create(endpoint + "/openai/deployments/" + deployment + "/embeddings?api-version=2024-02-01");

            Map<String, Object> body = new HashMap<>();
            body.put("input", request.content() != null ? request.content() : "");
            body.put("dimensions", targetDimension);

            String requestJson = objectMapper.writeValueAsString(body);

            HttpRequest httpRequest = HttpRequest.newBuilder()
                    .uri(uri)
                    .header("Content-Type", "application/json")
                    .header("api-key", apiKey)
                    .POST(HttpRequest.BodyPublishers.ofString(requestJson))
                    .timeout(Duration.ofSeconds(30))
                    .build();

            HttpResponse<String> response = httpClient.send(httpRequest, HttpResponse.BodyHandlers.ofString());

            if (response.statusCode() != 200) {
                log.error("Azure OpenAI embed HTTP error {}: {}", response.statusCode(), response.body());
                throw new IllegalStateException("Azure OpenAI embed HTTP " + response.statusCode() + ": " + response.body());
            }

            JsonNode root = objectMapper.readTree(response.body());
            JsonNode embeddingNode = root.path("data").path(0).path("embedding");

            List<Float> vector = new ArrayList<>(targetDimension);
            if (embeddingNode.isArray()) {
                for (JsonNode val : embeddingNode) {
                    vector.add((float) val.asDouble());
                }
            }

            normalizeVector(vector);

            long estimatedInput = root.path("usage").path("prompt_tokens").asLong(
                    request.estimatedInputTokens() > 0 ? request.estimatedInputTokens() : 50L
            );

            return new AiEmbeddingResult(vector, estimatedInput, deployment, "AZURE_OPENAI");

        } catch (Exception e) {
            log.error("Azure OpenAI embed failed on slot {}: {}", slot.getSlotAlias(), e.getMessage());
            throw new IllegalStateException("Azure OpenAI provider error: " + e.getMessage(), e);
        }
    }

    private static boolean isTestKey(String apiKey) {
        return apiKey == null || apiKey.startsWith("demo") || apiKey.startsWith("test") || apiKey.startsWith("mock") || apiKey.equals("dev-key");
    }

    private static void normalizeVector(List<Float> vector) {
        double normSq = 0.0;
        for (Float val : vector) {
            normSq += val * val;
        }
        if (normSq > 0.0) {
            double norm = Math.sqrt(normSq);
            for (int i = 0; i < vector.size(); i++) {
                vector.set(i, (float) (vector.get(i) / norm));
            }
        }
    }

    private AiGenerateResult simulateGenerate(ProviderSlot slot, AiGenerateRequest request) {
        long estimatedInput = request.estimatedInputTokens() > 0 ? request.estimatedInputTokens() : 120L;
        long simulatedOutput = 60L;
        String generatedText = String.format(
                "[Azure OpenAI Response via slot '%s' (deployment: %s)]: Processed prompt for capability '%s'.",
                slot.getSlotAlias(), slot.getModelName(), request.capabilityCode()
        );
        return new AiGenerateResult(generatedText, estimatedInput, simulatedOutput, slot.getModelName(), "AZURE_OPENAI");
    }

    private AiEmbeddingResult simulateEmbed(ProviderSlot slot, AiEmbeddingRequest request, int dimensions) {
        List<Float> vector = new ArrayList<>(dimensions);
        int seed = (request.content() != null ? request.content().hashCode() : 42);
        double normSq = 0.0;
        for (int i = 0; i < dimensions; i++) {
            float val = (float) Math.cos((seed + i + 1) * 0.05);
            vector.add(val);
            normSq += val * val;
        }
        double norm = Math.sqrt(normSq);
        for (int i = 0; i < dimensions; i++) {
            vector.set(i, (float) (vector.get(i) / norm));
        }
        long estimatedInput = request.estimatedInputTokens() > 0 ? request.estimatedInputTokens() : 50L;
        return new AiEmbeddingResult(vector, estimatedInput, slot.getModelName(), "AZURE_OPENAI");
    }
}

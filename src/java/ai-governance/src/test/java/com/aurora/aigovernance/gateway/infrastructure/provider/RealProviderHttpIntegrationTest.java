package com.aurora.aigovernance.gateway.infrastructure.provider;

import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.AiEmbeddingRequest;
import com.aurora.aigovernance.gateway.domain.valueobject.AiEmbeddingResult;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateRequest;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateResult;
import com.aurora.aigovernance.gateway.domain.valueobject.MultimodalPart;
import com.aurora.aigovernance.gateway.infrastructure.credential.CredentialPort;
import com.aurora.aigovernance.gateway.infrastructure.provider.azureopenai.AzureOpenAiProviderClient;
import com.aurora.aigovernance.gateway.infrastructure.provider.gemini.GeminiProviderClient;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.shared.domain.AiOperation;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.sun.net.httpserver.HttpServer;
import org.junit.jupiter.api.AfterAll;
import org.junit.jupiter.api.BeforeAll;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.io.IOException;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.net.http.HttpClient;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import java.util.Map;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

class RealProviderHttpIntegrationTest {

    private static HttpServer mockServer;
    private static int serverPort;
    private static CredentialPort credentialPort;
    private static ObjectMapper objectMapper;
    private static HttpClient httpClient;

    @BeforeAll
    static void startMockHttpServer() throws IOException {
        mockServer = HttpServer.create(new InetSocketAddress(0), 0);
        serverPort = mockServer.getAddress().getPort();

        objectMapper = new ObjectMapper();
        httpClient = HttpClient.newHttpClient();
        credentialPort = mock(CredentialPort.class);
        when(credentialPort.resolveSecret(anyString())).thenReturn("real-prod-sk-live-123456");

        // 1. Mock Gemini Embed Endpoint
        mockServer.createContext("/v1beta/models/text-embedding-004:embedContent", exchange -> {
            assertEquals("POST", exchange.getRequestMethod());
            assertEquals("real-prod-sk-live-123456", exchange.getRequestHeaders().getFirst("x-goog-api-key"));

            List<Double> mockValues = new ArrayList<>();
            for (int i = 0; i < 768; i++) {
                mockValues.add(0.01 * (i + 1));
            }

            Map<String, Object> responseBody = Map.of(
                    "embedding", Map.of("values", mockValues)
            );

            byte[] bytes = objectMapper.writeValueAsBytes(responseBody);
            exchange.getResponseHeaders().set("Content-Type", "application/json");
            exchange.sendResponseHeaders(200, bytes.length);
            try (OutputStream os = exchange.getResponseBody()) {
                os.write(bytes);
            }
        });

        // 2. Mock Gemini Generate Endpoint
        mockServer.createContext("/v1beta/models/gemini-1.5-flash:generateContent", exchange -> {
            assertEquals("POST", exchange.getRequestMethod());
            assertEquals("real-prod-sk-live-123456", exchange.getRequestHeaders().getFirst("x-goog-api-key"));

            Map<String, Object> responseBody = Map.of(
                    "candidates", List.of(
                            Map.of(
                                    "content", Map.of(
                                            "parts", List.of(Map.of("text", "{\"invoice_number\":\"INV-999\",\"total\":1500.0}"))
                                    ),
                                    "finishReason", "STOP"
                            )
                    ),
                    "usageMetadata", Map.of(
                            "promptTokenCount", 150,
                            "candidatesTokenCount", 45,
                            "totalTokenCount", 195
                    )
            );

            byte[] bytes = objectMapper.writeValueAsBytes(responseBody);
            exchange.getResponseHeaders().set("Content-Type", "application/json");
            exchange.sendResponseHeaders(200, bytes.length);
            try (OutputStream os = exchange.getResponseBody()) {
                os.write(bytes);
            }
        });

        // 3. Mock Azure OpenAI Embed Endpoint
        mockServer.createContext("/openai/deployments/text-embedding-3-small/embeddings", exchange -> {
            assertEquals("POST", exchange.getRequestMethod());
            assertEquals("real-prod-sk-live-123456", exchange.getRequestHeaders().getFirst("api-key"));

            List<Double> mockValues = new ArrayList<>();
            for (int i = 0; i < 768; i++) {
                mockValues.add(0.02 * (i + 1));
            }

            Map<String, Object> responseBody = Map.of(
                    "data", List.of(Map.of("embedding", mockValues)),
                    "usage", Map.of("prompt_tokens", 80, "total_tokens", 80)
            );

            byte[] bytes = objectMapper.writeValueAsBytes(responseBody);
            exchange.getResponseHeaders().set("Content-Type", "application/json");
            exchange.sendResponseHeaders(200, bytes.length);
            try (OutputStream os = exchange.getResponseBody()) {
                os.write(bytes);
            }
        });

        // 4. Mock Azure OpenAI Generate Endpoint
        mockServer.createContext("/openai/deployments/gpt-4o-mini/chat/completions", exchange -> {
            assertEquals("POST", exchange.getRequestMethod());
            assertEquals("real-prod-sk-live-123456", exchange.getRequestHeaders().getFirst("api-key"));

            Map<String, Object> responseBody = Map.of(
                    "choices", List.of(
                            Map.of("message", Map.of("content", "Compliant customs declaration verified."))
                    ),
                    "usage", Map.of("prompt_tokens", 110, "completion_tokens", 35, "total_tokens", 145)
            );

            byte[] bytes = objectMapper.writeValueAsBytes(responseBody);
            exchange.getResponseHeaders().set("Content-Type", "application/json");
            exchange.sendResponseHeaders(200, bytes.length);
            try (OutputStream os = exchange.getResponseBody()) {
                os.write(bytes);
            }
        });

        mockServer.start();
    }

    @AfterAll
    static void stopMockHttpServer() {
        if (mockServer != null) {
            mockServer.stop(0);
        }
    }

    @Test
    @DisplayName("GeminiProviderClient successfully calls live HTTP endpoint and parses 768-dim embedding")
    void testGeminiRealHttpEmbed() {
        String baseUrl = "http://localhost:" + serverPort;
        GeminiProviderClient client = new GeminiProviderClient(credentialPort, httpClient, objectMapper, baseUrl);

        ProviderSlot slot = new ProviderSlot();
        slot.setSlotAlias("gemini-prod-embed");
        slot.setProvider(AiProvider.GEMINI);
        slot.setOperation(AiOperation.EMBED);
        slot.setModelName("text-embedding-004");
        slot.setSecretRef("gemini-live-key");

        AiEmbeddingRequest request = new AiEmbeddingRequest("compliance.embed", "Customs declaration classification rule", 768, 90L);
        AiEmbeddingResult result = client.embed(slot, request);

        assertNotNull(result);
        assertEquals("GEMINI", result.provider());
        assertEquals("text-embedding-004", result.model());
        assertEquals(768, result.vector().size());
        assertTrue(result.vector().stream().allMatch(Float::isFinite));

        // Check unit vector length
        double normSq = result.vector().stream().mapToDouble(v -> v * v).sum();
        assertEquals(1.0, normSq, 0.001);
    }

    @Test
    @DisplayName("GeminiProviderClient successfully calls live HTTP generate with multimodal parts")
    void testGeminiRealHttpGenerateMultimodal() {
        String baseUrl = "http://localhost:" + serverPort;
        GeminiProviderClient client = new GeminiProviderClient(credentialPort, httpClient, objectMapper, baseUrl);

        ProviderSlot slot = new ProviderSlot();
        slot.setSlotAlias("gemini-prod-gen");
        slot.setProvider(AiProvider.GEMINI);
        slot.setOperation(AiOperation.GENERATE);
        slot.setModelName("gemini-1.5-flash");
        slot.setSecretRef("gemini-live-key");

        List<MultimodalPart> parts = List.of(
                MultimodalPart.text("Extract invoice json"),
                MultimodalPart.file("objects/tenants/inv1.pdf", "application/pdf", "inv1.pdf", 2048)
        );

        AiGenerateRequest request = new AiGenerateRequest("ocr.extract", "Extract invoice", 1000, 150, Map.of(), parts);
        AiGenerateResult result = client.generate(slot, request);

        assertNotNull(result);
        assertEquals("GEMINI", result.provider());
        assertTrue(result.content().contains("INV-999"));
        assertEquals(150, result.inputTokens());
        assertEquals(45, result.outputTokens());
    }

    @Test
    @DisplayName("AzureOpenAiProviderClient successfully calls live HTTP endpoint and parses 768-dim embedding")
    void testAzureOpenAiRealHttpEmbed() {
        String endpoint = "http://localhost:" + serverPort;
        AzureOpenAiProviderClient client = new AzureOpenAiProviderClient(credentialPort, httpClient, objectMapper, endpoint);

        ProviderSlot slot = new ProviderSlot();
        slot.setSlotAlias("azure-prod-embed");
        slot.setProvider(AiProvider.AZURE_OPENAI);
        slot.setOperation(AiOperation.EMBED);
        slot.setModelName("text-embedding-3-small");
        slot.setSecretRef("azure-live-key");

        AiEmbeddingRequest request = new AiEmbeddingRequest("compliance.embed", "Maritime transport regulation", 768, 80L);
        AiEmbeddingResult result = client.embed(slot, request);

        assertNotNull(result);
        assertEquals("AZURE_OPENAI", result.provider());
        assertEquals("text-embedding-3-small", result.model());
        assertEquals(768, result.vector().size());
        assertTrue(result.vector().stream().allMatch(Float::isFinite));

        double normSq = result.vector().stream().mapToDouble(v -> v * v).sum();
        assertEquals(1.0, normSq, 0.001);
    }

    @Test
    @DisplayName("AzureOpenAiProviderClient successfully calls live HTTP chat completions")
    void testAzureOpenAiRealHttpGenerate() {
        String endpoint = "http://localhost:" + serverPort;
        AzureOpenAiProviderClient client = new AzureOpenAiProviderClient(credentialPort, httpClient, objectMapper, endpoint);

        ProviderSlot slot = new ProviderSlot();
        slot.setSlotAlias("azure-prod-gen");
        slot.setProvider(AiProvider.AZURE_OPENAI);
        slot.setOperation(AiOperation.GENERATE);
        slot.setModelName("gpt-4o-mini");
        slot.setSecretRef("azure-live-key");

        AiGenerateRequest request = new AiGenerateRequest("compliance.evaluate", "Check customs compliance", 500, 110, Map.of());
        AiGenerateResult result = client.generate(slot, request);

        assertNotNull(result);
        assertEquals("AZURE_OPENAI", result.provider());
        assertTrue(result.content().contains("Compliant"));
        assertEquals(110, result.inputTokens());
        assertEquals(35, result.outputTokens());
    }
}

package com.aurora.aigovernance.gateway.infrastructure.provider;

import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateRequest;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateResult;
import com.aurora.aigovernance.gateway.infrastructure.credential.CredentialPort;
import com.aurora.aigovernance.gateway.infrastructure.provider.azureopenai.AzureOpenAiProviderClient;
import com.aurora.aigovernance.gateway.infrastructure.provider.gemini.GeminiProviderClient;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.shared.domain.AiOperation;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.junit.jupiter.api.Test;

import java.net.http.HttpClient;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;
import java.util.Map;
import java.util.concurrent.atomic.AtomicReference;

import com.sun.net.httpserver.HttpServer;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

class StructuredOutputContractTest {

    private final CredentialPort credentialPort = mock(CredentialPort.class);
    private final ObjectMapper objectMapper = new ObjectMapper();

    @Test
    void geminiSimulationReturnsStructuredComplianceAnswerJson() throws Exception {
        when(credentialPort.resolveSecret("test-key")).thenReturn("test-key");

        AiGenerateResult result = new GeminiProviderClient(credentialPort)
                .generate(slot(AiProvider.GEMINI, "gemini-test"), request());

        JsonNode json = objectMapper.readTree(result.content());

        assertTrue(json.isObject());
        assertTrue(json.has("answer"));
        assertTrue(json.has("citations"));
        assertTrue(json.has("knowledgeReferences"));
        assertTrue(json.has("conflicts"));
        assertTrue(json.has("insufficientEvidence"));
        assertTrue(json.has("missingInformation"));
    }

    @Test
    void azureSimulationReturnsStructuredComplianceAnswerJson() throws Exception {
        when(credentialPort.resolveSecret("test-key")).thenReturn("test-key");

        AiGenerateResult result = new AzureOpenAiProviderClient(credentialPort)
                .generate(slot(AiProvider.AZURE_OPENAI, "azure-test"), request());

        JsonNode json = objectMapper.readTree(result.content());

        assertTrue(json.isObject());
        assertEquals("compliance.answer", request().capabilityCode());
        assertTrue(json.has("answer"));
        assertTrue(json.has("citations"));
        assertTrue(json.has("knowledgeReferences"));
        assertTrue(json.has("conflicts"));
        assertTrue(json.has("insufficientEvidence"));
        assertTrue(json.has("missingInformation"));
    }

    @Test
    void geminiComplianceAnswerRequestsJsonMimeType() throws Exception {
        AtomicReference<JsonNode> requestBody = new AtomicReference<>();
        HttpServer server = HttpServer.create(new InetSocketAddress(0), 0);
        server.createContext("/v1beta/models/gemini-1.5-flash:generateContent", exchange -> {
            requestBody.set(objectMapper.readTree(exchange.getRequestBody()));
            writeResponse(exchange, "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"{}\"}]}}]}");
        });
        server.start();

        try {
            when(credentialPort.resolveSecret("live-key")).thenReturn("live-key");
            new GeminiProviderClient(
                    credentialPort,
                    HttpClient.newHttpClient(),
                    objectMapper,
                    "http://localhost:" + server.getAddress().getPort())
                    .generate(slot(AiProvider.GEMINI, "gemini-live", "live-key"), request());

            assertEquals("application/json",
                    requestBody.get().path("generationConfig").path("responseMimeType").asText());
        } finally {
            server.stop(0);
        }
    }

    @Test
    void azureComplianceAnswerRequestsJsonObjectFormat() throws Exception {
        AtomicReference<JsonNode> requestBody = new AtomicReference<>();
        HttpServer server = HttpServer.create(new InetSocketAddress(0), 0);
        server.createContext("/openai/deployments/gpt-4o-mini/chat/completions", exchange -> {
            requestBody.set(objectMapper.readTree(exchange.getRequestBody()));
            writeResponse(exchange, "{\"choices\":[{\"message\":{\"content\":\"{}\"}}]}");
        });
        server.start();

        try {
            when(credentialPort.resolveSecret("live-key")).thenReturn("live-key");
            new AzureOpenAiProviderClient(
                    credentialPort,
                    HttpClient.newHttpClient(),
                    objectMapper,
                    "http://localhost:" + server.getAddress().getPort())
                    .generate(slot(AiProvider.AZURE_OPENAI, "azure-live", "live-key"), request());

            assertEquals("json_object",
                    requestBody.get().path("response_format").path("type").asText());
        } finally {
            server.stop(0);
        }
    }

    private static AiGenerateRequest request() {
        return new AiGenerateRequest(
                "compliance.answer",
                "Return a grounded answer as JSON.",
                512,
                100,
                Map.of("response_format", "json_object"));
    }

    private static void writeResponse(com.sun.net.httpserver.HttpExchange exchange, String content) throws java.io.IOException {
        byte[] bytes = content.getBytes(StandardCharsets.UTF_8);
        exchange.getResponseHeaders().set("Content-Type", "application/json");
        exchange.sendResponseHeaders(200, bytes.length);
        try (var output = exchange.getResponseBody()) {
            output.write(bytes);
        }
    }

    private static ProviderSlot slot(AiProvider provider, String alias) {
        return slot(provider, alias, "test-key");
    }

    private static ProviderSlot slot(AiProvider provider, String alias, String secretRef) {
        ProviderSlot slot = new ProviderSlot();
        slot.setSlotAlias(alias);
        slot.setProvider(provider);
        slot.setOperation(AiOperation.GENERATE);
        slot.setModelName(provider == AiProvider.GEMINI ? "gemini-1.5-flash" : "gpt-4o-mini");
        slot.setSecretRef(secretRef);
        return slot;
    }
}

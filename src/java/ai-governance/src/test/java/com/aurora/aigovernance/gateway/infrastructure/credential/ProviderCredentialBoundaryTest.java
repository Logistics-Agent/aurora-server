package com.aurora.aigovernance.gateway.infrastructure.credential;

import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateRequest;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateResult;
import com.aurora.aigovernance.gateway.infrastructure.provider.gemini.GeminiProviderClient;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.shared.domain.AiOperation;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.sun.net.httpserver.HttpServer;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.io.IOException;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.net.http.HttpClient;
import java.util.List;
import java.util.Map;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
public class ProviderCredentialBoundaryTest {

    @Mock
    private CredentialPort credentialPort;

    private HttpServer mockServer;
    private int serverPort;

    @BeforeEach
    void setUp() throws IOException {
        mockServer = HttpServer.create(new InetSocketAddress(0), 0);
        serverPort = mockServer.getAddress().getPort();

        ObjectMapper objectMapper = new ObjectMapper();
        mockServer.createContext("/v1beta/models/gemini-1.5-flash:generateContent", exchange -> {
            Map<String, Object> responseBody = Map.of(
                    "candidates", List.of(
                            Map.of(
                                    "content", Map.of(
                                            "parts", List.of(Map.of("text", "mock-response-text"))
                                    ),
                                    "finishReason", "STOP"
                            )
                    ),
                    "usageMetadata", Map.of(
                            "promptTokenCount", 10,
                            "candidatesTokenCount", 20,
                            "totalTokenCount", 30
                    )
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

    @AfterEach
    void tearDown() {
        if (mockServer != null) {
            mockServer.stop(0);
        }
    }

    @Test
    public void testSecretResolution_OnlyHappensInsideProviderClient() {
        GeminiProviderClient client = new GeminiProviderClient(credentialPort);
        GeminiProviderClient client = new GeminiProviderClient(
                credentialPort,
                HttpClient.newHttpClient(),
                new ObjectMapper(),
                "http://localhost:" + serverPort
        );

        ProviderSlot slot = new ProviderSlot();
        slot.setSlotAlias("gemini-shared-generate-01");
        slot.setSecretRef("gemini-secret-vault-ref");
        slot.setProvider(AiProvider.GEMINI);
        slot.setOperation(AiOperation.GENERATE);
        slot.setModelName("gemini-1.5-flash");

        when(credentialPort.resolveSecret("gemini-secret-vault-ref")).thenReturn("AIzaSySecretApiKey12345");

        AiGenerateRequest request = new AiGenerateRequest("compliance.answer", "prompt", 100, 100, Map.of());

        AiGenerateResult result = client.generate(slot, request);

        // Verify CredentialPort was called internally
        verify(credentialPort).resolveSecret("gemini-secret-vault-ref");

        // Verify plaintext API key is never in result or model name
        assertNotNull(result);
        assertFalse(result.content().contains("AIzaSySecretApiKey12345"));
        assertFalse(result.model().contains("AIzaSySecretApiKey12345"));
        assertFalse(result.provider().contains("AIzaSySecretApiKey12345"));
    }
}

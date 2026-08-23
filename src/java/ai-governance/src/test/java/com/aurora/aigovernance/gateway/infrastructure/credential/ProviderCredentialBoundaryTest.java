package com.aurora.aigovernance.gateway.infrastructure.credential;

import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateRequest;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateResult;
import com.aurora.aigovernance.gateway.infrastructure.provider.gemini.GeminiProviderClient;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.shared.domain.AiOperation;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.util.Map;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
public class ProviderCredentialBoundaryTest {

    @Mock
    private CredentialPort credentialPort;

    @Test
    public void testSecretResolution_OnlyHappensInsideProviderClient() {
        GeminiProviderClient client = new GeminiProviderClient(credentialPort);

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

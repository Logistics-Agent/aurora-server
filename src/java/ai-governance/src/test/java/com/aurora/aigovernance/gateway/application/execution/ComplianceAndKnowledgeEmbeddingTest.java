package com.aurora.aigovernance.gateway.application.execution;

import com.aurora.aigovernance.gateway.application.capacity.CapacityReservationService;
import com.aurora.aigovernance.gateway.application.port.AiProviderClient;
import com.aurora.aigovernance.gateway.application.routing.ProviderRoutingService;
import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.*;
import com.aurora.aigovernance.gateway.infrastructure.credential.CredentialPort;
import com.aurora.aigovernance.gateway.infrastructure.persistence.ProviderSlotRepository;
import com.aurora.aigovernance.gateway.infrastructure.provider.azureopenai.AzureOpenAiProviderClient;
import com.aurora.aigovernance.gateway.infrastructure.provider.gemini.GeminiProviderClient;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.governance.domain.enums.AutomationLevel;
import com.aurora.aigovernance.governance.domain.enums.ModelTier;
import com.aurora.aigovernance.governance.domain.valueobject.GovernanceDecision;
import com.aurora.aigovernance.shared.domain.AiOperation;
import com.aurora.aigovernance.shared.domain.TokenBudget;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import org.mockito.Mockito;

import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.Set;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.*;
import static org.mockito.Mockito.when;

class ComplianceAndKnowledgeEmbeddingTest {

    private ProviderRoutingService routingService;
    private CapacityReservationService capacityService;
    private CredentialPort credentialPort;
    private ProviderSlotRepository slotRepository;
    private GeminiProviderClient geminiClient;
    private AzureOpenAiProviderClient azureClient;
    private AiExecutionService executionService;

    private ProviderSlot geminiEmbedSlot;

    @BeforeEach
    void setUp() {
        routingService = Mockito.mock(ProviderRoutingService.class);
        capacityService = Mockito.mock(CapacityReservationService.class);
        credentialPort = Mockito.mock(CredentialPort.class);
        slotRepository = Mockito.mock(ProviderSlotRepository.class);
        when(credentialPort.resolveSecret(anyString())).thenReturn("demo-api-key");

        geminiClient = new GeminiProviderClient(credentialPort);
        azureClient = new AzureOpenAiProviderClient(credentialPort);

        executionService = new AiExecutionService(
                routingService,
                capacityService,
                Map.of(
                        "geminiProviderClient", geminiClient,
                        "azureOpenAiProviderClient", azureClient
                ),
                slotRepository
        );

        geminiEmbedSlot = new ProviderSlot();
        geminiEmbedSlot.setSlotAlias("gemini-shared-embed-01");
        geminiEmbedSlot.setProvider(AiProvider.GEMINI);
        geminiEmbedSlot.setOperation(AiOperation.EMBED);
        geminiEmbedSlot.setModelName("text-embedding-004");
        geminiEmbedSlot.setSecretRef("gemini-api-key-shared-01");
    }

    @Test
    @DisplayName("compliance.embed executes on EMBED slot and produces exactly 768-dimension vector")
    void testComplianceEmbedExecution() {
        GovernanceDecision decision = new GovernanceDecision(
                true,
                null,
                UUID.randomUUID().toString(),
                Set.of(AiProvider.GEMINI),
                Set.of("shared-ai"),
                ModelTier.STANDARD,
                4096,
                AutomationLevel.SEMI_AUTONOMOUS,
                false,
                "v1"
        );

        AiEmbeddingRequest request = new AiEmbeddingRequest("compliance.embed", "Maritime dangerous goods regulation excerpt", 768, 80L);
        TokenBudget tokenBudget = new TokenBudget(80L, 0L);

        when(routingService.getCandidates(any(), any(), eq(AiOperation.EMBED), anyString(), any()))
                .thenReturn(List.of(geminiEmbedSlot));

        ProviderReservation mockReservation = new ProviderReservation(
                "token-123", geminiEmbedSlot, 80L, "rate-key", "day-key", "res-id"
        );
        when(capacityService.tryReserve(eq(geminiEmbedSlot), anyLong()))
                .thenReturn(Optional.of(mockReservation));

        AiEmbeddingResult result = executionService.embed(decision, request, "regulatory-compliance-rag", tokenBudget);

        assertNotNull(result);
        assertEquals("GEMINI", result.provider());
        assertEquals("text-embedding-004", result.model());
        assertEquals(768, result.vector().size());
        assertTrue(result.vector().stream().allMatch(Float::isFinite));
    }

    @Test
    @DisplayName("knowledge.embed executes on EMBED slot and produces exactly 768-dimension vector")
    void testKnowledgeEmbedExecution() {
        GovernanceDecision decision = new GovernanceDecision(
                true,
                null,
                UUID.randomUUID().toString(),
                Set.of(AiProvider.GEMINI),
                Set.of("shared-ai"),
                ModelTier.STANDARD,
                4096,
                AutomationLevel.SEMI_AUTONOMOUS,
                false,
                "v1"
        );

        AiEmbeddingRequest request = new AiEmbeddingRequest("knowledge.embed", "Internal customs SOP section 4.2", 768, 50L);
        TokenBudget tokenBudget = new TokenBudget(50L, 0L);

        when(routingService.getCandidates(any(), any(), eq(AiOperation.EMBED), anyString(), any()))
                .thenReturn(List.of(geminiEmbedSlot));

        ProviderReservation mockReservation = new ProviderReservation(
                "token-456", geminiEmbedSlot, 50L, "rate-key", "day-key", "res-id"
        );
        when(capacityService.tryReserve(eq(geminiEmbedSlot), anyLong()))
                .thenReturn(Optional.of(mockReservation));

        AiEmbeddingResult result = executionService.embed(decision, request, "regulatory-compliance-rag", tokenBudget);

        assertNotNull(result);
        assertEquals(768, result.vector().size());
    }

    @Test
    @DisplayName("Azure OpenAI fallback slot returns compatible 768-dimension vector")
    void testAzureOpenAiFallbackSlotDimensionCompatibility() {
        ProviderSlot azureSlot = new ProviderSlot();
        azureSlot.setSlotAlias("azure-embed-01");
        azureSlot.setProvider(AiProvider.AZURE_OPENAI);
        azureSlot.setOperation(AiOperation.EMBED);
        azureSlot.setModelName("text-embedding-3-small");
        azureSlot.setSecretRef("azure-key");

        AiEmbeddingRequest request = new AiEmbeddingRequest("compliance.embed", "Carrier liability contract clause", 768, 60L);
        AiEmbeddingResult result = azureClient.embed(azureSlot, request);

        assertNotNull(result);
        assertEquals("AZURE_OPENAI", result.provider());
        assertEquals(768, result.vector().size());
        assertTrue(result.vector().stream().allMatch(Float::isFinite));
    }
}

package com.aurora.aigovernance.gateway.application.execution;

import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.Set;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyLong;
import static org.mockito.ArgumentMatchers.eq;
import org.mockito.Mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;
import org.mockito.junit.jupiter.MockitoExtension;

import com.aurora.aigovernance.gateway.application.capacity.CapacityReservationService;
import com.aurora.aigovernance.gateway.application.port.AiProviderClient;
import com.aurora.aigovernance.gateway.application.routing.ProviderRoutingService;
import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateRequest;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateResult;
import com.aurora.aigovernance.gateway.domain.valueobject.ProviderReservation;
import com.aurora.aigovernance.gateway.infrastructure.persistence.ProviderSlotRepository;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.governance.domain.enums.AutomationLevel;
import com.aurora.aigovernance.governance.domain.enums.ModelTier;
import com.aurora.aigovernance.governance.domain.valueobject.GovernanceDecision;
import com.aurora.aigovernance.shared.domain.AiOperation;
import com.aurora.aigovernance.shared.domain.TokenBudget;

@ExtendWith(MockitoExtension.class)
public class DevOpsInternalProviderFallbackTest {

    @Mock
    private ProviderRoutingService routingService;

    @Mock
    private CapacityReservationService capacityService;

    @Mock
    private AiProviderClient azureClient;

    @Mock
    private AiProviderClient geminiClient;

    @Mock
    private ProviderSlotRepository slotRepository;

    private AiExecutionService aiExecutionService;

    @BeforeEach
    public void setup() {
        aiExecutionService = new AiExecutionService(
                routingService,
                capacityService,
                Map.of("azureOpenAiProviderClient", azureClient,
                       "geminiProviderClient", geminiClient),
                slotRepository
        );
    }

    @Test
    public void testAzureFailure_FallbacksToGeminiInsideDevOpsPool() {
        ProviderSlot azureSlot = new ProviderSlot();
        azureSlot.setSlotAlias("azure-devops-generate-01");
        azureSlot.setProvider(AiProvider.AZURE_OPENAI);
        azureSlot.setOperation(AiOperation.GENERATE);
        azureSlot.setPriority(1);

        ProviderSlot geminiFallbackSlot = new ProviderSlot();
        geminiFallbackSlot.setSlotAlias("gemini-devops-generate-01");
        geminiFallbackSlot.setProvider(AiProvider.GEMINI);
        geminiFallbackSlot.setOperation(AiOperation.GENERATE);
        geminiFallbackSlot.setPriority(10);

        GovernanceDecision decision = new GovernanceDecision(
                true, null, "dec-devops", Set.of(AiProvider.AZURE_OPENAI, AiProvider.GEMINI),
                Set.of("devops-internal"), ModelTier.PREMIUM, 8192,
                AutomationLevel.SUPERVISED_AUTONOMOUS, false, "v1"
        );

        when(routingService.getCandidates(any(), any(), eq(AiOperation.GENERATE), eq("devops-agent"), any()))
                .thenReturn(List.of(azureSlot, geminiFallbackSlot));

        // Both reserve successfully
        ProviderReservation resAzure = new ProviderReservation("res-az", azureSlot, 1000, "rpm", "tpm", "rpd");
        ProviderReservation resGemini = new ProviderReservation("res-gem", geminiFallbackSlot, 1000, "rpm", "tpm", "rpd");
        when(capacityService.tryReserve(eq(azureSlot), anyLong())).thenReturn(Optional.of(resAzure));
        when(capacityService.tryReserve(eq(geminiFallbackSlot), anyLong())).thenReturn(Optional.of(resGemini));

        // Azure fails with 429 rate limit error
        when(azureClient.generate(eq(azureSlot), any())).thenThrow(new RuntimeException("Azure 429 Rate Limit Exceeded"));

        // Gemini fallback succeeds
        AiGenerateResult fallbackResult = new AiGenerateResult("Gemini fallback response", 200, 100, "gemini-1.5-flash", "GEMINI");
        when(geminiClient.generate(eq(geminiFallbackSlot), any())).thenReturn(fallbackResult);

        AiGenerateRequest request = new AiGenerateRequest("devops.diagnose", "diagnose pod", 500, 500, Map.of());
        AiGenerateResult finalResult = aiExecutionService.generate(decision, request, "devops-agent", new TokenBudget(500, 500));

        assertNotNull(finalResult);
        assertEquals("Gemini fallback response", finalResult.content());
        assertEquals("GEMINI", finalResult.provider());
        verify(capacityService).release(eq(resAzure)); // Azure reservation released
        verify(capacityService).reconcile(eq(resGemini), eq(300L)); // Gemini reconciled
    }
}

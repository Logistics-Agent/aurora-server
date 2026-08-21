package com.aurora.aigovernance.gateway.application.execution;

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
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.Set;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
public class CapacityContentionFallbackTest {

    @Mock
    private ProviderRoutingService routingService;

    @Mock
    private CapacityReservationService capacityService;

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
                Map.of("geminiProviderClient", geminiClient),
                slotRepository
        );
    }

    @Test
    public void testCandidateAContention_AdvancesToCandidateB() {
        ProviderSlot candidateA = new ProviderSlot();
        candidateA.setSlotAlias("slot-A");
        candidateA.setProvider(AiProvider.GEMINI);
        candidateA.setOperation(AiOperation.GENERATE);

        ProviderSlot candidateB = new ProviderSlot();
        candidateB.setSlotAlias("slot-B");
        candidateB.setProvider(AiProvider.GEMINI);
        candidateB.setOperation(AiOperation.GENERATE);

        GovernanceDecision decision = new GovernanceDecision(
                true, null, "dec-1", Set.of(AiProvider.GEMINI),
                Set.of("shared-ai"), ModelTier.STANDARD, 4096,
                AutomationLevel.ASSISTED, false, "v1"
        );

        when(routingService.getCandidates(any(), any(), eq(AiOperation.GENERATE), eq("service-a"), any()))
                .thenReturn(List.of(candidateA, candidateB));

        // Candidate A fails reservation (contention)
        when(capacityService.tryReserve(eq(candidateA), anyLong())).thenReturn(Optional.empty());

        // Candidate B succeeds reservation
        ProviderReservation resB = new ProviderReservation("res-b", candidateB, 1000, "rpm", "tpm", "rpd");
        when(capacityService.tryReserve(eq(candidateB), anyLong())).thenReturn(Optional.of(resB));

        AiGenerateResult resultB = new AiGenerateResult("Response from B", 100, 50, "gemini-1.5-flash", "GEMINI");
        when(geminiClient.generate(eq(candidateB), any())).thenReturn(resultB);

        AiGenerateRequest request = new AiGenerateRequest("compliance.answer", "prompt", 500, 500, Map.of());
        AiGenerateResult finalResult = aiExecutionService.generate(decision, request, "service-a", new TokenBudget(500, 500));

        assertNotNull(finalResult);
        assertEquals("Response from B", finalResult.content());
        verify(capacityService).tryReserve(eq(candidateA), eq(1000L));
        verify(capacityService).tryReserve(eq(candidateB), eq(1000L));
        verify(geminiClient, never()).generate(eq(candidateA), any());
        verify(geminiClient).generate(eq(candidateB), any());
        verify(capacityService).reconcile(eq(resB), eq(150L));
    }
}

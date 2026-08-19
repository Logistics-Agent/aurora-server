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
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.Set;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
public class GenerateTokenReconciliationTest {

    @Mock
    private ProviderRoutingService routingService;

    @Mock
    private CapacityReservationService capacityService;

    @Mock
    private AiProviderClient geminiClient;

    @Mock
    private ProviderSlotRepository slotRepository;

    @Test
    public void testGenerateActualUsage_ReconcilesTotalInputPlusOutput() {
        AiExecutionService service = new AiExecutionService(
                routingService, capacityService,
                Map.of("geminiProviderClient", geminiClient),
                slotRepository
        );

        ProviderSlot slot = new ProviderSlot();
        slot.setSlotAlias("gemini-1");
        slot.setProvider(AiProvider.GEMINI);
        slot.setOperation(AiOperation.GENERATE);

        GovernanceDecision decision = new GovernanceDecision(
                true, null, "dec-1", Set.of(AiProvider.GEMINI),
                Set.of("shared-ai"), ModelTier.STANDARD, 4096,
                AutomationLevel.ASSISTED, false, "v1"
        );

        ProviderReservation reservation = new ProviderReservation("res-1", slot, 7000, "rpm", "tpm", "rpd");
        when(routingService.getCandidates(any(), any(), eq(AiOperation.GENERATE), eq("service-a"), any()))
                .thenReturn(List.of(slot));
        when(capacityService.tryReserve(eq(slot), eq(7000L))).thenReturn(Optional.of(reservation));

        // Provider actually produced: 2800 input + 1200 output = 4000 total tokens
        AiGenerateResult result = new AiGenerateResult("response", 2800, 1200, "gemini-1.5-flash", "GEMINI");
        when(geminiClient.generate(eq(slot), any())).thenReturn(result);

        AiGenerateRequest request = new AiGenerateRequest("compliance.answer", "prompt", 4000, 3000, Map.of());
        service.generate(decision, request, "service-a", new TokenBudget(3000, 4000));

        // Verify reconciliation called with actual 4000 tokens
        verify(capacityService).reconcile(eq(reservation), eq(4000L));
    }
}

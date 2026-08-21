package com.aurora.aigovernance.gateway.application.execution;

import com.aurora.aigovernance.gateway.application.capacity.CapacityReservationService;
import com.aurora.aigovernance.gateway.application.port.AiProviderClient;
import com.aurora.aigovernance.gateway.application.routing.ProviderRoutingService;
import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateRequest;
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

import java.util.Collections;
import java.util.List;
import java.util.Map;
import java.util.Set;

import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
public class AllCandidatesExhaustedTest {

    @Mock
    private ProviderRoutingService routingService;

    @Mock
    private CapacityReservationService capacityService;

    @Mock
    private ProviderSlotRepository slotRepository;

    @Test
    public void testNoCandidatesAvailable_ThrowsProviderCapacityExhausted() {
        AiExecutionService service = new AiExecutionService(
                routingService, capacityService,
                Collections.emptyMap(),
                slotRepository
        );

        GovernanceDecision decision = new GovernanceDecision(
                true, null, "dec-1", Set.of(AiProvider.GEMINI),
                Set.of("shared-ai"), ModelTier.STANDARD, 4096,
                AutomationLevel.ASSISTED, false, "v1"
        );

        when(routingService.getCandidates(any(), any(), eq(AiOperation.GENERATE), eq("service-a"), any()))
                .thenReturn(Collections.emptyList());

        AiGenerateRequest request = new AiGenerateRequest("compliance.answer", "prompt", 100, 100, Map.of());

        IllegalStateException ex = assertThrows(IllegalStateException.class, () ->
                service.generate(decision, request, "service-a", new TokenBudget(100, 100)));

        assertTrue(ex.getMessage().contains("PROVIDER_CAPACITY_EXHAUSTED"));
    }
}

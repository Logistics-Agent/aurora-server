package com.aurora.aigovernance.gateway.application.execution;

import com.aurora.aigovernance.gateway.application.capacity.CapacityReservationService;
import com.aurora.aigovernance.gateway.application.port.AiProviderClient;
import com.aurora.aigovernance.gateway.application.routing.ProviderRoutingService;
import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateRequest;
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

import java.net.SocketTimeoutException;
import java.util.List;
import java.util.Map;
import java.util.Optional;
import java.util.Set;

import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
public class ReservationFailureSemanticsTest {

    @Mock
    private ProviderRoutingService routingService;

    @Mock
    private CapacityReservationService capacityService;

    @Mock
    private AiProviderClient geminiClient;

    @Mock
    private ProviderSlotRepository slotRepository;

    private AiExecutionService aiExecutionService;

    private ProviderSlot slot;
    private GovernanceDecision decision;
    private AiGenerateRequest request;
    private ProviderReservation reservation;

    @BeforeEach
    public void setup() {
        aiExecutionService = new AiExecutionService(
                routingService,
                capacityService,
                Map.of("geminiProviderClient", geminiClient),
                slotRepository
        );

        slot = new ProviderSlot();
        slot.setSlotAlias("gemini-slot-1");
        slot.setProvider(AiProvider.GEMINI);
        slot.setOperation(AiOperation.GENERATE);

        decision = new GovernanceDecision(
                true, null, "dec-1", Set.of(AiProvider.GEMINI),
                Set.of("shared-ai"), ModelTier.STANDARD, 4096,
                AutomationLevel.ASSISTED, false, "v1"
        );

        request = new AiGenerateRequest("compliance.answer", "prompt", 500, 500, Map.of());
        reservation = new ProviderReservation("res-1", slot, 1000, "rpm", "tpm", "rpd");

        when(routingService.getCandidates(any(), any(), eq(AiOperation.GENERATE), eq("service-a"), any()))
                .thenReturn(List.of(slot));
        when(capacityService.tryReserve(eq(slot), anyLong())).thenReturn(Optional.of(reservation));
    }

    @Test
    public void testExplicitRateLimit429_ReleasesReservation() {
        when(geminiClient.generate(eq(slot), any())).thenThrow(new RuntimeException("429 Too Many Requests"));

        assertThrows(IllegalStateException.class, () ->
                aiExecutionService.generate(decision, request, "service-a", new TokenBudget(500, 500)));

        verify(capacityService).release(eq(reservation));
    }

    @Test
    public void testAmbiguousTimeout_DoesNotReleaseReservation_UncertainState() {
        when(geminiClient.generate(eq(slot), any())).thenThrow(new RuntimeException("Connection timed out waiting for response", new SocketTimeoutException()));

        assertThrows(IllegalStateException.class, () ->
                aiExecutionService.generate(decision, request, "service-a", new TokenBudget(500, 500)));

        // Verification of Invariant: UNCERTAIN state must NOT release reservation to prevent capacity under-counting
        verify(capacityService, never()).release(eq(reservation));
    }

    @Test
    public void testAmbiguous500InternalServerError_DoesNotReleaseReservation() {
        when(geminiClient.generate(eq(slot), any())).thenThrow(new RuntimeException("500 Internal Server Error from upstream"));

        assertThrows(IllegalStateException.class, () ->
                aiExecutionService.generate(decision, request, "service-a", new TokenBudget(500, 500)));

        verify(capacityService, never()).release(eq(reservation));
    }
}

package com.aurora.aigovernance.gateway.application.execution;

import com.aurora.aigovernance.gateway.application.capacity.CapacityReservationService;
import com.aurora.aigovernance.gateway.application.port.AiProviderClient;
import com.aurora.aigovernance.gateway.application.routing.ProviderRoutingService;
import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.AiEmbeddingRequest;
import com.aurora.aigovernance.gateway.domain.valueobject.AiEmbeddingResult;
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
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
public class EmbeddingAccountingTest {

    @Mock
    private ProviderRoutingService routingService;

    @Mock
    private CapacityReservationService capacityService;

    @Mock
    private AiProviderClient geminiClient;

    @Mock
    private ProviderSlotRepository slotRepository;

    @Test
    public void testEmbeddingAccounting_UsesInputTokensOnly() {
        AiExecutionService service = new AiExecutionService(
                routingService, capacityService,
                Map.of("geminiProviderClient", geminiClient),
                slotRepository
        );

        ProviderSlot slot = new ProviderSlot();
        slot.setSlotAlias("gemini-embed-1");
        slot.setProvider(AiProvider.GEMINI);
        slot.setOperation(AiOperation.EMBED);

        GovernanceDecision decision = new GovernanceDecision(
                true, null, "dec-embed", Set.of(AiProvider.GEMINI),
                Set.of("shared-ai"), ModelTier.STANDARD, 4096,
                AutomationLevel.ASSISTED, false, "v1"
        );

        ProviderReservation reservation = new ProviderReservation("res-embed-1", slot, 500, "rpm", "tpm", "rpd");
        when(routingService.getCandidates(any(), any(), eq(AiOperation.EMBED), eq("service-a"), any()))
                .thenReturn(List.of(slot));
        when(capacityService.tryReserve(eq(slot), eq(500L))).thenReturn(Optional.of(reservation));

        AiEmbeddingResult result = new AiEmbeddingResult(List.of(0.1f, 0.2f), 450L, "text-embedding-004", "GEMINI");
        when(geminiClient.embed(eq(slot), any())).thenReturn(result);

        AiEmbeddingRequest request = new AiEmbeddingRequest("compliance.answer", "content", 768, 500L);
        service.embed(decision, request, "service-a", TokenBudget.forEmbedding(500L));

        // Verify embedding reconciles actual input tokens (450) without output token concepts
        verify(capacityService).reconcile(eq(reservation), eq(450L));
    }
}

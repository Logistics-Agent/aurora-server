package com.aurora.aigovernance.gateway.application.routing;

import com.aurora.aigovernance.gateway.application.capacity.CapacityReservationService;
import com.aurora.aigovernance.gateway.domain.entity.ProviderPool;
import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.ProviderCapacityLimits;
import com.aurora.aigovernance.gateway.domain.valueobject.SlotCapacity;
import com.aurora.aigovernance.gateway.infrastructure.persistence.ProviderSlotRepository;
import com.aurora.aigovernance.gateway.infrastructure.persistence.ServiceProviderPoolPolicyRepository;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.shared.domain.AiOperation;
import com.aurora.aigovernance.shared.domain.TokenBudget;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.time.OffsetDateTime;
import java.util.Collections;
import java.util.List;
import java.util.Set;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
public class ProviderRoutingServiceTest {

    @Mock
    private ProviderSlotRepository slotRepository;

    @Mock
    private ServiceProviderPoolPolicyRepository poolPolicyRepository;

    @Mock
    private CapacityReservationService capacityService;

    private ProviderRoutingServiceImpl routingService;

    @BeforeEach
    public void setup() {
        routingService = new ProviderRoutingServiceImpl(
                slotRepository,
                poolPolicyRepository,
                capacityService
        );
    }

    @Test
    public void testOperationFiltering_GeneratesOnlyGenerateSlots() {
        ProviderPool pool = new ProviderPool();
        pool.setCode("shared-ai");

        ProviderSlot slotGen = new ProviderSlot();
        slotGen.setSlotAlias("slot-gen");
        slotGen.setPool(pool);
        slotGen.setProvider(AiProvider.GEMINI);
        slotGen.setOperation(AiOperation.GENERATE);
        slotGen.setPriority(1);
        slotGen.setEnabled(true);

        when(poolPolicyRepository.findByServiceIdOrderByPriorityAsc("service-a")).thenReturn(Collections.emptyList());
        when(slotRepository.findActiveCandidateSlots(eq(Set.of("shared-ai")), eq(Set.of(AiProvider.GEMINI)), eq(AiOperation.GENERATE)))
                .thenReturn(List.of(slotGen));

        when(capacityService.getEffectiveLimits(slotGen)).thenReturn(new ProviderCapacityLimits(12, 200000, 400));
        when(capacityService.getSlotCapacity(slotGen)).thenReturn(new SlotCapacity(0, 0, 0));

        List<ProviderSlot> candidates = routingService.getCandidates(
                Set.of(AiProvider.GEMINI),
                Set.of("shared-ai"),
                AiOperation.GENERATE,
                "service-a",
                new TokenBudget(100, 100)
        );

        assertEquals(1, candidates.size());
        assertEquals("slot-gen", candidates.get(0).getSlotAlias());
        assertEquals(AiOperation.GENERATE, candidates.get(0).getOperation());
    }

    @Test
    public void testCooldownSlots_AreFilteredOut() {
        ProviderPool pool = new ProviderPool();
        pool.setCode("shared-ai");

        ProviderSlot slotInCooldown = new ProviderSlot();
        slotInCooldown.setSlotAlias("slot-cooldown");
        slotInCooldown.setPool(pool);
        slotInCooldown.setProvider(AiProvider.GEMINI);
        slotInCooldown.setOperation(AiOperation.GENERATE);
        slotInCooldown.setCooldownUntil(OffsetDateTime.now().plusMinutes(5));
        slotInCooldown.setEnabled(true);

        when(poolPolicyRepository.findByServiceIdOrderByPriorityAsc("service-a")).thenReturn(Collections.emptyList());
        when(slotRepository.findActiveCandidateSlots(any(), any(), any()))
                .thenReturn(List.of(slotInCooldown));

        List<ProviderSlot> candidates = routingService.getCandidates(
                Set.of(AiProvider.GEMINI),
                Set.of("shared-ai"),
                AiOperation.GENERATE,
                "service-a",
                new TokenBudget(100, 100)
        );

        assertTrue(candidates.isEmpty());
    }

    @Test
    public void testPriorityOrdering_Priority1BeforePriority10() {
        ProviderPool pool = new ProviderPool();
        pool.setCode("devops-internal");

        ProviderSlot slotAzure = new ProviderSlot();
        slotAzure.setSlotAlias("azure-primary");
        slotAzure.setPool(pool);
        slotAzure.setProvider(AiProvider.AZURE_OPENAI);
        slotAzure.setOperation(AiOperation.GENERATE);
        slotAzure.setPriority(1);
        slotAzure.setEnabled(true);

        ProviderSlot slotGemini = new ProviderSlot();
        slotGemini.setSlotAlias("gemini-fallback");
        slotGemini.setPool(pool);
        slotGemini.setProvider(AiProvider.GEMINI);
        slotGemini.setOperation(AiOperation.GENERATE);
        slotGemini.setPriority(10);
        slotGemini.setEnabled(true);

        when(poolPolicyRepository.findByServiceIdOrderByPriorityAsc("devops-agent")).thenReturn(Collections.emptyList());
        when(slotRepository.findActiveCandidateSlots(any(), any(), any()))
                .thenReturn(List.of(slotGemini, slotAzure)); // DB returned in unordered list

        when(capacityService.getEffectiveLimits(any())).thenReturn(new ProviderCapacityLimits(20, 200000, 400));
        when(capacityService.getSlotCapacity(any())).thenReturn(new SlotCapacity(0, 0, 0));

        List<ProviderSlot> candidates = routingService.getCandidates(
                Set.of(AiProvider.AZURE_OPENAI, AiProvider.GEMINI),
                Set.of("devops-internal"),
                AiOperation.GENERATE,
                "devops-agent",
                new TokenBudget(100, 100)
        );

        assertEquals(2, candidates.size());
        assertEquals("azure-primary", candidates.get(0).getSlotAlias());
        assertEquals("gemini-fallback", candidates.get(1).getSlotAlias());
    }
}

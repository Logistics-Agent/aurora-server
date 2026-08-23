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
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import java.util.Collections;
import java.util.List;
import java.util.Set;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
public class SharedPoolIsolationTest {

    @Mock
    private ProviderSlotRepository slotRepository;

    @Mock
    private ServiceProviderPoolPolicyRepository poolPolicyRepository;

    @Mock
    private CapacityReservationService capacityService;

    @Test
    public void testAllTenantsShareSameSlotPool_NoFixedProjectPinning() {
        ProviderRoutingServiceImpl routingService = new ProviderRoutingServiceImpl(
                slotRepository, poolPolicyRepository, capacityService
        );

        ProviderPool sharedPool = new ProviderPool();
        sharedPool.setCode("shared-ai");

        ProviderSlot slot1 = new ProviderSlot();
        slot1.setSlotAlias("gemini-shared-generate-01");
        slot1.setPool(sharedPool);
        slot1.setProvider(AiProvider.GEMINI);
        slot1.setOperation(AiOperation.GENERATE);
        slot1.setPriority(1);

        ProviderSlot slot2 = new ProviderSlot();
        slot2.setSlotAlias("gemini-shared-generate-02");
        slot2.setPool(sharedPool);
        slot2.setProvider(AiProvider.GEMINI);
        slot2.setOperation(AiOperation.GENERATE);
        slot2.setPriority(1);

        when(poolPolicyRepository.findByServiceIdOrderByPriorityAsc(any())).thenReturn(Collections.emptyList());
        when(slotRepository.findActiveCandidateSlots(any(), any(), any())).thenReturn(List.of(slot1, slot2));
        when(capacityService.getEffectiveLimits(any())).thenReturn(new ProviderCapacityLimits(12, 200000, 400));
        when(capacityService.getSlotCapacity(any())).thenReturn(new SlotCapacity(0, 0, 0));

        // When Tenant A queries routing
        List<ProviderSlot> candidatesTenantA = routingService.getCandidates(
                Set.of(AiProvider.GEMINI), Set.of("shared-ai"), AiOperation.GENERATE, "service-1", new TokenBudget(100, 100)
        );

        // When Tenant B queries routing
        List<ProviderSlot> candidatesTenantB = routingService.getCandidates(
                Set.of(AiProvider.GEMINI), Set.of("shared-ai"), AiOperation.GENERATE, "service-2", new TokenBudget(100, 100)
        );

        // Then both tenants receive candidates from the same shared pool
        assertEquals(2, candidatesTenantA.size());
        assertEquals(2, candidatesTenantB.size());
    }
}

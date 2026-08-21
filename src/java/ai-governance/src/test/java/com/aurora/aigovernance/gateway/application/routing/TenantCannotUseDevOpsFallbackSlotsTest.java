package com.aurora.aigovernance.gateway.application.routing;

import com.aurora.aigovernance.gateway.application.capacity.CapacityReservationService;
import com.aurora.aigovernance.gateway.domain.entity.ProviderPool;
import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.entity.ServiceProviderPoolPolicy;
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

import java.util.List;
import java.util.Set;

import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
public class TenantCannotUseDevOpsFallbackSlotsTest {

    @Mock
    private ProviderSlotRepository slotRepository;

    @Mock
    private ServiceProviderPoolPolicyRepository poolPolicyRepository;

    @Mock
    private CapacityReservationService capacityService;

    @Test
    public void testTenantService_CannotAccessDevOpsInternalPoolSlots() {
        ProviderRoutingServiceImpl routingService = new ProviderRoutingServiceImpl(
                slotRepository, poolPolicyRepository, capacityService
        );

        ProviderPool sharedPool = new ProviderPool();
        sharedPool.setCode("shared-ai");

        ServiceProviderPoolPolicy policy = new ServiceProviderPoolPolicy();
        policy.setServiceId("regulatory-compliance-rag");
        policy.setPool(sharedPool);
        policy.setPriority(1);

        when(poolPolicyRepository.findByServiceIdOrderByPriorityAsc("regulatory-compliance-rag"))
                .thenReturn(List.of(policy));

        // When DB query executes, poolCodes parameter is strictly Set.of("shared-ai")
        when(slotRepository.findActiveCandidateSlots(eq(Set.of("shared-ai")), any(), any()))
                .thenReturn(List.of());

        List<ProviderSlot> candidates = routingService.getCandidates(
                Set.of(AiProvider.GEMINI),
                Set.of("shared-ai"),
                AiOperation.GENERATE,
                "regulatory-compliance-rag",
                new TokenBudget(100, 100)
        );

        // Verification: candidate list never includes slots from devops-internal
        assertTrue(candidates.isEmpty());
    }
}

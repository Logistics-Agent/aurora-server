package com.aurora.aigovernance.gateway.application.routing;

import com.aurora.aigovernance.gateway.application.capacity.CapacityReservationService;
import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.entity.ServiceProviderPoolPolicy;
import com.aurora.aigovernance.gateway.domain.valueobject.ProviderCapacityLimits;
import com.aurora.aigovernance.gateway.domain.valueobject.SlotCapacity;
import com.aurora.aigovernance.gateway.infrastructure.persistence.ProviderSlotRepository;
import com.aurora.aigovernance.gateway.infrastructure.persistence.ServiceProviderPoolPolicyRepository;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.shared.domain.AiOperation;
import com.aurora.aigovernance.shared.domain.TokenBudget;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.*;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.stream.Collectors;

@Service
public class ProviderRoutingServiceImpl implements ProviderRoutingService {

    private static final Logger log = LoggerFactory.getLogger(ProviderRoutingServiceImpl.class);

    private final ProviderSlotRepository slotRepository;
    private final ServiceProviderPoolPolicyRepository poolPolicyRepository;
    private final CapacityReservationService capacityService;

    private final AtomicInteger roundRobinCounter = new AtomicInteger(0);

    public ProviderRoutingServiceImpl(
            ProviderSlotRepository slotRepository,
            ServiceProviderPoolPolicyRepository poolPolicyRepository,
            CapacityReservationService capacityService) {
        this.slotRepository = slotRepository;
        this.poolPolicyRepository = poolPolicyRepository;
        this.capacityService = capacityService;
    }

    @Override
    @Transactional(readOnly = true)
    public List<ProviderSlot> getCandidates(
            Set<AiProvider> allowedProviders,
            Set<String> allowedProviderPools,
            AiOperation operation,
            String callerServiceId,
            TokenBudget tokenBudget) {

        // 1. Resolve effective pools for callerServiceId
        Set<String> effectivePools = resolveEffectivePools(callerServiceId, allowedProviderPools);
        if (effectivePools.isEmpty() || allowedProviders.isEmpty()) {
            log.warn("No candidate pools/providers found for serviceId: {}", callerServiceId);
            return Collections.emptyList();
        }

        // 2. Query slots matching pools, providers, operation, enabled
        List<ProviderSlot> slots = slotRepository.findActiveCandidateSlots(
                effectivePools,
                allowedProviders,
                operation
        );

        if (slots.isEmpty()) {
            log.warn("No active slots in DB for pools={}, providers={}, operation={}",
                    effectivePools, allowedProviders, operation);
            return Collections.emptyList();
        }

        // 3. Filter out slots currently in cooldown
        List<ProviderSlot> availableSlots = slots.stream()
                .filter(slot -> !slot.isInCooldown())
                .toList();

        if (availableSlots.isEmpty()) {
            log.warn("All candidate slots in cooldown for pools={}, providers={}", effectivePools, allowedProviders);
            return Collections.emptyList();
        }

        // 4. Rank candidates by:
        //    a) priority ASC
        //    b) lowest utilization (using effective limits)
        //    c) round-robin tie-break
        long requiredTokens = tokenBudget.reservationTokens();
        Map<ProviderSlot, SlotScore> scoredSlots = new HashMap<>();

        for (ProviderSlot slot : availableSlots) {
            ProviderCapacityLimits effectiveLimits = capacityService.getEffectiveLimits(slot);
            SlotCapacity currentUsage = capacityService.getSlotCapacity(slot);

            // Pre-check soft capacity filter
            if (currentUsage.currentRpm() >= effectiveLimits.rpmLimit() ||
                currentUsage.currentRpd() >= effectiveLimits.rpdLimit() ||
                (currentUsage.currentTpm() + requiredTokens) > effectiveLimits.tpmLimit()) {
                log.debug("Slot {} skipped in pre-check (RPM={}/{}, TPM={}/{}, RPD={}/{})",
                        slot.getSlotAlias(),
                        currentUsage.currentRpm(), effectiveLimits.rpmLimit(),
                        currentUsage.currentTpm() + requiredTokens, effectiveLimits.tpmLimit(),
                        currentUsage.currentRpd(), effectiveLimits.rpdLimit());
                continue;
            }

            double rpmUtil = (double) currentUsage.currentRpm() / Math.max(1, effectiveLimits.rpmLimit());
            double tpmUtil = (double) currentUsage.currentTpm() / Math.max(1, effectiveLimits.tpmLimit());
            double rpdUtil = (double) currentUsage.currentRpd() / Math.max(1, effectiveLimits.rpdLimit());
            double maxUtil = Math.max(rpmUtil, Math.max(tpmUtil, rpdUtil));

            scoredSlots.put(slot, new SlotScore(slot.getPriority(), maxUtil));
        }

        // If all pre-filtered out, fallback to all available slots (Redis Lua is authoritative anyway)
        List<ProviderSlot> candidatePool = scoredSlots.isEmpty() ? availableSlots : new ArrayList<>(scoredSlots.keySet());

        int rrOffset = Math.abs(roundRobinCounter.getAndIncrement());

        return candidatePool.stream()
                .sorted((s1, s2) -> {
                    SlotScore sc1 = scoredSlots.getOrDefault(s1, new SlotScore(s1.getPriority(), 1.0));
                    SlotScore sc2 = scoredSlots.getOrDefault(s2, new SlotScore(s2.getPriority(), 1.0));

                    // First by Priority ASC
                    int pComp = Integer.compare(sc1.priority, sc2.priority);
                    if (pComp != 0) return pComp;

                    // Then by Utilization ASC
                    int uComp = Double.compare(sc1.utilization, sc2.utilization);
                    if (uComp != 0) return uComp;

                    // Deterministic tie-break
                    return Integer.compare(
                            (s1.getSlotAlias().hashCode() + rrOffset) & 0x7FFFFFFF,
                            (s2.getSlotAlias().hashCode() + rrOffset) & 0x7FFFFFFF
                    );
                })
                .collect(Collectors.toList());
    }

    private Set<String> resolveEffectivePools(String callerServiceId, Set<String> allowedProviderPools) {
        // Look up ServiceProviderPoolPolicy for this service
        List<ServiceProviderPoolPolicy> policies = poolPolicyRepository
                .findByServiceIdOrderByPriorityAsc(callerServiceId);

        if (!policies.isEmpty()) {
            return policies.stream()
                    .map(p -> p.getPool().getCode())
                    .collect(Collectors.toSet());
        }

        // Default to allowedProviderPools from governance
        return allowedProviderPools;
    }

    private record SlotScore(int priority, double utilization) {}
}

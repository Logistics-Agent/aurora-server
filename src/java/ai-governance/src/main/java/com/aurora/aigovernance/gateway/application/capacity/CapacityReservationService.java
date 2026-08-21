package com.aurora.aigovernance.gateway.application.capacity;

import com.aurora.aigovernance.gateway.application.port.ProviderCapacityPort;
import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.ProviderCapacityLimits;
import com.aurora.aigovernance.gateway.domain.valueobject.ProviderReservation;
import com.aurora.aigovernance.gateway.domain.valueobject.SlotCapacity;
import org.springframework.stereotype.Service;

import java.util.Optional;

@Service
public class CapacityReservationService {

    private final ProviderCapacityPort providerCapacityPort;
    private final ProviderCapacityLimitPolicy capacityLimitPolicy;

    public CapacityReservationService(
            ProviderCapacityPort providerCapacityPort,
            ProviderCapacityLimitPolicy capacityLimitPolicy) {
        this.providerCapacityPort = providerCapacityPort;
        this.capacityLimitPolicy = capacityLimitPolicy;
    }

    public Optional<ProviderReservation> tryReserve(ProviderSlot slot, long requestedTokens) {
        ProviderCapacityLimits effectiveLimits = capacityLimitPolicy.effectiveLimits(slot);
        return providerCapacityPort.tryReserve(slot, effectiveLimits, requestedTokens);
    }

    public void reconcile(ProviderReservation reservation, long actualTokens) {
        providerCapacityPort.reconcile(reservation, actualTokens);
    }

    public void release(ProviderReservation reservation) {
        providerCapacityPort.release(reservation);
    }

    public SlotCapacity getSlotCapacity(ProviderSlot slot) {
        return providerCapacityPort.getSlotCapacity(slot);
    }

    public ProviderCapacityLimits getEffectiveLimits(ProviderSlot slot) {
        return capacityLimitPolicy.effectiveLimits(slot);
    }
}

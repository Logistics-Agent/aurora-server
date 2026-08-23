package com.aurora.aigovernance.gateway.application.port;

import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.ProviderCapacityLimits;
import com.aurora.aigovernance.gateway.domain.valueobject.ProviderReservation;
import com.aurora.aigovernance.gateway.domain.valueobject.SlotCapacity;

import java.util.Optional;

/**
 * Port for Redis-backed atomic capacity reservation and reconciliation.
 */
public interface ProviderCapacityPort {

    /**
     * Attempt atomic reservation against effective limits.
     * Returns reservation if successful, empty if capacity exhausted/contention.
     */
    Optional<ProviderReservation> tryReserve(
            ProviderSlot slot,
            ProviderCapacityLimits effectiveLimits,
            long requestedTokens
    );

    /**
     * Reconcile actual tokens used vs reserved amount.
     */
    void reconcile(ProviderReservation reservation, long actualTokens);

    /**
     * Release reservation completely (e.g. failure before send or explicit provider reject).
     */
    void release(ProviderReservation reservation);

    /**
     * Read current slot capacity usage for ranking.
     */
    SlotCapacity getSlotCapacity(ProviderSlot slot);
}

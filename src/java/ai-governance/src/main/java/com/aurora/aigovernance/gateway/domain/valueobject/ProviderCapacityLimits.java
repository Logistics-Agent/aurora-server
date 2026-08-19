package com.aurora.aigovernance.gateway.domain.valueobject;

/**
 * Effective provider capacity limits after headroom deduction.
 * <p>
 * Computed by {@code ProviderCapacityLimitPolicy}: {@code floor(physical × (1 - headroom))}.
 * Passed to Redis Lua for atomic reservation — Lua never hard-codes headroom.
 */
public record ProviderCapacityLimits(
        long rpmLimit,
        long tpmLimit,
        long rpdLimit
) {}

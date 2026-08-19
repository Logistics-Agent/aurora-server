package com.aurora.aigovernance.gateway.application.capacity;

import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.ProviderCapacityLimits;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;

public class ProviderHeadroomEffectiveLimitTest {

    @Test
    public void testEffectiveLimitFormula_TwentyPercentHeadroom() {
        ProviderCapacityLimitPolicy policy = new ProviderCapacityLimitPolicy(0.20);

        ProviderSlot slot = new ProviderSlot();
        slot.setRpmLimit(15);
        slot.setTpmLimit(250000);
        slot.setRpdLimit(500);

        ProviderCapacityLimits limits = policy.effectiveLimits(slot);

        // Effective limits: floor(limit * (1 - 0.20))
        assertEquals(12, limits.rpmLimit());
        assertEquals(200000, limits.tpmLimit());
        assertEquals(400, limits.rpdLimit());
    }

    @Test
    public void testEffectiveLimit_ZeroOrNegativeHandling() {
        ProviderCapacityLimitPolicy policy = new ProviderCapacityLimitPolicy(0.20);

        ProviderSlot slot = new ProviderSlot();
        slot.setRpmLimit(1);
        slot.setTpmLimit(100);
        slot.setRpdLimit(0);

        ProviderCapacityLimits limits = policy.effectiveLimits(slot);

        // Minimum 1 if physical > 0
        assertEquals(1, limits.rpmLimit());
        assertEquals(80, limits.tpmLimit());
        assertEquals(0, limits.rpdLimit());
    }
}

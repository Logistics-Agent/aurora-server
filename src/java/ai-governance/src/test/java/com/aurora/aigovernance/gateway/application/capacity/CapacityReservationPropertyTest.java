package com.aurora.aigovernance.gateway.application.capacity;

import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.ProviderCapacityLimits;
import net.jqwik.api.*;

public class CapacityReservationPropertyTest {

    @Property
    public boolean effectiveLimitsNeverExceedPhysicalLimits(
            @ForAll("validRpm") int rpm,
            @ForAll("validTpm") int tpm,
            @ForAll("validRpd") int rpd,
            @ForAll("validHeadroom") double headroom) {

        ProviderCapacityLimitPolicy policy = new ProviderCapacityLimitPolicy(headroom);

        ProviderSlot slot = new ProviderSlot();
        slot.setRpmLimit(rpm);
        slot.setTpmLimit(tpm);
        slot.setRpdLimit(rpd);

        ProviderCapacityLimits effective = policy.effectiveLimits(slot);

        return effective.rpmLimit() <= rpm &&
               effective.tpmLimit() <= tpm &&
               effective.rpdLimit() <= rpd &&
               effective.rpmLimit() >= 0 &&
               effective.tpmLimit() >= 0 &&
               effective.rpdLimit() >= 0;
    }

    @Provide
    Arbitrary<Integer> validRpm() {
        return Arbitraries.integers().between(1, 1000);
    }

    @Provide
    Arbitrary<Integer> validTpm() {
        return Arbitraries.integers().between(1000, 10000000);
    }

    @Provide
    Arbitrary<Integer> validRpd() {
        return Arbitraries.integers().between(10, 50000);
    }

    @Provide
    Arbitrary<Double> validHeadroom() {
        return Arbitraries.doubles().between(0.0, 0.5);
    }
}

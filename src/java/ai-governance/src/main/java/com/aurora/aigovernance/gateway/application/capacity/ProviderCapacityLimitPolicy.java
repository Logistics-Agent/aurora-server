package com.aurora.aigovernance.gateway.application.capacity;

import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.ProviderCapacityLimits;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

/**
 * Policy calculating effective capacity limits after deducting headroom.
 * <p>
 * {@code effectiveLimit = floor(physicalLimit * (1 - headroomRatio))}
 */
@Component
public class ProviderCapacityLimitPolicy {

    private final double headroomRatio;

    public ProviderCapacityLimitPolicy(
            @Value("${ai-governance.provider.capacity-headroom-ratio:0.20}") double headroomRatio) {
        this.headroomRatio = Math.max(0.0, Math.min(0.9, headroomRatio));
    }

    public ProviderCapacityLimits effectiveLimits(ProviderSlot slot) {
        double factor = 1.0 - headroomRatio;
        long effectiveRpm = (long) Math.floor(slot.getRpmLimit() * factor);
        long effectiveTpm = (long) Math.floor(slot.getTpmLimit() * factor);
        long effectiveRpd = (long) Math.floor(slot.getRpdLimit() * factor);

        // Ensure minimum 1 if physical limit > 0
        effectiveRpm = Math.max(slot.getRpmLimit() > 0 ? 1 : 0, effectiveRpm);
        effectiveTpm = Math.max(slot.getTpmLimit() > 0 ? 1 : 0, effectiveTpm);
        effectiveRpd = Math.max(slot.getRpdLimit() > 0 ? 1 : 0, effectiveRpd);

        return new ProviderCapacityLimits(effectiveRpm, effectiveTpm, effectiveRpd);
    }

    public double getHeadroomRatio() {
        return headroomRatio;
    }
}

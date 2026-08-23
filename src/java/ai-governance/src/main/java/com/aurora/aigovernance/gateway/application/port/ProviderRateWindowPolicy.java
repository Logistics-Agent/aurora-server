package com.aurora.aigovernance.gateway.application.port;

import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.gateway.domain.valueobject.RateWindow;

import java.time.Instant;

/**
 * Strategy interface for computing provider-specific rate-limit bucket keys and TTLs.
 */
public interface ProviderRateWindowPolicy {

    RateWindow rpmWindow(ProviderSlot slot, Instant now);

    RateWindow tpmWindow(ProviderSlot slot, Instant now);

    RateWindow rpdWindow(ProviderSlot slot, Instant now);
}

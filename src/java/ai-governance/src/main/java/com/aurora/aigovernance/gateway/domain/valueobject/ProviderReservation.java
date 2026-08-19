package com.aurora.aigovernance.gateway.domain.valueobject;

import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;

public record ProviderReservation(
        String reservationId,
        ProviderSlot slot,
        long reservedTokens,
        String rpmKey,
        String tpmKey,
        String rpdKey
) {}

package com.aurora.aigovernance.gateway.domain.valueobject;

public record SlotCapacity(
        long currentRpm,
        long currentTpm,
        long currentRpd
) {}

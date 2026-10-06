package com.aurora.aigovernance.governance.domain.enums;

/**
 * Automation levels from fully manual to fully autonomous.
 */
public enum AutomationLevel {
    MANUAL,
    ASSISTED,
    SEMI_AUTONOMOUS,
    SUPERVISED_AUTONOMOUS,
    /**
     * Canonical value persisted by the plan capability seed data.
     */
    FULL_AUTONOMOUS,
    /**
     * Legacy spelling retained so existing rows remain readable.
     */
    @Deprecated
    FULLY_AUTONOMOUS
}

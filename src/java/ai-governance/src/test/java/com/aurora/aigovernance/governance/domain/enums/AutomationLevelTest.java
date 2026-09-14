package com.aurora.aigovernance.governance.domain.enums;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;

class AutomationLevelTest {

    @Test
    void supportsAutomationLevelPersistedByPlanCapabilities() {
        assertEquals(
                AutomationLevel.FULL_AUTONOMOUS,
                AutomationLevel.valueOf("FULL_AUTONOMOUS")
        );
    }
}

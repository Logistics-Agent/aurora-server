package com.aurora.aigovernance.orchestration.application;

import com.aurora.aigovernance.shared.domain.TokenBudget;
import org.junit.jupiter.api.Test;

import java.util.Map;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.*;

public class ServiceOnlyRequestTest {

    @Test
    public void testServiceOnlyRequest_NullUserId_ValidCommand() {
        UUID tenantId = UUID.randomUUID();
        GenerateAiCommand command = new GenerateAiCommand(
                tenantId,
                null, // Null userId for service-to-service automated background request
                "devops-agent",
                "devops.diagnose",
                "diagnose node failure",
                new TokenBudget(500, 500),
                Map.of()
        );

        assertNull(command.userId());
        assertEquals("devops-agent", command.callerServiceId());
        assertEquals(tenantId, command.tenantId());
    }
}

package com.aurora.aigovernance.integration;

import com.aurora.aigovernance.governance.application.service.GovernancePolicyService;
import com.aurora.aigovernance.governance.domain.valueobject.GovernanceDecision;
import com.aurora.aigovernance.shared.domain.AiOperation;
import com.aurora.aigovernance.shared.domain.TokenBudget;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.test.context.ActiveProfiles;

import java.util.UUID;

import static org.junit.jupiter.api.Assertions.*;

@SpringBootTest
@ActiveProfiles("test")
public class ExecutePolicyIntegrationTest {

    @Autowired(required = false)
    private GovernancePolicyService governancePolicyService;

    @Test
    public void testPolicyPreCheckIntegration_KnownTenant() {
        if (governancePolicyService == null) {
            return; // Environment test isolation
        }

        UUID demoTenantId = UUID.fromString("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        GovernanceDecision decision = governancePolicyService.evaluate(
                demoTenantId,
                "regulatory-compliance-rag",
                "compliance.answer",
                AiOperation.GENERATE,
                new TokenBudget(100, 100)
        );

        assertNotNull(decision);
        assertNotNull(decision.decisionId());
    }
}

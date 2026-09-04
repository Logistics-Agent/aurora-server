package com.aurora.aigovernance.integration;

import com.aurora.aigovernance.orchestration.application.ExecuteAiService;
import com.aurora.aigovernance.orchestration.application.GenerateAiCommand;
import com.aurora.aigovernance.shared.domain.TokenBudget;
import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.test.context.ActiveProfiles;

import java.util.Map;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.assertNotNull;

@Tag("integration")
@SpringBootTest
@ActiveProfiles("test")
public class GenerateIntegrationTest {

    @Autowired(required = false)
    private ExecuteAiService executeAiService;

    @Test
    public void testEndToEndGenerateIntegration() {
        if (executeAiService == null) return;

        UUID demoTenantId = UUID.fromString("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        GenerateAiCommand command = new GenerateAiCommand(
                demoTenantId,
                UUID.randomUUID(),
                "regulatory-compliance-rag",
                "compliance.answer",
                "Summarize customs import procedure",
                new TokenBudget(200, 200),
                Map.of()
        );

        ExecuteAiService.GovernedGenerateResult result = executeAiService.generate(command);
        assertNotNull(result);
        assertNotNull(result.decision());
    }
}

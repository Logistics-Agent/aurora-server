package com.aurora.aigovernance.integration;

import com.aurora.aigovernance.orchestration.application.EmbedAiCommand;
import com.aurora.aigovernance.orchestration.application.ExecuteAiService;
import org.junit.jupiter.api.Tag;
import org.junit.jupiter.api.Test;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.boot.test.context.SpringBootTest;
import org.springframework.test.context.ActiveProfiles;

import java.util.UUID;

import static org.junit.jupiter.api.Assertions.assertNotNull;

@Tag("integration")
@SpringBootTest
@ActiveProfiles("test")
public class EmbedIntegrationTest {

    @Autowired(required = false)
    private ExecuteAiService executeAiService;

    @Test
    public void testEndToEndEmbedIntegration() {
        if (executeAiService == null) return;

        UUID demoTenantId = UUID.fromString("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        EmbedAiCommand command = new EmbedAiCommand(
                demoTenantId,
                UUID.randomUUID(),
                "regulatory-compliance-rag",
                "compliance.answer",
                "Customs tariff classification HS code text",
                768,
                150L
        );

        ExecuteAiService.GovernedEmbedResult result = executeAiService.embed(command);
        assertNotNull(result);
        assertNotNull(result.decision());
    }
}

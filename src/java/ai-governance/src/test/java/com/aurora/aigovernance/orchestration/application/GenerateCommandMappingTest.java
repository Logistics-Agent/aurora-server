package com.aurora.aigovernance.orchestration.application;

import com.aurora.aigovernance.shared.domain.TokenBudget;
import org.junit.jupiter.api.Test;

import java.util.Map;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.*;

public class GenerateCommandMappingTest {

    @Test
    public void testGenerateAiCommand_HoldsExplicitBudgetAndServiceId() {
        UUID tenantId = UUID.randomUUID();
        UUID userId = UUID.randomUUID();
        TokenBudget budget = new TokenBudget(3000, 4000);

        GenerateAiCommand command = new GenerateAiCommand(
                tenantId,
                userId,
                "regulatory-compliance-rag",
                "compliance.answer",
                "Summarize customs law",
                budget,
                Map.of("temp", "0.7")
        );

        assertEquals(tenantId, command.tenantId());
        assertEquals(userId, command.userId());
        assertEquals("regulatory-compliance-rag", command.callerServiceId());
        assertEquals("compliance.answer", command.capabilityCode());
        assertEquals("Summarize customs law", command.prompt());
        assertEquals(7000L, command.tokenBudget().reservationTokens());
        assertEquals("0.7", command.parameters().get("temp"));
    }

    @Test
    public void testEmbedAiCommand_NoPromptAndNoOutputTokens() {
        UUID tenantId = UUID.randomUUID();

        EmbedAiCommand command = new EmbedAiCommand(
                tenantId,
                null,
                "regulatory-compliance-rag",
                "compliance.answer",
                "Text content for embedding vector generation",
                768,
                500L
        );

        assertEquals(tenantId, command.tenantId());
        assertNull(command.userId());
        assertEquals(768, command.dimensions());
        assertEquals(500L, command.estimatedInputTokens());
        assertEquals("Text content for embedding vector generation", command.content());
    }
}

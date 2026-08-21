package com.aurora.aigovernance.gateway.application.routing;

import com.aurora.aigovernance.gateway.domain.entity.ProviderSlot;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.shared.domain.AiOperation;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotEquals;

public class ProviderOperationCompatibilityTest {

    @Test
    public void testSlotDeclaresOperation_GenerateVsEmbed() {
        ProviderSlot generateSlot = new ProviderSlot();
        generateSlot.setSlotAlias("gemini-shared-generate-01");
        generateSlot.setProvider(AiProvider.GEMINI);
        generateSlot.setOperation(AiOperation.GENERATE);
        generateSlot.setModelName("gemini-1.5-flash");

        ProviderSlot embedSlot = new ProviderSlot();
        embedSlot.setSlotAlias("gemini-shared-embed-01");
        embedSlot.setProvider(AiProvider.GEMINI);
        embedSlot.setOperation(AiOperation.EMBED);
        embedSlot.setModelName("text-embedding-004");

        assertEquals(AiOperation.GENERATE, generateSlot.getOperation());
        assertEquals(AiOperation.EMBED, embedSlot.getOperation());
        assertNotEquals(generateSlot.getOperation(), embedSlot.getOperation());
    }
}

package com.aurora.aigovernance.shared.domain;

import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;

public class TokenBudgetReservationTest {

    @Test
    public void testGenerationTokenBudget_SumsInputAndMaxOutput() {
        TokenBudget budget = new TokenBudget(3000, 4000);
        assertEquals(3000, budget.estimatedInputTokens());
        assertEquals(4000, budget.maxOutputTokens());
        // TPM reservation = 3000 + 4000 = 7000
        assertEquals(7000, budget.reservationTokens());
    }

    @Test
    public void testEmbeddingTokenBudget_InputTokensOnly() {
        TokenBudget budget = TokenBudget.forEmbedding(500);
        assertEquals(500, budget.estimatedInputTokens());
        assertEquals(0, budget.maxOutputTokens());
        // TPM reservation = 500
        assertEquals(500, budget.reservationTokens());
    }
}

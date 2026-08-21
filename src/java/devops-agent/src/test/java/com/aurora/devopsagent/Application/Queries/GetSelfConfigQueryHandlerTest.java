package com.aurora.devopsagent.Application.Queries;

import com.aurora.devopsagent.Domain.Entity.DevOpsAgentSelfConfig;
import com.aurora.devopsagent.Infrastructure.Persistence.SelfConfigJpaRepository;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.math.BigDecimal;
import java.util.Collections;
import java.util.List;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;

class GetSelfConfigQueryHandlerTest {

    private SelfConfigJpaRepository selfConfigRepository;
    private GetSelfConfigQueryHandler handler;

    @BeforeEach
    void setUp() {
        selfConfigRepository = mock(SelfConfigJpaRepository.class);
        handler = new GetSelfConfigQueryHandler(selfConfigRepository);
    }

    @Test
    @DisplayName("Empty DB returns default self config without hard-coded LLM provider defaults")
    void testEmptyDbReturnsDefaultsWithoutHardcodedProviders() {
        when(selfConfigRepository.findAll()).thenReturn(Collections.emptyList());

        DevOpsAgentSelfConfig config = handler.handle();

        assertNotNull(config);
        // Provider/model/endpoint must NOT be hard-coded (managed by AiGovernance)
        assertNull(config.getModelProvider());
        assertNull(config.getModelName());
        assertNull(config.getApiEndpoint());

        // Domain-owned defaults
        assertEquals(4096, config.getMaxTokensPerRequest());
        assertEquals(new BigDecimal("50.0000"), config.getAlertThresholdUsdPerDay());
    }

    @Test
    @DisplayName("Existing config in DB is returned unchanged")
    void testExistingConfigReturnedUnchanged() {
        DevOpsAgentSelfConfig existing = new DevOpsAgentSelfConfig();
        existing.setMaxTokensPerRequest(8192);
        existing.setAlertThresholdUsdPerDay(new BigDecimal("100.0000"));

        when(selfConfigRepository.findAll()).thenReturn(List.of(existing));

        DevOpsAgentSelfConfig result = handler.handle();

        assertSame(existing, result);
        assertEquals(8192, result.getMaxTokensPerRequest());
        assertEquals(new BigDecimal("100.0000"), result.getAlertThresholdUsdPerDay());
    }
}

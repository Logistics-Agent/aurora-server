package com.aurora.aigovernance.orchestration.application;

import com.aurora.aigovernance.gateway.application.execution.AiExecutionService;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateRequest;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateResult;
import com.aurora.aigovernance.governance.application.port.PolicyAuditPort;
import com.aurora.aigovernance.governance.application.service.GovernancePolicyService;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.governance.domain.enums.AutomationLevel;
import com.aurora.aigovernance.governance.domain.enums.ModelTier;
import com.aurora.aigovernance.governance.domain.valueobject.GovernanceDecision;
import com.aurora.aigovernance.shared.domain.AiOperation;
import com.aurora.aigovernance.shared.domain.TokenBudget;
import com.aurora.shared.security.CurrentServiceContext;
import com.aurora.shared.security.CurrentUserContext;
import io.micrometer.core.instrument.simple.SimpleMeterRegistry;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.amqp.rabbit.core.RabbitTemplate;

import java.util.Map;
import java.util.Set;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertTrue;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.when;

@ExtendWith(MockitoExtension.class)
public class ThreadLocalBoundaryTest {

    @Mock
    private GovernancePolicyService governancePolicyService;

    @Mock
    private AiExecutionService aiExecutionService;

    @Mock
    private PolicyAuditPort policyAuditPort;

    @Mock
    private RabbitTemplate rabbitTemplate;

    private ExecuteAiService executeAiService;

    @BeforeEach
    public void setup() {
        executeAiService = new ExecuteAiService(
                governancePolicyService,
                aiExecutionService,
                policyAuditPort,
                rabbitTemplate,
                new SimpleMeterRegistry()
        );
        // Clear all ThreadLocal contexts to simulate execution detached from transport thread
        CurrentUserContext.clear();
        CurrentServiceContext.clear();
    }

    @AfterEach
    public void tearDown() {
        CurrentUserContext.clear();
        CurrentServiceContext.clear();
    }

    @Test
    public void testExecuteAiService_RunsSafelyWithoutThreadLocal() {
        UUID tenantId = UUID.randomUUID();
        GenerateAiCommand command = new GenerateAiCommand(
                tenantId,
                null,
                "regulatory-compliance-rag",
                "compliance.answer",
                "prompt",
                new TokenBudget(100, 100),
                Map.of()
        );

        GovernanceDecision allowedDecision = new GovernanceDecision(
                true, null, "dec-1", Set.of(AiProvider.GEMINI),
                Set.of("shared-ai"), ModelTier.STANDARD, 4096,
                AutomationLevel.ASSISTED, false, "v1"
        );

        when(governancePolicyService.evaluate(eq(tenantId), eq("regulatory-compliance-rag"), eq("compliance.answer"), eq(AiOperation.GENERATE), any()))
                .thenReturn(allowedDecision);

        when(aiExecutionService.generate(any(), any(), eq("regulatory-compliance-rag"), any()))
                .thenReturn(new AiGenerateResult("content", 100, 50, "gemini-1.5-flash", "GEMINI"));

        // When: executed with empty ThreadLocal contexts
        ExecuteAiService.GovernedGenerateResult result = executeAiService.generate(command);

        // Then: succeeds because application layer relies exclusively on command parameters
        assertTrue(result.decision().allowed());
        assertNotNull(result.result());
    }
}

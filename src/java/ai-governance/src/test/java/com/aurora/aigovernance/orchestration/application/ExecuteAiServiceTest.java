package com.aurora.aigovernance.orchestration.application;

import com.aurora.aigovernance.gateway.application.execution.AiExecutionService;
import com.aurora.aigovernance.gateway.domain.valueobject.AiEmbeddingRequest;
import com.aurora.aigovernance.gateway.domain.valueobject.AiEmbeddingResult;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateRequest;
import com.aurora.aigovernance.gateway.domain.valueobject.AiGenerateResult;
import com.aurora.aigovernance.governance.application.port.PolicyAuditPort;
import com.aurora.aigovernance.governance.application.service.GovernancePolicyService;
import com.aurora.aigovernance.governance.domain.enums.AiProvider;
import com.aurora.aigovernance.governance.domain.enums.AutomationLevel;
import com.aurora.aigovernance.governance.domain.enums.DenyReason;
import com.aurora.aigovernance.governance.domain.enums.ModelTier;
import com.aurora.aigovernance.governance.domain.valueobject.GovernanceDecision;
import com.aurora.aigovernance.shared.domain.AiOperation;
import com.aurora.aigovernance.shared.domain.TokenBudget;
import io.micrometer.core.instrument.MeterRegistry;
import io.micrometer.core.instrument.simple.SimpleMeterRegistry;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;
import org.springframework.amqp.rabbit.core.RabbitTemplate;

import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
public class ExecuteAiServiceTest {

    @Mock
    private GovernancePolicyService governancePolicyService;

    @Mock
    private AiExecutionService aiExecutionService;

    @Mock
    private PolicyAuditPort policyAuditPort;

    @Mock
    private RabbitTemplate rabbitTemplate;

    private MeterRegistry meterRegistry;
    private ExecuteAiService executeAiService;

    private final UUID tenantId = UUID.randomUUID();
    private final UUID userId = UUID.randomUUID();

    @BeforeEach
    public void setup() {
        meterRegistry = new SimpleMeterRegistry();
        executeAiService = new ExecuteAiService(
                governancePolicyService,
                aiExecutionService,
                policyAuditPort,
                rabbitTemplate,
                meterRegistry
        );
    }

    @Test
    public void testGenerate_DeniedByPolicy_NeverCallsGateway() {
        GovernanceDecision deniedDecision = GovernanceDecision.denied(DenyReason.QUOTA_EXCEEDED, "dec-1");
        when(governancePolicyService.evaluate(any(), any(), any(), eq(AiOperation.GENERATE), any()))
                .thenReturn(deniedDecision);

        GenerateAiCommand command = new GenerateAiCommand(
                tenantId, userId, "service-a", "compliance.answer",
                "test prompt", new TokenBudget(100, 100), Map.of()
        );

        ExecuteAiService.GovernedGenerateResult result = executeAiService.generate(command);

        assertFalse(result.decision().allowed());
        assertNull(result.result());
        verify(aiExecutionService, never()).generate(any(), any(), any(), any());
        verify(policyAuditPort).publishPolicyDecision(any());
    }

    @Test
    public void testGenerate_AllowedByPolicy_ExecutesGateway() {
        GovernanceDecision allowedDecision = new GovernanceDecision(
                true, null, "dec-1", Set.of(AiProvider.GEMINI),
                Set.of("shared-ai"), ModelTier.STANDARD, 4096,
                AutomationLevel.ASSISTED, false, "v1"
        );
        when(governancePolicyService.evaluate(any(), any(), any(), eq(AiOperation.GENERATE), any()))
                .thenReturn(allowedDecision);

        AiGenerateResult genResult = new AiGenerateResult("response text", 100, 50, "gemini-1.5-flash", "GEMINI");
        when(aiExecutionService.generate(eq(allowedDecision), any(AiGenerateRequest.class), eq("service-a"), any(TokenBudget.class)))
                .thenReturn(genResult);

        GenerateAiCommand command = new GenerateAiCommand(
                tenantId, userId, "service-a", "compliance.answer",
                "test prompt", new TokenBudget(100, 100), Map.of()
        );

        ExecuteAiService.GovernedGenerateResult result = executeAiService.generate(command);

        assertTrue(result.decision().allowed());
        assertNotNull(result.result());
        assertEquals("response text", result.result().content());
        assertEquals("GEMINI", result.result().provider());
    }

    @Test
    public void testEmbed_AllowedByPolicy_ExecutesGatewayEmbedding() {
        GovernanceDecision allowedDecision = new GovernanceDecision(
                true, null, "dec-2", Set.of(AiProvider.GEMINI),
                Set.of("shared-ai"), ModelTier.STANDARD, 4096,
                AutomationLevel.ASSISTED, false, "v1"
        );
        when(governancePolicyService.evaluate(any(), any(), any(), eq(AiOperation.EMBED), any()))
                .thenReturn(allowedDecision);

        AiEmbeddingResult embedResult = new AiEmbeddingResult(List.of(0.1f, 0.2f), 50, "text-embedding-004", "GEMINI");
        when(aiExecutionService.embed(eq(allowedDecision), any(AiEmbeddingRequest.class), eq("service-a"), any(TokenBudget.class)))
                .thenReturn(embedResult);

        EmbedAiCommand command = new EmbedAiCommand(
                tenantId, userId, "service-a", "compliance.answer",
                "content to embed", 768, 50L
        );

        ExecuteAiService.GovernedEmbedResult result = executeAiService.embed(command);

        assertTrue(result.decision().allowed());
        assertNotNull(result.result());
        assertEquals(2, result.result().vector().size());
        assertEquals(50L, result.result().inputTokens());
    }
}

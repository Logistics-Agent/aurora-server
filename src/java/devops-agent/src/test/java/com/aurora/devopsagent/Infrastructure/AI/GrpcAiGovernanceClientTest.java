package com.aurora.devopsagent.Infrastructure.AI;

import com.aurora.aigovernance.grpc.generated.AiExecutionServiceGrpc;
import com.aurora.aigovernance.grpc.generated.AiGenerateRequest;
import com.aurora.aigovernance.grpc.generated.AiGenerateResponse;
import io.github.resilience4j.circuitbreaker.CircuitBreakerRegistry;
import io.github.resilience4j.retry.RetryRegistry;
import io.grpc.Status;
import io.grpc.StatusRuntimeException;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;

import java.util.Map;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.*;

class GrpcAiGovernanceClientTest {

    private AiExecutionServiceGrpc.AiExecutionServiceBlockingStub mockStub;
    private GrpcAiGovernanceClient client;

    @BeforeEach
    void setUp() {
        mockStub = mock(AiExecutionServiceGrpc.AiExecutionServiceBlockingStub.class);
        when(mockStub.withInterceptors(any())).thenReturn(mockStub);
        when(mockStub.withDeadlineAfter(anyLong(), any())).thenReturn(mockStub);

        CircuitBreakerRegistry cbRegistry = CircuitBreakerRegistry.ofDefaults();
        RetryRegistry retryRegistry = RetryRegistry.ofDefaults();

        client = new GrpcAiGovernanceClient(mockStub, cbRegistry, retryRegistry);
    }

    @Test
    @DisplayName("Generates LLM response successfully via AiExecutionService gRPC stub")
    void testGenerateSuccess() {
        AiGenerateResponse mockResponse = AiGenerateResponse.newBuilder()
                .setContent("RCA diagnosis: memory leak")
                .setInputTokens(100)
                .setOutputTokens(50)
                .setDecisionId("dec-001")
                .setAutomationLevel("AUTOMATED")
                .setRequiresApproval(false)
                .setModel("gpt-4o")
                .setProvider("azure_openai")
                .build();

        when(mockStub.generate(any(AiGenerateRequest.class))).thenReturn(mockResponse);

        AiGovernanceClient.GenerateCommand command = new AiGovernanceClient.GenerateCommand(
                "devops.rca",
                "Analyze memory leak",
                4000,
                500,
                Map.of("service", "PaymentService")
        );

        AiGovernanceClient.GenerateResult result = client.generate(command);

        assertNotNull(result);
        assertEquals("RCA diagnosis: memory leak", result.content());
        assertEquals(100, result.inputTokens());
        assertEquals(50, result.outputTokens());
        assertEquals("dec-001", result.decisionId());
        assertEquals("gpt-4o", result.model());
        assertEquals("azure_openai", result.provider());
        assertFalse(result.requiresApproval());
    }

    @Test
    @DisplayName("Throws SecurityException when AiGovernance returns PERMISSION_DENIED")
    void testPermissionDenied() {
        when(mockStub.generate(any(AiGenerateRequest.class))).thenThrow(
                new StatusRuntimeException(Status.PERMISSION_DENIED.withDescription("Monthly token quota exceeded"))
        );

        AiGovernanceClient.GenerateCommand command = new AiGovernanceClient.GenerateCommand(
                "devops.rca",
                "Prompt",
                1000,
                100,
                Map.of()
        );

        SecurityException ex = assertThrows(SecurityException.class, () -> client.generate(command));
        assertTrue(ex.getMessage().contains("Monthly token quota exceeded"));
    }
}

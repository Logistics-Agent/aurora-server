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

class GrpcAiGovernanceRetryPolicyTest {

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
    @DisplayName("UNAVAILABLE error triggers bounded retry and succeeds on subsequent attempt")
    void testUnavailableRetriesAndSucceeds() {
        AiGenerateResponse mockResponse = AiGenerateResponse.newBuilder()
                .setContent("Success after retry")
                .setInputTokens(100)
                .setOutputTokens(50)
                .setDecisionId("dec-retry-01")
                .setAutomationLevel("AUTOMATED")
                .build();

        // First attempt throws UNAVAILABLE, second attempt succeeds
        when(mockStub.generate(any(AiGenerateRequest.class)))
                .thenThrow(new StatusRuntimeException(Status.UNAVAILABLE.withDescription("Connection reset")))
                .thenReturn(mockResponse);

        AiGovernanceClient.GenerateCommand command = new AiGovernanceClient.GenerateCommand(
                "devops.rca",
                "Prompt",
                2000,
                500,
                Map.of()
        );

        AiGovernanceClient.GenerateResult result = client.generate(command);

        assertNotNull(result);
        assertEquals("Success after retry", result.content());
        verify(mockStub, times(2)).generate(any(AiGenerateRequest.class));
    }

    @Test
    @DisplayName("PERMISSION_DENIED policy error is never retried")
    void testPermissionDeniedNeverRetried() {
        when(mockStub.generate(any(AiGenerateRequest.class)))
                .thenThrow(new StatusRuntimeException(Status.PERMISSION_DENIED.withDescription("Tenant budget exhausted")));

        AiGovernanceClient.GenerateCommand command = new AiGovernanceClient.GenerateCommand(
                "devops.rca",
                "Prompt",
                2000,
                500,
                Map.of()
        );

        assertThrows(SecurityException.class, () -> client.generate(command));
        verify(mockStub, times(1)).generate(any(AiGenerateRequest.class));
    }

    @Test
    @DisplayName("INVALID_ARGUMENT error is never retried")
    void testInvalidArgumentNeverRetried() {
        when(mockStub.generate(any(AiGenerateRequest.class)))
                .thenThrow(new StatusRuntimeException(Status.INVALID_ARGUMENT.withDescription("Invalid capability code")));

        AiGovernanceClient.GenerateCommand command = new AiGovernanceClient.GenerateCommand(
                "unknown.capability",
                "Prompt",
                2000,
                500,
                Map.of()
        );

        assertThrows(IllegalArgumentException.class, () -> client.generate(command));
        verify(mockStub, times(1)).generate(any(AiGenerateRequest.class));
    }
}

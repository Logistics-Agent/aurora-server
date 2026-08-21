package com.aurora.aigovernance.grpc.gateway;

import com.aurora.aigovernance.grpc.generated.AiGenerateRequest;
import com.aurora.aigovernance.grpc.generated.AiGenerateResponse;
import com.aurora.aigovernance.orchestration.application.ExecuteAiService;
import com.aurora.shared.security.CurrentServiceContext;
import io.grpc.Status;
import io.grpc.StatusRuntimeException;
import io.grpc.stub.StreamObserver;
import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.junit.jupiter.api.extension.ExtendWith;
import org.mockito.ArgumentCaptor;
import org.mockito.Mock;
import org.mockito.junit.jupiter.MockitoExtension;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.mockito.Mockito.*;

@ExtendWith(MockitoExtension.class)
public class MissingServiceIdentityTest {

    @Mock
    private ExecuteAiService executeAiService;

    @Mock
    private StreamObserver<AiGenerateResponse> responseObserver;

    private AiExecutionGrpcHandler grpcHandler;

    @BeforeEach
    public void setup() {
        grpcHandler = new AiExecutionGrpcHandler(executeAiService);
        CurrentServiceContext.clear();
    }

    @AfterEach
    public void tearDown() {
        CurrentServiceContext.clear();
    }

    @Test
    public void testMissingServiceId_ReturnsUnauthenticated() {
        // Given: no serviceId populated in CurrentServiceContext
        AiGenerateRequest request = AiGenerateRequest.newBuilder()
                .setCapabilityCode("compliance.answer")
                .setPrompt("Test prompt")
                .build();

        // When
        grpcHandler.generate(request, responseObserver);

        // Then: responseObserver.onError(UNAUTHENTICATED)
        ArgumentCaptor<Throwable> captor = ArgumentCaptor.forClass(Throwable.class);
        verify(responseObserver).onError(captor.capture());

        StatusRuntimeException ex = (StatusRuntimeException) captor.getValue();
        assertEquals(Status.Code.UNAUTHENTICATED, ex.getStatus().getCode());
        verifyNoInteractions(executeAiService);
    }
}

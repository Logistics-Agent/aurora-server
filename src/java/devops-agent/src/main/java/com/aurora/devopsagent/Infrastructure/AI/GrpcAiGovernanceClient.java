package com.aurora.devopsagent.Infrastructure.AI;

import com.aurora.aigovernance.grpc.generated.AiExecutionServiceGrpc;
import com.aurora.aigovernance.grpc.generated.AiGenerateRequest;
import com.aurora.aigovernance.grpc.generated.AiGenerateResponse;
import com.aurora.shared.constants.GrpcMetadataKeys;
import com.aurora.shared.security.CurrentUserContext;
import io.github.resilience4j.circuitbreaker.CircuitBreaker;
import io.github.resilience4j.circuitbreaker.CircuitBreakerConfig;
import io.github.resilience4j.circuitbreaker.CircuitBreakerRegistry;
import io.github.resilience4j.retry.Retry;
import io.github.resilience4j.retry.RetryConfig;
import io.github.resilience4j.retry.RetryRegistry;
import io.grpc.CallOptions;
import io.grpc.Channel;
import io.grpc.ClientCall;
import io.grpc.ClientInterceptor;
import io.grpc.ForwardingClientCall;
import io.grpc.Metadata;
import io.grpc.MethodDescriptor;
import io.grpc.Status;
import io.grpc.StatusRuntimeException;
import net.devh.boot.grpc.client.inject.GrpcClient;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

import java.time.Duration;
import java.util.concurrent.TimeUnit;

/**
 * GrpcAiGovernanceClient: Centralized gRPC client for Governed LLM Generation.
 * Implements strict error classification:
 * - Retries ONLY transient transport failures (UNAVAILABLE).
 * - NEVER retries policy/quota/argument errors (PERMISSION_DENIED, INVALID_ARGUMENT, etc.).
 * - CircuitBreaker ignores policy denials and only tracks server/transport faults.
 */
@Service
public class GrpcAiGovernanceClient implements AiGovernanceClient {

    private static final Logger log = LoggerFactory.getLogger(GrpcAiGovernanceClient.class);
    private static final String SERVICE_ID = "devops-agent";
    private static final long DEFAULT_TIMEOUT_SECONDS = 30;

    @GrpcClient("ai-governance-service")
    private AiExecutionServiceGrpc.AiExecutionServiceBlockingStub aiExecutionStub;

    private final CircuitBreaker circuitBreaker;
    private final Retry retry;

    public GrpcAiGovernanceClient(CircuitBreakerRegistry circuitBreakerRegistry, RetryRegistry retryRegistry) {
        this.circuitBreaker = configureCircuitBreaker(circuitBreakerRegistry);
        this.retry = configureRetry(retryRegistry);
    }

    public GrpcAiGovernanceClient(
            AiExecutionServiceGrpc.AiExecutionServiceBlockingStub aiExecutionStub,
            CircuitBreakerRegistry circuitBreakerRegistry,
            RetryRegistry retryRegistry) {
        this.aiExecutionStub = aiExecutionStub;
        this.circuitBreaker = configureCircuitBreaker(circuitBreakerRegistry);
        this.retry = configureRetry(retryRegistry);
    }

    private CircuitBreaker configureCircuitBreaker(CircuitBreakerRegistry registry) {
        CircuitBreakerConfig config = CircuitBreakerConfig.custom()
                .slidingWindowSize(20)
                .failureRateThreshold(50.0f)
                .waitDurationInOpenState(Duration.ofSeconds(15))
                .ignoreExceptions(SecurityException.class, IllegalArgumentException.class)
                .recordException(e -> isTransportFault(e))
                .build();
        return registry.circuitBreaker("aiGovernanceClient", config);
    }

    private Retry configureRetry(RetryRegistry registry) {
        RetryConfig config = RetryConfig.custom()
                .maxAttempts(2)
                .waitDuration(Duration.ofMillis(300))
                .retryOnException(this::isTransientTransportFault)
                .build();
        return registry.retry("aiGovernanceClient", config);
    }

    private boolean isTransientTransportFault(Throwable throwable) {
        if (throwable instanceof StatusRuntimeException sre) {
            Status.Code code = sre.getStatus().getCode();
            // ONLY retry UNAVAILABLE
            return code == Status.Code.UNAVAILABLE;
        }
        return false;
    }

    private boolean isTransportFault(Throwable throwable) {
        if (throwable instanceof StatusRuntimeException sre) {
            Status.Code code = sre.getStatus().getCode();
            return code == Status.Code.UNAVAILABLE || code == Status.Code.INTERNAL || code == Status.Code.UNKNOWN;
        }
        return true;
    }

    @Override
    public GenerateResult generate(GenerateCommand command) {
        if (aiExecutionStub == null) {
            throw new IllegalStateException("AiExecutionService gRPC stub is not initialized.");
        }

        AiGenerateRequest.Builder requestBuilder = AiGenerateRequest.newBuilder()
                .setCapabilityCode(command.capabilityCode())
                .setPrompt(command.prompt())
                .setMaxOutputTokens(command.maxOutputTokens())
                .setEstimatedInputTokens(command.estimatedInputTokens());

        if (command.parameters() != null) {
            requestBuilder.putAllParameters(command.parameters());
        }

        AiGenerateRequest request = requestBuilder.build();

        AiExecutionServiceGrpc.AiExecutionServiceBlockingStub stubWithMetadata = aiExecutionStub
                .withInterceptors(new MetadataClientInterceptor())
                .withDeadlineAfter(DEFAULT_TIMEOUT_SECONDS, TimeUnit.SECONDS);

        try {
            return Retry.decorateSupplier(retry, () ->
                    CircuitBreaker.decorateSupplier(circuitBreaker, () -> {
                        log.debug("Calling AiGovernance.Generate capability={}", command.capabilityCode());
                        AiGenerateResponse response = stubWithMetadata.generate(request);
                        return new GenerateResult(
                                response.getContent(),
                                response.getInputTokens(),
                                response.getOutputTokens(),
                                response.getDecisionId(),
                                response.getAutomationLevel(),
                                response.getRequiresApproval(),
                                response.getModel(),
                                response.getProvider()
                        );
                    }).get()
            ).get();

        } catch (StatusRuntimeException e) {
            Status.Code code = e.getStatus().getCode();
            log.error("AiGovernance gRPC call failed: status={}, description={}", code, e.getStatus().getDescription());

            if (code == Status.Code.PERMISSION_DENIED) {
                throw new SecurityException("AI Governance policy denial: " + e.getStatus().getDescription(), e);
            }
            if (code == Status.Code.INVALID_ARGUMENT) {
                throw new IllegalArgumentException("Invalid argument sent to AiGovernance: " + e.getStatus().getDescription(), e);
            }
            if (code == Status.Code.RESOURCE_EXHAUSTED) {
                throw new SecurityException("AI Governance quota exhausted: " + e.getStatus().getDescription(), e);
            }
            throw new RuntimeException("AI Governance execution failed (" + code + "): " + e.getMessage(), e);
        } catch (Exception e) {
            log.error("AiGovernance call unexpected error: {}", e.getMessage(), e);
            throw new RuntimeException("AI Governance execution failed: " + e.getMessage(), e);
        }
    }

    private static class MetadataClientInterceptor implements ClientInterceptor {
        @Override
        public <ReqT, RespT> ClientCall<ReqT, RespT> interceptCall(
                MethodDescriptor<ReqT, RespT> method,
                CallOptions callOptions,
                Channel next) {

            return new ForwardingClientCall.SimpleForwardingClientCall<>(next.newCall(method, callOptions)) {
                @Override
                public void start(Listener<RespT> responseListener, Metadata headers) {
                    headers.put(GrpcMetadataKeys.SERVICE_ID, SERVICE_ID);

                    CurrentUserContext userContext = CurrentUserContext.getCurrent();
                    if (userContext != null) {
                        if (userContext.getTenantId() != null) {
                            headers.put(GrpcMetadataKeys.TENANT_ID, userContext.getTenantId().toString());
                        }
                        if (userContext.getUserId() != null) {
                            headers.put(GrpcMetadataKeys.USER_ID, userContext.getUserId().toString());
                        }
                        if (userContext.getTraceId() != null) {
                            headers.put(GrpcMetadataKeys.TRACE_ID, userContext.getTraceId());
                        }
                        if (userContext.getPermissionVersion() != null) {
                            headers.put(GrpcMetadataKeys.PERMISSION_VERSION, userContext.getPermissionVersion().toString());
                        }
                        if (userContext.getRoleIds() != null && !userContext.getRoleIds().isEmpty()) {
                            headers.put(GrpcMetadataKeys.ROLE_IDS, String.join(",", userContext.getRoleIds()));
                        }
                    }

                    super.start(responseListener, headers);
                }
            };
        }
    }
}

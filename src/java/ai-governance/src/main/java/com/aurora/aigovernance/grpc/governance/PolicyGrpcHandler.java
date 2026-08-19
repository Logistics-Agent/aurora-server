package com.aurora.aigovernance.grpc.governance;

import com.aurora.aigovernance.governance.application.service.GovernancePolicyService;
import com.aurora.aigovernance.governance.domain.valueobject.GovernanceDecision;
import com.aurora.aigovernance.grpc.generated.AiGovernanceServiceGrpc;
import com.aurora.aigovernance.grpc.generated.ExecutePolicyRequest;
import com.aurora.aigovernance.grpc.generated.ExecutePolicyResponse;
import com.aurora.aigovernance.shared.domain.AiOperation;
import com.aurora.aigovernance.shared.domain.TokenBudget;
import com.aurora.shared.security.CurrentServiceContext;
import com.aurora.shared.security.CurrentUserContext;
import io.grpc.Status;
import io.grpc.stub.StreamObserver;
import net.devh.boot.grpc.server.service.GrpcService;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.UUID;
import java.util.stream.Collectors;

/**
 * gRPC Handler for Policy Pre-Check / Preview.
 * <p>
 * Thin adapter: maps proto to GovernancePolicyService and returns response.
 * Result cannot be used as an authorization token for execution.
 */
@GrpcService
public class PolicyGrpcHandler extends AiGovernanceServiceGrpc.AiGovernanceServiceImplBase {

    private static final Logger log = LoggerFactory.getLogger(PolicyGrpcHandler.class);

    private final GovernancePolicyService governancePolicyService;

    public PolicyGrpcHandler(GovernancePolicyService governancePolicyService) {
        this.governancePolicyService = governancePolicyService;
    }

    @Override
    public void executePolicy(ExecutePolicyRequest request, StreamObserver<ExecutePolicyResponse> responseObserver) {
        // Extract identity from authenticated context
        CurrentUserContext userContext = CurrentUserContext.getCurrent();
        CurrentServiceContext serviceContext = CurrentServiceContext.getCurrent();

        String serviceId = serviceContext != null ? serviceContext.getServiceId() : null;
        if (serviceId == null || serviceId.isBlank()) {
            responseObserver.onError(Status.UNAUTHENTICATED
                    .withDescription("Missing required x-service-id metadata header")
                    .asRuntimeException());
            return;
        }

        UUID tenantId = userContext != null ? userContext.getTenantId() : null;

        TokenBudget tokenBudget = new TokenBudget(
                request.getEstimatedInputTokens(),
                request.getMaxOutputTokens()
        );

        GovernanceDecision decision = governancePolicyService.evaluate(
                tenantId,
                serviceId,
                request.getCapabilityCode(),
                AiOperation.GENERATE,
                tokenBudget
        );

        ExecutePolicyResponse.Builder responseBuilder = ExecutePolicyResponse.newBuilder()
                .setAllowed(decision.allowed())
                .setDecisionId(decision.decisionId())
                .setRequiresApproval(decision.requireApproval());

        if (decision.denyReason() != null) {
            responseBuilder.setDenyReason(decision.denyReason().name());
        }

        if (decision.allowed()) {
            if (decision.allowedProviders() != null) {
                responseBuilder.addAllAllowedProviders(
                        decision.allowedProviders().stream().map(Enum::name).collect(Collectors.toList())
                );
            }
            if (decision.modelTier() != null) {
                responseBuilder.setModelTier(decision.modelTier().name());
            }
            responseBuilder.setMaxTokens(decision.maxTokens());
            if (decision.automationLevel() != null) {
                responseBuilder.setAutomationLevel(decision.automationLevel().name());
            }
            if (decision.policyVersion() != null) {
                responseBuilder.setPolicyVersion(decision.policyVersion());
            }
        }

        responseObserver.onNext(responseBuilder.build());
        responseObserver.onCompleted();
    }
}

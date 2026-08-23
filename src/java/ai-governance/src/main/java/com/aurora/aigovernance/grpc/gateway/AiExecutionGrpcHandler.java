package com.aurora.aigovernance.grpc.gateway;

import com.aurora.aigovernance.grpc.generated.*;
import com.aurora.aigovernance.orchestration.application.EmbedAiCommand;
import com.aurora.aigovernance.orchestration.application.ExecuteAiService;
import com.aurora.aigovernance.orchestration.application.GenerateAiCommand;
import com.aurora.aigovernance.shared.domain.TokenBudget;
import com.aurora.shared.security.CurrentServiceContext;
import com.aurora.shared.security.CurrentUserContext;
import io.grpc.Status;
import io.grpc.stub.StreamObserver;
import net.devh.boot.grpc.server.service.GrpcService;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.Map;
import java.util.UUID;

/**
 * gRPC Handler for governed AI Execution (Generate & Embed).
 * <p>
 * Thin boundary layer:
 * <ul>
 *   <li>Extracts identity from transport ThreadLocal</li>
 *   <li>Enforces missing x-service-id -> UNAUTHENTICATED</li>
 *   <li>Builds operation-specific application command</li>
 *   <li>Delegates to {@link ExecuteAiService}</li>
 *   <li>Maps result to protobuf response</li>
 * </ul>
 */
@GrpcService
public class AiExecutionGrpcHandler extends AiExecutionServiceGrpc.AiExecutionServiceImplBase {

    private static final Logger log = LoggerFactory.getLogger(AiExecutionGrpcHandler.class);

    private final ExecuteAiService executeAiService;

    public AiExecutionGrpcHandler(ExecuteAiService executeAiService) {
        this.executeAiService = executeAiService;
    }

    @Override
    public void generate(AiGenerateRequest request, StreamObserver<AiGenerateResponse> responseObserver) {
        // 1. Extract context from transport boundary
        CurrentUserContext userContext = CurrentUserContext.getCurrent();
        CurrentServiceContext serviceContext = CurrentServiceContext.getCurrent();

        String serviceId = serviceContext != null ? serviceContext.getServiceId() : null;
        if (serviceId == null || serviceId.isBlank()) {
            responseObserver.onError(Status.UNAUTHENTICATED
                    .withDescription("Missing required x-service-id metadata header")
                    .asRuntimeException());
            return;
        }

        UUID userId = userContext != null ? userContext.getUserId() : null;
        UUID tenantId = userContext != null ? userContext.getTenantId() : null;

        // 2. Build GenerateAiCommand with explicit TokenBudget
        TokenBudget tokenBudget = new TokenBudget(
                request.getEstimatedInputTokens(),
                request.getMaxOutputTokens()
        );

        Map<String, String> parameters = new java.util.HashMap<>(request.getParametersMap());
        java.util.List<com.aurora.aigovernance.gateway.domain.valueobject.MultimodalPart> inputParts = new java.util.ArrayList<>();

        if (request.getInputPartsCount() > 0) {
            for (com.aurora.aigovernance.grpc.generated.AiInputPart part : request.getInputPartsList()) {
                if (part.hasText()) {
                    inputParts.add(com.aurora.aigovernance.gateway.domain.valueobject.MultimodalPart.text(part.getText()));
                } else if (part.hasFile()) {
                    var fileRef = part.getFile();
                    String storageRef = fileRef.getStorageReference();
                    // Validate storage reference security (no traversal, no SSRF)
                    if (storageRef == null || storageRef.isBlank() || storageRef.contains("..") ||
                            storageRef.startsWith("http://") || storageRef.startsWith("https://") ||
                            storageRef.startsWith("file://") || storageRef.startsWith("ftp://")) {
                        responseObserver.onError(Status.INVALID_ARGUMENT
                                .withDescription("Invalid or unsafe storage reference: " + storageRef)
                                .asRuntimeException());
                        return;
                    }
                    inputParts.add(com.aurora.aigovernance.gateway.domain.valueobject.MultimodalPart.file(
                            storageRef,
                            fileRef.getMimeType(),
                            fileRef.getFileName(),
                            0L
                    ));
                    parameters.put("storage_reference", storageRef);
                    parameters.put("mime_type", fileRef.getMimeType());
                    parameters.put("file_name", fileRef.getFileName());
                }
            }
        }

        GenerateAiCommand command = new GenerateAiCommand(
                tenantId,
                userId,
                serviceId,
                request.getCapabilityCode(),
                request.getPrompt(),
                tokenBudget,
                parameters,
                inputParts
        );

        // 3. Delegate to orchestrator
        try {
            ExecuteAiService.GovernedGenerateResult governedResult = executeAiService.generate(command);

            if (!governedResult.decision().allowed()) {
                responseObserver.onError(Status.PERMISSION_DENIED
                        .withDescription("Governance policy denied: " + governedResult.decision().denyReason())
                        .asRuntimeException());
                return;
            }

            AiGenerateResponse.Builder responseBuilder = AiGenerateResponse.newBuilder()
                    .setContent(governedResult.result().content())
                    .setInputTokens(governedResult.result().inputTokens())
                    .setOutputTokens(governedResult.result().outputTokens())
                    .setDecisionId(governedResult.decision().decisionId())
                    .setRequiresApproval(governedResult.decision().requireApproval())
                    .setModel(governedResult.result().model())
                    .setProvider(governedResult.result().provider());

            if (governedResult.decision().automationLevel() != null) {
                responseBuilder.setAutomationLevel(governedResult.decision().automationLevel().name());
            }

            responseObserver.onNext(responseBuilder.build());
            responseObserver.onCompleted();

        } catch (Exception e) {
            log.error("Generation failed: serviceId={}, capability={}, error={}",
                    serviceId, request.getCapabilityCode(), e.getMessage(), e);
            responseObserver.onError(Status.INTERNAL
                    .withDescription("AI generation failed: " + e.getMessage())
                    .asRuntimeException());
        }
    }

    @Override
    public void embed(AiEmbedRequest request, StreamObserver<AiEmbedResponse> responseObserver) {
        CurrentUserContext userContext = CurrentUserContext.getCurrent();
        CurrentServiceContext serviceContext = CurrentServiceContext.getCurrent();

        String serviceId = serviceContext != null ? serviceContext.getServiceId() : null;
        if (serviceId == null || serviceId.isBlank()) {
            responseObserver.onError(Status.UNAUTHENTICATED
                    .withDescription("Missing required x-service-id metadata header")
                    .asRuntimeException());
            return;
        }

        UUID userId = userContext != null ? userContext.getUserId() : null;
        UUID tenantId = userContext != null ? userContext.getTenantId() : null;

        EmbedAiCommand command = new EmbedAiCommand(
                tenantId,
                userId,
                serviceId,
                request.getCapabilityCode(),
                request.getContent(),
                request.getDimensions() > 0 ? request.getDimensions() : null,
                request.getEstimatedInputTokens()
        );

        try {
            ExecuteAiService.GovernedEmbedResult governedResult = executeAiService.embed(command);

            if (!governedResult.decision().allowed()) {
                responseObserver.onError(Status.PERMISSION_DENIED
                        .withDescription("Governance policy denied: " + governedResult.decision().denyReason())
                        .asRuntimeException());
                return;
            }

            AiEmbedResponse.Builder responseBuilder = AiEmbedResponse.newBuilder()
                    .addAllVector(governedResult.result().vector())
                    .setInputTokens(governedResult.result().inputTokens())
                    .setDecisionId(governedResult.decision().decisionId())
                    .setModel(governedResult.result().model())
                    .setProvider(governedResult.result().provider());

            responseObserver.onNext(responseBuilder.build());
            responseObserver.onCompleted();

        } catch (Exception e) {
            log.error("Embedding failed: serviceId={}, capability={}, error={}",
                    serviceId, request.getCapabilityCode(), e.getMessage(), e);
            responseObserver.onError(Status.INTERNAL
                    .withDescription("AI embedding failed: " + e.getMessage())
                    .asRuntimeException());
        }
    }
}

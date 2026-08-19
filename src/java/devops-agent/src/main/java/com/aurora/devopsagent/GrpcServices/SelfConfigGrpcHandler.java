package com.aurora.devopsagent.GrpcServices;

import com.aurora.devopsagent.Application.Commands.UpdateSelfConfigCommand;
import com.aurora.devopsagent.Application.Commands.UpdateSelfConfigCommandHandler;
import com.aurora.devopsagent.Application.Queries.GetSelfConfigQueryHandler;
import com.aurora.devopsagent.Domain.Entity.DevOpsAgentSelfConfig;
import com.aurora.devopsagent.grpc.*;
import io.grpc.stub.StreamObserver;
import net.devh.boot.grpc.server.service.GrpcService;

import java.math.BigDecimal;

@GrpcService
public class SelfConfigGrpcHandler extends DevOpsAgentServiceGrpc.DevOpsAgentServiceImplBase {

    private final GetSelfConfigQueryHandler getSelfConfigQueryHandler;
    private final UpdateSelfConfigCommandHandler updateSelfConfigCommandHandler;

    public SelfConfigGrpcHandler(
            GetSelfConfigQueryHandler getSelfConfigQueryHandler,
            UpdateSelfConfigCommandHandler updateSelfConfigCommandHandler) {
        this.getSelfConfigQueryHandler = getSelfConfigQueryHandler;
        this.updateSelfConfigCommandHandler = updateSelfConfigCommandHandler;
    }

    @Override
    public void getSelfConfig(EmptyDevOpsRequest request, StreamObserver<SelfConfigResponse> responseObserver) {
        DevOpsAgentSelfConfig config = getSelfConfigQueryHandler.handle();

        SelfConfigResponse response = SelfConfigResponse.newBuilder()
                .setId(config.getId() != null ? config.getId().toString() : "")
                .setModelProvider(config.getModelProvider() != null ? config.getModelProvider() : "")
                .setModelName(config.getModelName() != null ? config.getModelName() : "")
                .setApiEndpoint(config.getApiEndpoint() != null ? config.getApiEndpoint() : "")
                .setMaxTokensPerRequest(config.getMaxTokensPerRequest())
                .setAlertThresholdUsdPerDay(config.getAlertThresholdUsdPerDay() != null ? config.getAlertThresholdUsdPerDay().doubleValue() : 0.0)
                .setUpdatedBy(config.getUpdatedBy() != null ? config.getUpdatedBy() : "")
                .setUpdatedAt(config.getUpdatedAt() != null ? config.getUpdatedAt().toString() : "")
                .build();

        responseObserver.onNext(response);
        responseObserver.onCompleted();
    }

    @Override
    public void updateSelfConfig(UpdateSelfConfigRequest request, StreamObserver<SelfConfigResponse> responseObserver) {
        UpdateSelfConfigCommand command = new UpdateSelfConfigCommand(
                request.getModelProvider(),
                request.getModelName(),
                request.getApiEndpoint(),
                request.getMaxTokensPerRequest(),
                BigDecimal.valueOf(request.getAlertThresholdUsdPerDay())
        );

        DevOpsAgentSelfConfig updated = updateSelfConfigCommandHandler.handle(command);

        SelfConfigResponse response = SelfConfigResponse.newBuilder()
                .setId(updated.getId().toString())
                .setModelProvider(updated.getModelProvider() != null ? updated.getModelProvider() : "")
                .setModelName(updated.getModelName() != null ? updated.getModelName() : "")
                .setApiEndpoint(updated.getApiEndpoint() != null ? updated.getApiEndpoint() : "")
                .setMaxTokensPerRequest(updated.getMaxTokensPerRequest())
                .setAlertThresholdUsdPerDay(updated.getAlertThresholdUsdPerDay() != null ? updated.getAlertThresholdUsdPerDay().doubleValue() : 0.0)
                .setUpdatedBy(updated.getUpdatedBy() != null ? updated.getUpdatedBy() : "")
                .setUpdatedAt(updated.getUpdatedAt() != null ? updated.getUpdatedAt().toString() : "")
                .build();

        responseObserver.onNext(response);
        responseObserver.onCompleted();
    }
}

package com.aurora.devopsagent.GrpcServices;

import com.aurora.devopsagent.Application.Commands.UpdateSelfConfigCommand;
import com.aurora.devopsagent.Application.Queries.GetSelfConfigQueryHandler;
import com.aurora.devopsagent.Domain.Entity.DevOpsAgentSelfConfig;
import com.aurora.devopsagent.grpc.*;
import io.grpc.stub.StreamObserver;
import net.devh.boot.grpc.server.service.GrpcService;

import java.math.BigDecimal;

@GrpcService
public class SelfConfigGrpcHandler extends DevOpsConfigServiceGrpc.DevOpsConfigServiceImplBase {

    private final GetSelfConfigQueryHandler getSelfConfigQueryHandler;
    private final UpdateSelfConfigCommand.Handler updateSelfConfigHandler;

    public SelfConfigGrpcHandler(
            GetSelfConfigQueryHandler getSelfConfigQueryHandler,
            UpdateSelfConfigCommand.Handler updateSelfConfigHandler) {
        this.getSelfConfigQueryHandler = getSelfConfigQueryHandler;
        this.updateSelfConfigHandler = updateSelfConfigHandler;
    }

    @Override
    public void getSelfConfig(EmptyDevOpsRequest request, StreamObserver<SelfConfigResponse> responseObserver) {
        DevOpsAgentSelfConfig config = getSelfConfigQueryHandler.handle();

        SelfConfigResponse.Builder builder = SelfConfigResponse.newBuilder()
                .setId(config.getId() != null ? config.getId().toString() : "")
                .setMaxTokensPerRequest(config.getMaxTokensPerRequest())
                .setAlertThresholdUsdPerDay(config.getAlertThresholdUsdPerDay() != null ? config.getAlertThresholdUsdPerDay().doubleValue() : 0.0)
                .setUpdatedBy(config.getUpdatedBy() != null ? config.getUpdatedBy() : "")
                .setUpdatedAt(config.getUpdatedAt() != null ? config.getUpdatedAt().toString() : "");

        if (config.getModelProvider() != null) {
            builder.setModelProvider(config.getModelProvider());
        }
        if (config.getModelName() != null) {
            builder.setModelName(config.getModelName());
        }
        if (config.getApiEndpoint() != null) {
            builder.setApiEndpoint(config.getApiEndpoint());
        }

        responseObserver.onNext(builder.build());
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

        DevOpsAgentSelfConfig updated = updateSelfConfigHandler.handle(command);

        SelfConfigResponse.Builder builder = SelfConfigResponse.newBuilder()
                .setId(updated.getId().toString())
                .setMaxTokensPerRequest(updated.getMaxTokensPerRequest())
                .setAlertThresholdUsdPerDay(updated.getAlertThresholdUsdPerDay() != null ? updated.getAlertThresholdUsdPerDay().doubleValue() : 0.0)
                .setUpdatedBy(updated.getUpdatedBy() != null ? updated.getUpdatedBy() : "")
                .setUpdatedAt(updated.getUpdatedAt() != null ? updated.getUpdatedAt().toString() : "");

        if (updated.getModelProvider() != null) {
            builder.setModelProvider(updated.getModelProvider());
        }
        if (updated.getModelName() != null) {
            builder.setModelName(updated.getModelName());
        }
        if (updated.getApiEndpoint() != null) {
            builder.setApiEndpoint(updated.getApiEndpoint());
        }

        responseObserver.onNext(builder.build());
        responseObserver.onCompleted();
    }
}

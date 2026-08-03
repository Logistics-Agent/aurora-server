package com.aurora.devopsagent.GrpcServices;

import com.aurora.devopsagent.Application.Commands.IngestAlertCommand;
import com.aurora.devopsagent.Application.Commands.IngestAlertCommandHandler;
import com.aurora.devopsagent.Application.Commands.IngestAlertResult;
import com.aurora.devopsagent.grpc.DevOpsAgentServiceGrpc;
import com.aurora.devopsagent.grpc.IngestAlertRequest;
import com.aurora.devopsagent.grpc.IngestAlertResponse;
import io.grpc.stub.StreamObserver;
import net.devh.boot.grpc.server.service.GrpcService;

/**
 * gRPC Service Handler cho Event Ingestion (nhận alert từ BFF qua proto).
 */
@GrpcService
public class IngestionGrpcHandler extends DevOpsAgentServiceGrpc.DevOpsAgentServiceImplBase {

    private final IngestAlertCommandHandler ingestAlertCommandHandler;

    public IngestionGrpcHandler(IngestAlertCommandHandler ingestAlertCommandHandler) {
        this.ingestAlertCommandHandler = ingestAlertCommandHandler;
    }

    @Override
    public void ingestAlert(IngestAlertRequest request, StreamObserver<IngestAlertResponse> responseObserver) {
        IngestAlertCommand command = new IngestAlertCommand(
                request.getSource(),
                request.getErrorSignature(),
                request.getPayloadJson(),
                request.getAffectedService(),
                request.getEnvironment()
        );

        IngestAlertResult result = ingestAlertCommandHandler.handle(command);

        IngestAlertResponse response = IngestAlertResponse.newBuilder()
                .setDuplicated(result.duplicated())
                .setCorrelationId(result.correlationId() != null ? result.correlationId() : "")
                .setIncidentId(result.incidentId() != null ? result.incidentId() : "")
                .setStatus(result.status() != null ? result.status() : "")
                .build();

        responseObserver.onNext(response);
        responseObserver.onCompleted();
    }
}

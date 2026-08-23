package com.aurora.devopsagent.GrpcServices;

import com.aurora.devopsagent.Application.Commands.IngestAlertCommand;
import com.aurora.devopsagent.grpc.DevOpsIngestionServiceGrpc;
import com.aurora.devopsagent.grpc.IngestAlertRequest;
import com.aurora.devopsagent.grpc.IngestAlertResponse;
import io.grpc.stub.StreamObserver;
import net.devh.boot.grpc.server.service.GrpcService;

/**
 * gRPC Service Handler cho Event Ingestion (nhận alert qua proto).
 */
@GrpcService
public class IngestionGrpcHandler extends DevOpsIngestionServiceGrpc.DevOpsIngestionServiceImplBase {

    private final IngestAlertCommand.Handler ingestAlertHandler;

    public IngestionGrpcHandler(IngestAlertCommand.Handler ingestAlertHandler) {
        this.ingestAlertHandler = ingestAlertHandler;
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

        IngestAlertCommand.Result result = ingestAlertHandler.handle(command);

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

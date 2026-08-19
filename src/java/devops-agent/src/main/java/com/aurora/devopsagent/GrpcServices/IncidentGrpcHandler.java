package com.aurora.devopsagent.GrpcServices;

import com.aurora.devopsagent.Application.Queries.GetIncidentQueryHandler;
import com.aurora.devopsagent.Application.Queries.ListIncidentsQueryHandler;
import com.aurora.devopsagent.Domain.Entity.Incident;
import com.aurora.devopsagent.grpc.*;
import io.grpc.stub.StreamObserver;
import net.devh.boot.grpc.server.service.GrpcService;
import org.springframework.data.domain.Page;

@GrpcService
public class IncidentGrpcHandler extends DevOpsAgentServiceGrpc.DevOpsAgentServiceImplBase {

    private final GetIncidentQueryHandler getIncidentQueryHandler;
    private final ListIncidentsQueryHandler listIncidentsQueryHandler;

    public IncidentGrpcHandler(
            GetIncidentQueryHandler getIncidentQueryHandler,
            ListIncidentsQueryHandler listIncidentsQueryHandler) {
        this.getIncidentQueryHandler = getIncidentQueryHandler;
        this.listIncidentsQueryHandler = listIncidentsQueryHandler;
    }

    @Override
    public void getIncident(GetIncidentRequest request, StreamObserver<IncidentResponse> responseObserver) {
        Incident incident = getIncidentQueryHandler.getByCorrelationId(request.getCorrelationId());

        IncidentResponse response = IncidentResponse.newBuilder()
                .setId(incident.getId().toString())
                .setCorrelationId(incident.getCorrelationId())
                .setSource(incident.getSource() != null ? incident.getSource() : "")
                .setErrorSignature(incident.getErrorSignature() != null ? incident.getErrorSignature() : "")
                .setSeverity(incident.getSeverity() != null ? incident.getSeverity().name() : "")
                .setOriginalSeverity(incident.getOriginalSeverity() != null ? incident.getOriginalSeverity().name() : "")
                .setStatus(incident.getStatus() != null ? incident.getStatus().name() : "")
                .setImpactScore(incident.getImpactScore() != null ? incident.getImpactScore().doubleValue() : 0.0)
                .setAffectedService(incident.getAffectedService() != null ? incident.getAffectedService() : "")
                .setRcaRootCause(incident.getRcaRootCause() != null ? incident.getRcaRootCause() : "")
                .setRcaRecommendation(incident.getRcaRecommendation() != null ? incident.getRcaRecommendation() : "")
                .setCreatedAt(incident.getCreatedAt() != null ? incident.getCreatedAt().toString() : "")
                .build();

        responseObserver.onNext(response);
        responseObserver.onCompleted();
    }

    @Override
    public void listIncidents(ListIncidentsRequest request, StreamObserver<ListIncidentsResponse> responseObserver) {
        Page<Incident> page = listIncidentsQueryHandler.handle(request.getSeverity(), request.getPageNumber(), request.getPageSize());

        ListIncidentsResponse.Builder builder = ListIncidentsResponse.newBuilder()
                .setTotalElements((int) page.getTotalElements())
                .setTotalPages(page.getTotalPages())
                .setCurrentPage(page.getNumber());

        for (Incident inc : page.getContent()) {
            builder.addIncidents(IncidentSummary.newBuilder()
                    .setId(inc.getId().toString())
                    .setCorrelationId(inc.getCorrelationId())
                    .setSource(inc.getSource() != null ? inc.getSource() : "")
                    .setErrorSignature(inc.getErrorSignature() != null ? inc.getErrorSignature() : "")
                    .setSeverity(inc.getSeverity() != null ? inc.getSeverity().name() : "")
                    .setStatus(inc.getStatus() != null ? inc.getStatus().name() : "")
                    .setImpactScore(inc.getImpactScore() != null ? inc.getImpactScore().doubleValue() : 0.0)
                    .setCreatedAt(inc.getCreatedAt() != null ? inc.getCreatedAt().toString() : "")
                    .build());
        }

        responseObserver.onNext(builder.build());
        responseObserver.onCompleted();
    }
}

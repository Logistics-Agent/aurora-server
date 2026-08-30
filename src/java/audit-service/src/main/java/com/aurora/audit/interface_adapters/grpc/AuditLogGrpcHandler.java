package com.aurora.audit.interface_adapters.grpc;

import com.aurora.audit.application.usecase.IngestAuditEventUseCase;
import com.aurora.audit.grpc.*;
import com.aurora.audit.infrastructure.messaging.dto.AuditEventDto;
import com.aurora.audit.infrastructure.persistence.SpringDataAuditLogRepository;
import com.aurora.audit.infrastructure.persistence.entity.AuditLogEntity;
import com.google.protobuf.Timestamp;
import io.grpc.stub.StreamObserver;
import lombok.RequiredArgsConstructor;
import net.devh.boot.grpc.server.service.GrpcService;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.PageRequest;

import java.time.Instant;
import java.util.List;
import java.util.stream.Collectors;

@GrpcService
@RequiredArgsConstructor
public class AuditLogGrpcHandler extends AuditLogServiceGrpc.AuditLogServiceImplBase {

    private final IngestAuditEventUseCase ingestAuditEventUseCase;
    private final SpringDataAuditLogRepository auditLogRepository;

    @Override
    public void getSystemAuditLogs(GetSystemAuditLogsRequest request, StreamObserver<AuditLogsResponse> responseObserver) {
        int page = request.getPage() > 0 ? request.getPage() : 0;
        int limit = request.getLimit() > 0 ? request.getLimit() : 20;

        List<AuditLogEntity> entities;
        if (request.getServiceName() != null && !request.getServiceName().isBlank()) {
            entities = auditLogRepository.findByServiceNameOrderByCreatedAtDesc(request.getServiceName());
        } else {
            Page<AuditLogEntity> pageResult = auditLogRepository.findAllByOrderByCreatedAtDesc(PageRequest.of(page, limit));
            entities = pageResult.getContent();
        }

        List<AuditLogMessage> messages = entities.stream()
                .map(this::mapToGrpcMessage)
                .collect(Collectors.toList());

        AuditLogsResponse response = AuditLogsResponse.newBuilder()
                .addAllLogs(messages)
                .setPage(page)
                .setLimit(limit)
                .setTotalItems(entities.size())
                .setTotalPages(1)
                .build();

        responseObserver.onNext(response);
        responseObserver.onCompleted();
    }

    @Override
    public void getAdminAuditLogs(GetAdminAuditLogsRequest request, StreamObserver<AuditLogsResponse> responseObserver) {
        int page = request.getPage() > 0 ? request.getPage() : 0;
        int limit = request.getLimit() > 0 ? request.getLimit() : 20;

        List<AuditLogEntity> entities;
        if (request.getTenantId() != null && !request.getTenantId().isBlank()) {
            entities = auditLogRepository.findByTenantIdOrderByCreatedAtDesc(request.getTenantId());
        } else {
            Page<AuditLogEntity> pageResult = auditLogRepository.findAllByOrderByCreatedAtDesc(PageRequest.of(page, limit));
            entities = pageResult.getContent();
        }

        List<AuditLogMessage> messages = entities.stream()
                .map(this::mapToGrpcMessage)
                .collect(Collectors.toList());

        AuditLogsResponse response = AuditLogsResponse.newBuilder()
                .addAllLogs(messages)
                .setPage(page)
                .setLimit(limit)
                .setTotalItems(entities.size())
                .setTotalPages(1)
                .build();

        responseObserver.onNext(response);
        responseObserver.onCompleted();
    }

    @Override
    public void ingestAuditEvent(IngestAuditEventRequest request, StreamObserver<AuditLogMessage> responseObserver) {
        AuditEventDto dto = new AuditEventDto(
                request.getEventId(),
                request.getServiceName(),
                request.getEventType(),
                request.getTenantId(),
                request.getUserId(),
                request.getUserRole(),
                request.getResourceId(),
                request.getPayloadJson(),
                request.getIpAddress(),
                Instant.now().toString()
        );

        AuditLogEntity entity = ingestAuditEventUseCase.execute(dto);
        AuditLogMessage response = mapToGrpcMessage(entity);

        responseObserver.onNext(response);
        responseObserver.onCompleted();
    }

    private AuditLogMessage mapToGrpcMessage(AuditLogEntity entity) {
        Instant instant = entity.getCreatedAt() != null ? entity.getCreatedAt() : Instant.now();
        Timestamp timestamp = Timestamp.newBuilder()
                .setSeconds(instant.getEpochSecond())
                .setNanos(instant.getNano())
                .build();

        return AuditLogMessage.newBuilder()
                .setId(entity.getId() != null ? entity.getId() : "")
                .setServiceName(entity.getServiceName() != null ? entity.getServiceName() : "")
                .setEventType(entity.getEventType() != null ? entity.getEventType() : "")
                .setTenantId(entity.getTenantId() != null ? entity.getTenantId() : "")
                .setUserId(entity.getUserId() != null ? entity.getUserId() : "")
                .setUserRole(entity.getUserRole() != null ? entity.getUserRole() : "")
                .setResourceId(entity.getResourceId() != null ? entity.getResourceId() : "")
                .setPayloadJson(entity.getPayloadJson() != null ? entity.getPayloadJson() : "{}")
                .setIpAddress(entity.getIpAddress() != null ? entity.getIpAddress() : "")
                .setCreatedAt(timestamp)
                .build();
    }
}

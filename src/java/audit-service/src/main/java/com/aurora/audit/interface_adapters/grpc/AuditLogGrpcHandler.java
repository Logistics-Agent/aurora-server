package com.aurora.audit.interface_adapters.grpc;

import com.aurora.audit.application.usecase.IngestAuditEventUseCase;
import com.aurora.audit.config.AuditGrpcSecurityInterceptor;
import com.aurora.audit.grpc.*;
import com.aurora.audit.infrastructure.messaging.dto.AuditEventDto;
import com.aurora.audit.infrastructure.persistence.SpringDataAuditLogRepository;
import com.aurora.audit.infrastructure.persistence.entity.AuditLogEntity;
import com.google.protobuf.Timestamp;
import io.grpc.Status;
import io.grpc.stub.StreamObserver;
import lombok.RequiredArgsConstructor;
import net.devh.boot.grpc.server.service.GrpcService;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.PageRequest;
import org.springframework.data.domain.Sort;
import org.springframework.data.jpa.domain.Specification;

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
        int page = Math.max(request.getPage(), 0);
        int limit = request.getLimit() > 0 ? request.getLimit() : 20;

        Specification<AuditLogEntity> spec = Specification.where(null);

        if (request.getServiceName() != null && !request.getServiceName().isBlank()) {
            spec = spec.and((root, query, cb) -> cb.equal(root.get("serviceName"), request.getServiceName().trim()));
        }
        if (request.getEventType() != null && !request.getEventType().isBlank()) {
            spec = spec.and((root, query, cb) -> cb.equal(root.get("eventType"), request.getEventType().trim()));
        }
        if (request.getTenantId() != null && !request.getTenantId().isBlank()) {
            spec = spec.and((root, query, cb) -> cb.equal(root.get("tenantId"), request.getTenantId().trim()));
        }
        if (request.getUserId() != null && !request.getUserId().isBlank()) {
            spec = spec.and((root, query, cb) -> cb.equal(root.get("userId"), request.getUserId().trim()));
        }

        Page<AuditLogEntity> pageResult = auditLogRepository.findAll(
                spec,
                PageRequest.of(page, limit, Sort.by(Sort.Direction.DESC, "createdAt"))
        );

        List<AuditLogMessage> messages = pageResult.getContent().stream()
                .map(this::mapToGrpcMessage)
                .collect(Collectors.toList());

        AuditLogsResponse response = AuditLogsResponse.newBuilder()
                .addAllLogs(messages)
                .setPage(page)
                .setLimit(limit)
                .setTotalItems((int) pageResult.getTotalElements())
                .setTotalPages(pageResult.getTotalPages())
                .build();

        responseObserver.onNext(response);
        responseObserver.onCompleted();
    }

    @Override
    public void getAdminAuditLogs(GetAdminAuditLogsRequest request, StreamObserver<AuditLogsResponse> responseObserver) {
        String tenantId = request.getTenantId();

        // 1. Enforce strict tenant isolation: tenantId is strictly mandatory
        if (tenantId == null || tenantId.isBlank()) {
            responseObserver.onError(Status.INVALID_ARGUMENT
                    .withDescription("tenantId is required for admin audit logs")
                    .asRuntimeException());
            return;
        }

        // 2. Cross-check against authenticated context if present from gRPC metadata
        String contextTenantId = AuditGrpcSecurityInterceptor.TENANT_ID_CONTEXT_KEY.get();
        String contextRole = AuditGrpcSecurityInterceptor.ROLE_CONTEXT_KEY.get();

        if (contextTenantId != null && !contextTenantId.isBlank()
                && !"SYSTEM_ADMIN".equalsIgnoreCase(contextRole)
                && !tenantId.trim().equalsIgnoreCase(contextTenantId.trim())) {
            responseObserver.onError(Status.PERMISSION_DENIED
                    .withDescription("Access denied: Tenant ID mismatch with authenticated identity")
                    .asRuntimeException());
            return;
        }

        int page = Math.max(request.getPage(), 0);
        int limit = request.getLimit() > 0 ? request.getLimit() : 20;

        Specification<AuditLogEntity> spec = (root, query, cb) -> cb.equal(root.get("tenantId"), tenantId.trim());

        if (request.getUserId() != null && !request.getUserId().isBlank()) {
            spec = spec.and((root, query, cb) -> cb.equal(root.get("userId"), request.getUserId().trim()));
        }

        Page<AuditLogEntity> pageResult = auditLogRepository.findAll(
                spec,
                PageRequest.of(page, limit, Sort.by(Sort.Direction.DESC, "createdAt"))
        );

        List<AuditLogMessage> messages = pageResult.getContent().stream()
                .map(this::mapToGrpcMessage)
                .collect(Collectors.toList());

        AuditLogsResponse response = AuditLogsResponse.newBuilder()
                .addAllLogs(messages)
                .setPage(page)
                .setLimit(limit)
                .setTotalItems((int) pageResult.getTotalElements())
                .setTotalPages(pageResult.getTotalPages())
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

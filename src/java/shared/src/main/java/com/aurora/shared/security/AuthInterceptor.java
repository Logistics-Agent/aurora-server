package com.aurora.shared.security;

import com.aurora.shared.constants.GrpcMetadataKeys;
import io.grpc.*;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.Arrays;
import java.util.Collections;
import java.util.List;
import java.util.UUID;

/**
 * gRPC Server-side Interceptor: Đọc metadata từ BFF/client (x-user-id, x-tenant-id, x-role-ids, etc.),
 * populate CurrentUserContext cho tất cả gRPC handlers phía server.
 * <p>
 * Đồng thời đọc {@code x-service-id} để populate {@link CurrentServiceContext} — immediate caller
 * workload identity, tách biệt hoàn toàn khỏi user identity.
 * <p>
 * Parallel với AuthInterceptor.cs trong .NET.
 */
public class AuthInterceptor implements ServerInterceptor {

    private static final Logger log = LoggerFactory.getLogger(AuthInterceptor.class);

    @Override
    public <ReqT, RespT> ServerCall.Listener<ReqT> interceptCall(
            ServerCall<ReqT, RespT> call,
            Metadata headers,
            ServerCallHandler<ReqT, RespT> next) {

        // --- User identity context ---
        String userIdStr = headers.get(GrpcMetadataKeys.USER_ID);
        String tenantIdStr = headers.get(GrpcMetadataKeys.TENANT_ID);
        String traceId = headers.get(GrpcMetadataKeys.TRACE_ID);
        String permissionVersionStr = headers.get(GrpcMetadataKeys.PERMISSION_VERSION);
        String roleIdsStr = headers.get(GrpcMetadataKeys.ROLE_IDS);

        UUID userId = parseUuid(userIdStr);
        UUID tenantId = parseUuid(tenantIdStr);
        Integer permissionVersion = parseInteger(permissionVersionStr);
        List<String> roleIds = parseList(roleIdsStr);

        CurrentUserContext userContext = new CurrentUserContext();
        userContext.populate(userId, tenantId, traceId, permissionVersion, roleIds, Collections.emptyList());
        CurrentUserContext.setCurrent(userContext);

        // --- Service/workload identity context ---
        String serviceId = headers.get(GrpcMetadataKeys.SERVICE_ID);

        CurrentServiceContext serviceContext = new CurrentServiceContext();
        serviceContext.populate(serviceId);
        CurrentServiceContext.setCurrent(serviceContext);

        log.debug("AuthInterceptor: UserId={}, TenantId={}, TraceId={}, RoleIds={}, ServiceId={}",
                userId, tenantId, traceId, roleIds, serviceId);

        return new ForwardingServerCallListener.SimpleForwardingServerCallListener<ReqT>(
                next.startCall(call, headers)) {
            @Override
            public void onComplete() {
                try {
                    super.onComplete();
                } finally {
                    CurrentUserContext.clear();
                    CurrentServiceContext.clear();
                }
            }

            @Override
            public void onCancel() {
                try {
                    super.onCancel();
                } finally {
                    CurrentUserContext.clear();
                    CurrentServiceContext.clear();
                }
            }
        };
    }

    private UUID parseUuid(String value) {
        if (value == null || value.trim().isEmpty()) return null;
        try {
            return UUID.fromString(value.trim());
        } catch (IllegalArgumentException e) {
            return null;
        }
    }

    private Integer parseInteger(String value) {
        if (value == null || value.trim().isEmpty()) return null;
        try {
            return Integer.parseInt(value.trim());
        } catch (NumberFormatException e) {
            return null;
        }
    }

    private List<String> parseList(String value) {
        if (value == null || value.trim().isEmpty()) return Collections.emptyList();
        return Arrays.stream(value.split(","))
                .map(String::trim)
                .filter(s -> !s.isEmpty())
                .toList();
    }
}

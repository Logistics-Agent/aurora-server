package com.aurora.shared.security;

import com.aurora.shared.constants.GrpcMetadataKeys;
import io.grpc.*;

/**
 * gRPC Client-side Interceptor: Forward x-user-id, x-tenant-id, x-trace-id, x-role-ids,
 * x-service-id khi gọi gRPC microservices downstream (ví dụ DevOps-Agent -> RAG Service).
 * Parallel với ClientMetadataInterceptor.cs trong .NET.
 */
public class ClientMetadataInterceptor implements ClientInterceptor {

    @Override
    public <ReqT, RespT> ClientCall<ReqT, RespT> interceptCall(
            MethodDescriptor<ReqT, RespT> method,
            CallOptions callOptions,
            Channel next) {

        return new ForwardingClientCall.SimpleForwardingClientCall<ReqT, RespT>(
                next.newCall(method, callOptions)) {
            @Override
            public void start(Listener<RespT> responseListener, Metadata headers) {
                // Forward user identity context
                CurrentUserContext current = CurrentUserContext.getCurrent();
                if (current != null) {
                    if (current.getUserId() != null) {
                        headers.put(GrpcMetadataKeys.USER_ID, current.getUserId().toString());
                    }
                    if (current.getTenantId() != null) {
                        headers.put(GrpcMetadataKeys.TENANT_ID, current.getTenantId().toString());
                    }
                    if (current.getTraceId() != null) {
                        headers.put(GrpcMetadataKeys.TRACE_ID, current.getTraceId());
                    }
                    if (current.getPermissionVersion() != null) {
                        headers.put(GrpcMetadataKeys.PERMISSION_VERSION, current.getPermissionVersion().toString());
                    }
                    if (current.getRole() != null && !current.getRole().isEmpty()) {
                        headers.put(GrpcMetadataKeys.ROLE, current.getRole());
                    }
                }

                // Forward service/workload identity context
                CurrentServiceContext serviceContext = CurrentServiceContext.getCurrent();
                if (serviceContext != null && serviceContext.getServiceId() != null) {
                    headers.put(GrpcMetadataKeys.SERVICE_ID, serviceContext.getServiceId());
                }

                super.start(responseListener, headers);
            }
        };
    }
}

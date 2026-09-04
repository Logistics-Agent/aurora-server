package com.aurora.audit.config;

import io.grpc.*;
import net.devh.boot.grpc.server.interceptor.GrpcGlobalServerInterceptor;
import org.springframework.context.annotation.Configuration;

@Configuration
@GrpcGlobalServerInterceptor
public class AuditGrpcSecurityInterceptor implements ServerInterceptor {

    public static final Context.Key<String> TENANT_ID_CONTEXT_KEY = Context.key("x-tenant-id");
    public static final Context.Key<String> USER_ID_CONTEXT_KEY = Context.key("x-user-id");
    public static final Context.Key<String> ROLE_CONTEXT_KEY = Context.key("x-role");

    private static final Metadata.Key<String> TENANT_ID_HEADER =
            Metadata.Key.of("x-tenant-id", Metadata.ASCII_STRING_MARSHALLER);
    private static final Metadata.Key<String> USER_ID_HEADER =
            Metadata.Key.of("x-user-id", Metadata.ASCII_STRING_MARSHALLER);
    private static final Metadata.Key<String> ROLE_HEADER =
            Metadata.Key.of("x-role", Metadata.ASCII_STRING_MARSHALLER);

    @Override
    public <ReqT, RespT> ServerCall.Listener<ReqT> interceptCall(
            ServerCall<ReqT, RespT> call,
            Metadata headers,
            ServerCallHandler<ReqT, RespT> next) {

        String tenantId = headers.get(TENANT_ID_HEADER);
        String userId = headers.get(USER_ID_HEADER);
        String role = headers.get(ROLE_HEADER);

        Context context = Context.current();
        if (tenantId != null && !tenantId.isBlank()) {
            context = context.withValue(TENANT_ID_CONTEXT_KEY, tenantId.trim());
        }
        if (userId != null && !userId.isBlank()) {
            context = context.withValue(USER_ID_CONTEXT_KEY, userId.trim());
        }
        if (role != null && !role.isBlank()) {
            context = context.withValue(ROLE_CONTEXT_KEY, role.trim());
        }

        return Contexts.interceptCall(context, call, headers, next);
    }
}

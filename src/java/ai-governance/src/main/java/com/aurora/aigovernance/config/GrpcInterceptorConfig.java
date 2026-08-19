package com.aurora.aigovernance.config;

import com.aurora.shared.exception.ExceptionInterceptor;
import com.aurora.shared.security.AuthInterceptor;
import net.devh.boot.grpc.server.interceptor.GrpcGlobalServerInterceptor;
import org.springframework.context.annotation.Configuration;

/**
 * Registers shared gRPC server interceptors from aurora-java-shared.
 * <p>
 * AuthInterceptor reads x-user-id, x-tenant-id, x-service-id, etc. from gRPC metadata
 * and populates CurrentUserContext + CurrentServiceContext ThreadLocal.
 * <p>
 * ExceptionInterceptor maps domain exceptions to gRPC Status codes.
 * <p>
 * No custom interceptors in AiGovernance — all interceptor logic lives in shared library.
 */
@Configuration
public class GrpcInterceptorConfig {

    @GrpcGlobalServerInterceptor
    public AuthInterceptor authInterceptor() {
        return new AuthInterceptor();
    }

    @GrpcGlobalServerInterceptor
    public ExceptionInterceptor exceptionInterceptor() {
        return new ExceptionInterceptor();
    }
}

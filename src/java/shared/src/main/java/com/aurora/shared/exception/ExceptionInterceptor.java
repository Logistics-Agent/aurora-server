package com.aurora.shared.exception;

import io.grpc.*;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

/**
 * gRPC Server Interceptor: Maps Java domain exceptions sang gRPC status codes.
 * Parallel với ExceptionInterceptor.cs trong .NET.
 */
public class ExceptionInterceptor implements ServerInterceptor {

    private static final Logger log = LoggerFactory.getLogger(ExceptionInterceptor.class);

    @Override
    public <ReqT, RespT> ServerCall.Listener<ReqT> interceptCall(
            ServerCall<ReqT, RespT> call,
            Metadata headers,
            ServerCallHandler<ReqT, RespT> next) {

        ServerCall.Listener<ReqT> listener = next.startCall(call, headers);

        return new ForwardingServerCallListener.SimpleForwardingServerCallListener<ReqT>(listener) {
            @Override
            public void onHalfClose() {
                try {
                    super.onHalfClose();
                } catch (Throwable t) {
                    handleException(call, t);
                }
            }
        };
    }

    private <ReqT, RespT> void handleException(ServerCall<ReqT, RespT> call, Throwable t) {
        if (t instanceof DomainExceptions.NotFoundException ex) {
            call.close(Status.NOT_FOUND.withDescription(ex.getMessage()), new Metadata());
        } else if (t instanceof DomainExceptions.ConflictException ex) {
            call.close(Status.ALREADY_EXISTS.withDescription(ex.getMessage()), new Metadata());
        } else if (t instanceof DomainExceptions.ForbiddenException ex) {
            call.close(Status.PERMISSION_DENIED.withDescription(ex.getMessage()), new Metadata());
        } else if (t instanceof DomainExceptions.DomainException ex) {
            call.close(Status.INVALID_ARGUMENT.withDescription(ex.getMessage()), new Metadata());
        } else if (t instanceof StatusRuntimeException ex) {
            call.close(ex.getStatus(), ex.getTrailers());
        } else {
            log.error("Unhandled exception in gRPC handler: {}", t.getMessage(), t);
            call.close(Status.INTERNAL.withDescription("Internal server error"), new Metadata());
        }
    }
}

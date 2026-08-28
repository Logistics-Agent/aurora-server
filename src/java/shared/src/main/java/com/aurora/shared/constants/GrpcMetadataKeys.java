package com.aurora.shared.constants;

import io.grpc.Metadata;

/**
 * Keys cho gRPC Metadata headers (matching .NET JwtClaims.cs & GrpcMetadataKeys).
 */
public final class GrpcMetadataKeys {
    private GrpcMetadataKeys() {}

    public static final String USER_ID_HEADER = "x-user-id";
    public static final String TENANT_ID_HEADER = "x-tenant-id";
    public static final String PERMISSION_VERSION_HEADER = "x-permission-version";
    public static final String ROLE_HEADER = "x-role";
    public static final String ACCESS_TOKEN_HEADER = "x-access-token";
    public static final String TRACE_ID_HEADER = "x-trace-id";
    public static final String SERVICE_ID_HEADER = "x-service-id";
    public static final String CORRELATION_ID_HEADER = "x-correlation-id";

    public static final Metadata.Key<String> USER_ID =
            Metadata.Key.of(USER_ID_HEADER, Metadata.ASCII_STRING_MARSHALLER);
    public static final Metadata.Key<String> TENANT_ID =
            Metadata.Key.of(TENANT_ID_HEADER, Metadata.ASCII_STRING_MARSHALLER);
    public static final Metadata.Key<String> PERMISSION_VERSION =
            Metadata.Key.of(PERMISSION_VERSION_HEADER, Metadata.ASCII_STRING_MARSHALLER);
    public static final Metadata.Key<String> ROLE =
            Metadata.Key.of(ROLE_HEADER, Metadata.ASCII_STRING_MARSHALLER);
    public static final Metadata.Key<String> ACCESS_TOKEN =
            Metadata.Key.of(ACCESS_TOKEN_HEADER, Metadata.ASCII_STRING_MARSHALLER);
    public static final Metadata.Key<String> TRACE_ID =
            Metadata.Key.of(TRACE_ID_HEADER, Metadata.ASCII_STRING_MARSHALLER);
    public static final Metadata.Key<String> SERVICE_ID =
            Metadata.Key.of(SERVICE_ID_HEADER, Metadata.ASCII_STRING_MARSHALLER);
    public static final Metadata.Key<String> CORRELATION_ID =
            Metadata.Key.of(CORRELATION_ID_HEADER, Metadata.ASCII_STRING_MARSHALLER);
}


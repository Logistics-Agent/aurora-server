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
    public static final String ROLE_IDS_HEADER = "x-role-ids";
    public static final String ACCESS_TOKEN_HEADER = "x-access-token";
    public static final String TRACE_ID_HEADER = "x-trace-id";

    public static final Metadata.Key<String> USER_ID =
            Metadata.Key.of(USER_ID_HEADER, Metadata.ASCII_STRING_MARSHALLER);
    public static final Metadata.Key<String> TENANT_ID =
            Metadata.Key.of(TENANT_ID_HEADER, Metadata.ASCII_STRING_MARSHALLER);
    public static final Metadata.Key<String> PERMISSION_VERSION =
            Metadata.Key.of(PERMISSION_VERSION_HEADER, Metadata.ASCII_STRING_MARSHALLER);
    public static final Metadata.Key<String> ROLE_IDS =
            Metadata.Key.of(ROLE_IDS_HEADER, Metadata.ASCII_STRING_MARSHALLER);
    public static final Metadata.Key<String> ACCESS_TOKEN =
            Metadata.Key.of(ACCESS_TOKEN_HEADER, Metadata.ASCII_STRING_MARSHALLER);
    public static final Metadata.Key<String> TRACE_ID =
            Metadata.Key.of(TRACE_ID_HEADER, Metadata.ASCII_STRING_MARSHALLER);
}

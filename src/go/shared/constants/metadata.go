package constants

// Header keys cho gRPC Metadata (trùng khớp hoàn toàn với .NET JwtClaims.cs & Java GrpcMetadataKeys.java)
const (
	HeaderUserID            = "x-user-id"
	HeaderTenantID          = "x-tenant-id"
	HeaderPermissionVersion = "x-permission-version"
	HeaderRole              = "x-role"
	HeaderAccessToken       = "x-access-token"
	HeaderAuthorization     = "authorization"
	HeaderTraceID           = "x-trace-id"
	HeaderServiceID         = "x-service-id"
	HeaderCorrelationID     = "x-correlation-id"
)


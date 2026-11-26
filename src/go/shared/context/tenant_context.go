package context

import (
	"context"

	"github.com/google/uuid"
)

type contextKey string

const (
	tenantIDKey      contextKey = "tenant_id"
	userIDKey        contextKey = "user_id"
	userRoleKey      contextKey = "user_role"
	traceIDKey           contextKey = "trace_id"
	correlationIDKey     contextKey = "correlation_id"
	serviceIDKey         contextKey = "service_id"
	permissionVersionKey contextKey = "permission_version"
)

// WithTenantID gắn TenantID (UUID) vào context
func WithTenantID(ctx context.Context, tenantID uuid.UUID) context.Context {
	return context.WithValue(ctx, tenantIDKey, tenantID)
}

// GetTenantID lấy TenantID từ context. Trả về false nếu không tồn tại hoặc rỗng.
func GetTenantID(ctx context.Context) (uuid.UUID, bool) {
	val, ok := ctx.Value(tenantIDKey).(uuid.UUID)
	if !ok || val == uuid.Nil {
		return uuid.Nil, false
	}
	return val, true
}

// WithUserID gắn UserID (UUID hoặc string) vào context
func WithUserID(ctx context.Context, userID string) context.Context {
	return context.WithValue(ctx, userIDKey, userID)
}

// GetUserID lấy UserID từ context
func GetUserID(ctx context.Context) (string, bool) {
	val, ok := ctx.Value(userIDKey).(string)
	return val, ok && val != ""
}

// WithUserRole gắn UserRole vào context
func WithUserRole(ctx context.Context, role string) context.Context {
	return context.WithValue(ctx, userRoleKey, role)
}

// GetUserRole lấy UserRole từ context
func GetUserRole(ctx context.Context) (string, bool) {
	val, ok := ctx.Value(userRoleKey).(string)
	return val, ok && val != ""
}

// WithTraceID gắn TraceID vào context
func WithTraceID(ctx context.Context, traceID string) context.Context {
	return context.WithValue(ctx, traceIDKey, traceID)
}

// GetTraceID lấy TraceID từ context
func GetTraceID(ctx context.Context) (string, bool) {
	val, ok := ctx.Value(traceIDKey).(string)
	return val, ok && val != ""
}

// WithCorrelationID gắn CorrelationID vào context
func WithCorrelationID(ctx context.Context, correlationID string) context.Context {
	return context.WithValue(ctx, correlationIDKey, correlationID)
}

// GetCorrelationID lấy CorrelationID từ context
func GetCorrelationID(ctx context.Context) (string, bool) {
	val, ok := ctx.Value(correlationIDKey).(string)
	return val, ok && val != ""
}

// WithServiceID gắn ServiceID vào context
func WithServiceID(ctx context.Context, serviceID string) context.Context {
	return context.WithValue(ctx, serviceIDKey, serviceID)
}

// GetServiceID lấy ServiceID từ context
func GetServiceID(ctx context.Context) (string, bool) {
	val, ok := ctx.Value(serviceIDKey).(string)
	return val, ok && val != ""
}

// WithPermissionVersion gắn PermissionVersion vào context
func WithPermissionVersion(ctx context.Context, version int32) context.Context {
	return context.WithValue(ctx, permissionVersionKey, version)
}

// GetPermissionVersion lấy PermissionVersion từ context
func GetPermissionVersion(ctx context.Context) (int32, bool) {
	val, ok := ctx.Value(permissionVersionKey).(int32)
	return val, ok
}


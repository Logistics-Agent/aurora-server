package interceptors

import (
	"context"
	"strconv"
	"strings"

	"aurora/shared/constants"
	sharedctx "aurora/shared/context"

	"github.com/google/uuid"
	"google.golang.org/grpc"
	"google.golang.org/grpc/metadata"
)

// UnaryMetadataInterceptor trích xuất tenant_id, user_id, role, trace_id, service_id từ gRPC metadata vào context
func UnaryMetadataInterceptor() grpc.UnaryServerInterceptor {
	return func(
		ctx context.Context,
		req any,
		info *grpc.UnaryServerInfo,
		handler grpc.UnaryHandler,
	) (any, error) {
		md, ok := metadata.FromIncomingContext(ctx)
		if ok {
			// 1. Trích xuất TenantID
			if values := md.Get(constants.HeaderTenantID); len(values) > 0 {
				if trimmed := strings.TrimSpace(values[0]); trimmed != "" {
					if tenantUUID, err := uuid.Parse(trimmed); err == nil && tenantUUID != uuid.Nil {
						ctx = sharedctx.WithTenantID(ctx, tenantUUID)
					}
				}
			}

			// 2. Trích xuất UserID
			if values := md.Get(constants.HeaderUserID); len(values) > 0 {
				if trimmed := strings.TrimSpace(values[0]); trimmed != "" {
					ctx = sharedctx.WithUserID(ctx, trimmed)
				}
			}

			// 3. Trích xuất UserRole
			if values := md.Get(constants.HeaderRole); len(values) > 0 {
				if trimmed := strings.TrimSpace(values[0]); trimmed != "" {
					ctx = sharedctx.WithUserRole(ctx, trimmed)
				}
			}

			// 4. Trích xuất TraceID
			if values := md.Get(constants.HeaderTraceID); len(values) > 0 {
				if trimmed := strings.TrimSpace(values[0]); trimmed != "" {
					ctx = sharedctx.WithTraceID(ctx, trimmed)
				}
			}

			// 5. Trích xuất CorrelationID
			if values := md.Get(constants.HeaderCorrelationID); len(values) > 0 {
				if trimmed := strings.TrimSpace(values[0]); trimmed != "" {
					ctx = sharedctx.WithCorrelationID(ctx, trimmed)
				}
			}

			// 6. Trích xuất ServiceID
			if values := md.Get(constants.HeaderServiceID); len(values) > 0 {
				if trimmed := strings.TrimSpace(values[0]); trimmed != "" {
					ctx = sharedctx.WithServiceID(ctx, trimmed)
				}
			}

			// 7. Trích xuất PermissionVersion
			if values := md.Get(constants.HeaderPermissionVersion); len(values) > 0 {
				if trimmed := strings.TrimSpace(values[0]); trimmed != "" {
					if pv, err := strconv.ParseInt(trimmed, 10, 32); err == nil {
						ctx = sharedctx.WithPermissionVersion(ctx, int32(pv))
					}
				}
			}
		}

		return handler(ctx, req)
	}
}


package interceptors

import (
	"context"

	"aurora/shared/constants"
	sharedctx "aurora/shared/context"

	"github.com/google/uuid"
	"google.golang.org/grpc"
	"google.golang.org/grpc/metadata"
)

// UnaryMetadataInterceptor trích xuất tenant_id, user_id, role, trace_id từ gRPC metadata vào context
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
			if values := md.Get(constants.HeaderTenantID); len(values) > 0 && values[0] != "" {
				if tenantUUID, err := uuid.Parse(values[0]); err == nil && tenantUUID != uuid.Nil {
					ctx = sharedctx.WithTenantID(ctx, tenantUUID)
				}
			}

			// 2. Trích xuất UserID
			if values := md.Get(constants.HeaderUserID); len(values) > 0 && values[0] != "" {
				ctx = sharedctx.WithUserID(ctx, values[0])
			}

			// 3. Trích xuất UserRole
			if values := md.Get(constants.HeaderRole); len(values) > 0 && values[0] != "" {
				ctx = sharedctx.WithUserRole(ctx, values[0])
			}

			// 4. Trích xuất TraceID
			if values := md.Get(constants.HeaderTraceID); len(values) > 0 && values[0] != "" {
				ctx = sharedctx.WithTraceID(ctx, values[0])
			}

			// 5. Trích xuất CorrelationID
			if values := md.Get(constants.HeaderCorrelationID); len(values) > 0 && values[0] != "" {
				ctx = sharedctx.WithCorrelationID(ctx, values[0])
			}
		}

		return handler(ctx, req)
	}
}


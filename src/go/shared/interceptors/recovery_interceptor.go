package interceptors

import (
	"context"
	"fmt"
	"log/slog"
	"runtime/debug"

	"google.golang.org/grpc"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/status"
)

// UnaryRecoveryInterceptor bắt mọi panic xảy ra trong gRPC handler để chống crash server
func UnaryRecoveryInterceptor(logger *slog.Logger) grpc.UnaryServerInterceptor {
	return func(
		ctx context.Context,
		req any,
		info *grpc.UnaryServerInfo,
		handler grpc.UnaryHandler,
	) (resp any, err error) {
		defer func() {
			if r := recover(); r != nil {
				stack := string(debug.Stack())
				if logger != nil {
					logger.Error("panic recovered in gRPC handler",
						slog.String("method", info.FullMethod),
						slog.Any("panic", r),
						slog.String("stack", stack),
					)
				}
				err = status.Errorf(codes.Internal, "Internal server error: %v", fmt.Sprint(r))
			}
		}()

		return handler(ctx, req)
	}
}


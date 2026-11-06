package interceptors

import (
	"context"
	"log/slog"
	"time"

	sharedctx "aurora/shared/context"

	"google.golang.org/grpc"
	"google.golang.org/grpc/status"
)

// UnaryLoggingInterceptor ghi log request duration, method name và status code
func UnaryLoggingInterceptor(logger *slog.Logger) grpc.UnaryServerInterceptor {
	return func(
		ctx context.Context,
		req any,
		info *grpc.UnaryServerInfo,
		handler grpc.UnaryHandler,
	) (any, error) {
		start := time.Now()
		resp, err := handler(ctx, req)
		duration := time.Since(start)

		st, _ := status.FromError(err)
		code := st.Code()

		var tenantIDStr string
		if tID, ok := sharedctx.GetTenantID(ctx); ok {
			tenantIDStr = tID.String()
		}

		traceID, _ := sharedctx.GetTraceID(ctx)

		logLevel := slog.LevelInfo
		if err != nil {
			logLevel = slog.LevelWarn
		}

		if logger != nil {
			logger.Log(ctx, logLevel, "gRPC call completed",
				slog.String("method", info.FullMethod),
				slog.String("status", code.String()),
				slog.Duration("duration_ms", duration),
				slog.String("tenant_id", tenantIDStr),
				slog.String("trace_id", traceID),
			)
		}

		return resp, err
	}
}


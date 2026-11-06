package main

import (
	"fmt"
	"log/slog"
	"net"
	"os"

	"aurora/route-planning-agent/proto/routeplanningpb"
	"aurora/shared/interceptors"

	"google.golang.org/grpc"
	"google.golang.org/grpc/reflection"
)

type server struct {
	routeplanningpb.UnimplementedRoutePlanningServiceServer
}

func main() {
	logger := slog.New(slog.NewJSONHandler(os.Stdout, nil))
	port := os.Getenv("PORT")
	if port == "" {
		port = "5005"
	}

	listener, err := net.Listen("tcp", fmt.Sprintf(":%s", port))
	if err != nil {
		logger.Error("Lỗi khởi tạo listener", slog.Any("error", err))
		os.Exit(1)
	}

	grpcServer := grpc.NewServer(
		grpc.ChainUnaryInterceptor(
			interceptors.UnaryRecoveryInterceptor(logger),
			interceptors.UnaryMetadataInterceptor(),
			interceptors.UnaryLoggingInterceptor(logger),
		),
	)

	routeplanningpb.RegisterRoutePlanningServiceServer(grpcServer, &server{})
	reflection.Register(grpcServer)

	logger.Info("Route Planning Agent (Golang) đang lắng nghe trên cổng", slog.String("port", port))
	if err := grpcServer.Serve(listener); err != nil {
		logger.Error("Lỗi chạy gRPC server", slog.Any("error", err))
	}
}


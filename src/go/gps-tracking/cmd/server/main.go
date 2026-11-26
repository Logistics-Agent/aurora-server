package main

import (
	"context"
	"fmt"
	"log"
	"log/slog"
	"net"
	"os"
	"os/signal"
	"syscall"

	"aurora/gps-tracking/internal/config"
	"aurora/gps-tracking/internal/infrastructure/background"
	"aurora/gps-tracking/internal/infrastructure/persistence/postgres"
	"aurora/gps-tracking/internal/transport/grpc/handlers"
	"aurora/gps-tracking/proto/gpstrackingpb"
	"aurora/shared/rabbitmq"

	"google.golang.org/grpc"
	"google.golang.org/grpc/reflection"
)

func main() {
	cfg := config.LoadConfig()
	logger := slog.Default()

	log.Printf("Connecting to database...")
	db, err := postgres.NewDB(cfg)
	if err != nil {
		log.Fatalf("Failed to initialize database: %v", err)
	}

	repo := postgres.NewGpsRepository(db, cfg.StationarySpeedKph)
	handler := handlers.NewGpsHandler(repo)

	// Context for background workers and graceful shutdown
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	// Initialize RabbitMQ client (graceful fallback if broker is not yet reachable)
	var rabbitClient *rabbitmq.Client
	if cfg.RabbitMQURL != "" {
		rc, err := rabbitmq.NewClient(cfg.RabbitMQURL, logger)
		if err != nil {
			logger.Warn("RabbitMQ connection failed on startup, outbox/consumers will retry or skip", slog.Any("error", err))
		} else {
			rabbitClient = rc
			defer rabbitClient.Close()
		}
	}

	// Start Background Workers
	outboxPub := background.NewOutboxPublisher(db, rabbitClient, cfg, logger)
	go outboxPub.Start(ctx)

	signalLossWorker := background.NewSignalLossWorker(db, cfg, logger)
	go signalLossWorker.Start(ctx)

	if rabbitClient != nil {
		shipmentConsumer := background.NewShipmentConsumer(db, rabbitClient, logger)
		if err := shipmentConsumer.Start(ctx); err != nil {
			logger.Warn("Failed to start shipment consumer", slog.Any("error", err))
		}
	}

	lis, err := net.Listen("tcp", fmt.Sprintf(":%d", cfg.Port))
	if err != nil {
		log.Fatalf("Failed to listen on port %d: %v", cfg.Port, err)
	}

	grpcServer := grpc.NewServer()
	gpstrackingpb.RegisterGpsTrackingServiceServer(grpcServer, handler)
	reflection.Register(grpcServer)

	// Graceful shutdown
	sigChan := make(chan os.Signal, 1)
	signal.Notify(sigChan, os.Interrupt, syscall.SIGTERM)

	go func() {
		log.Printf("Aurora GPS Tracking Service (Go) started on port :%d", cfg.Port)
		if err := grpcServer.Serve(lis); err != nil {
			log.Printf("gRPC server terminated: %v", err)
		}
	}()

	<-sigChan
	log.Println("Shutting down Aurora GPS Tracking Service gracefully...")
	cancel()
	grpcServer.GracefulStop()
	log.Println("Aurora GPS Tracking Service stopped.")
}


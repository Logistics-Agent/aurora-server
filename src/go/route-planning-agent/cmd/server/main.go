package main

import (
	"context"
	"fmt"
	"log/slog"
	"net"
	"os"
	"os/signal"
	"sync"
	"syscall"
	"time"

	"aurora/route-planning-agent/internal/application/services"
	"aurora/route-planning-agent/internal/config"
	"aurora/route-planning-agent/internal/infrastructure/external/aigov"
	"aurora/route-planning-agent/internal/infrastructure/external/compliance"
	"aurora/route-planning-agent/internal/infrastructure/external/vroom"
	"aurora/route-planning-agent/internal/infrastructure/messaging"
	"aurora/route-planning-agent/internal/infrastructure/outbox"
	"aurora/route-planning-agent/internal/infrastructure/persistence/postgres"
	"aurora/route-planning-agent/internal/infrastructure/rules"
	"aurora/route-planning-agent/internal/transport/grpc/handlers"
	"aurora/route-planning-agent/proto/routeplanningpb"
	"aurora/shared/interceptors"
	"aurora/shared/rabbitmq"

	"google.golang.org/grpc"
	"google.golang.org/grpc/health"
	"google.golang.org/grpc/health/grpc_health_v1"
	"google.golang.org/grpc/reflection"
	gormlogger "gorm.io/gorm/logger"
)

func main() {
	logger := slog.New(slog.NewJSONHandler(os.Stdout, &slog.HandlerOptions{Level: slog.LevelInfo}))

	// 1. Nạp cấu hình môi trường
	cfg := config.LoadConfig()
	logger.Info("Cấu hình RoutePlanningAgent (Go)",
		slog.String("port", cfg.Port),
		slog.String("vroom_url", cfg.VroomURL),
		slog.String("ai_gov_url", cfg.AiGovernanceURL),
		slog.String("compliance_url", cfg.ComplianceRagURL),
	)

	// 2. Kết nối PostgreSQL
	db, err := postgres.NewDB(postgres.Config{
		DSN:             cfg.PostgresDSN,
		MaxIdleConns:    10,
		MaxOpenConns:    50,
		ConnMaxLifetime: time.Hour,
		LogLevel:        gormlogger.Warn,
	})
	if err != nil {
		logger.Warn("Chưa thể kết nối PostgreSQL tại khởi động (chờ database sẵn sàng)", slog.Any("error", err))
	} else {
		logger.Info("Đã kết nối PostgreSQL thành công")
	}

	// 3. Khởi tạo Repositories & UnitOfWork
	routeRepo := postgres.NewRouteRepository(db)
	approvalRepo := postgres.NewApprovalRepository(db)
	policyRepo := postgres.NewRiskPolicyRepository(db)
	ruleConfigRepo := postgres.NewTenantRuleConfigRepository(db)
	outboxRepo := postgres.NewOutboxRepository(db)
	uow := postgres.NewUnitOfWork(db)

	// 4. Khởi tạo Services & External Clients
	ruleConfigService := services.NewTenantRuleConfigService(ruleConfigRepo)
	policyProvider := services.NewRiskPolicyProvider(policyRepo, ruleConfigRepo, ruleConfigService)
	governanceService := services.NewRouteGovernanceService(policyRepo, approvalRepo)
	approvalService := services.NewApprovalService(approvalRepo, routeRepo, uow)
	ruleEngine := rules.NewDefaultRouteRuleEngine()

	vroomService := vroom.NewClient(cfg.VroomURL, 30*time.Second, logger)
	aiService, err := aigov.NewClient(cfg.AiGovernanceURL, "route.plan", logger)
	if err != nil {
		logger.Warn("Không thể khởi tạo AiGovernance client", slog.Any("error", err))
	}
	complianceService, err := compliance.NewClient(cfg.ComplianceRagURL, logger)
	if err != nil {
		logger.Warn("Không thể khởi tạo ComplianceRag client", slog.Any("error", err))
	}

	// 5. Khởi tạo RabbitMQ & Outbox Processor & Consumer
	rootCtx, rootCancel := context.WithCancel(context.Background())
	defer rootCancel()

	var wg sync.WaitGroup

	rabbitClient, err := rabbitmq.NewClient(cfg.RabbitMQURI, logger)
	if err != nil {
		logger.Warn("Chưa thể kết nối RabbitMQ tại thời điểm khởi động", slog.Any("error", err))
	} else {
		outboxProcessor := outbox.NewProcessor(outboxRepo, rabbitClient, outbox.Config{
			PollingInterval: cfg.OutboxPollingSecs,
			BatchSize:       cfg.OutboxBatchSize,
			Exchange:        "route.events",
		}, logger)

		wg.Add(1)
		go func() {
			defer wg.Done()
			outboxProcessor.Start(rootCtx)
		}()

		ruleConfigConsumer := messaging.NewRuleConfigConsumer(rabbitClient, ruleConfigService, "tenant_rule_config_changed_event", logger)
		wg.Add(1)
		go func() {
			defer wg.Done()
			if err := ruleConfigConsumer.Start(rootCtx); err != nil && rootCtx.Err() == nil {
				logger.Error("RuleConfigConsumer dừng do lỗi", slog.Any("error", err))
			}
		}()
	}

	// 6. Khởi tạo gRPC Handler & Server
	handler := handlers.NewRoutePlanningHandler(
		routeRepo, approvalRepo, policyRepo, ruleConfigRepo, outboxRepo,
		uow, ruleEngine, vroomService, aiService, complianceService,
		governanceService, approvalService, policyProvider, ruleConfigService,
		logger,
	)

	grpcServer := grpc.NewServer(
		grpc.ChainUnaryInterceptor(
			interceptors.UnaryRecoveryInterceptor(logger),
			interceptors.UnaryMetadataInterceptor(),
			interceptors.UnaryLoggingInterceptor(logger),
		),
	)

	routeplanningpb.RegisterRoutePlanningServiceServer(grpcServer, handler)

	// Health check service
	healthServer := health.NewServer()
	grpc_health_v1.RegisterHealthServer(grpcServer, healthServer)
	healthServer.SetServingStatus("", grpc_health_v1.HealthCheckResponse_SERVING)

	reflection.Register(grpcServer)

	listener, err := net.Listen("tcp", fmt.Sprintf(":%s", cfg.Port))
	if err != nil {
		logger.Error("Lỗi khởi tạo TCP listener", slog.Any("error", err))
		os.Exit(1)
	}

	// 7. Graceful Shutdown
	sigCh := make(chan os.Signal, 1)
	signal.Notify(sigCh, os.Interrupt, syscall.SIGTERM)

	go func() {
		<-sigCh
		logger.Info("Đang tắt gRPC Server an toàn (Graceful Shutdown)...")
		rootCancel()
		healthServer.SetServingStatus("", grpc_health_v1.HealthCheckResponse_NOT_SERVING)
		grpcServer.GracefulStop()
		wg.Wait()
		if rabbitClient != nil {
			_ = rabbitClient.Close()
		}
		if db != nil {
			if sqlDB, err := db.DB(); err == nil && sqlDB != nil {
				_ = sqlDB.Close()
			}
		}
		logger.Info("Hệ thống đã dừng hoàn toàn.")
	}()

	logger.Info("Route Planning Agent (Golang) đang hoạt động trên cổng", slog.String("port", cfg.Port))
	if err := grpcServer.Serve(listener); err != nil && err != grpc.ErrServerStopped {
		logger.Error("Lỗi chạy gRPC server", slog.Any("error", err))
	}
}

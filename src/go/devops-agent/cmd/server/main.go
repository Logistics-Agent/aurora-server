package main

import (
	"context"
	"errors"
	"fmt"
	"log/slog"
	"net"
	"net/http"
	"os"
	"os/signal"
	"sync"
	"syscall"
	"time"

	"aurora/devops-agent/internal/application"
	"aurora/devops-agent/internal/config"
	"aurora/devops-agent/internal/infrastructure/ai"
	"aurora/devops-agent/internal/infrastructure/metrics"
	pg "aurora/devops-agent/internal/infrastructure/persistence/postgres"
	"aurora/devops-agent/internal/infrastructure/rag"
	"aurora/devops-agent/internal/infrastructure/security"
	"aurora/devops-agent/internal/infrastructure/workers"
	grpchandler "aurora/devops-agent/internal/transport/grpc"
	httphandler "aurora/devops-agent/internal/transport/http"
	"aurora/devops-agent/proto/aigovpb"
	"aurora/devops-agent/proto/devopsagentpb"
	"aurora/devops-agent/proto/devopsragpb"
	"aurora/shared/interceptors"
	"aurora/shared/rabbitmq"

	"github.com/prometheus/client_golang/prometheus"
	"github.com/redis/go-redis/v9"
	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
	"google.golang.org/grpc/health"
	"google.golang.org/grpc/health/grpc_health_v1"
	"google.golang.org/grpc/reflection"
	"gorm.io/driver/postgres"
	"gorm.io/gorm"
	gormlogger "gorm.io/gorm/logger"
)

func main() {
	logger := slog.New(slog.NewJSONHandler(os.Stdout, &slog.HandlerOptions{Level: slog.LevelInfo}))
	cfg := config.LoadConfig()
	logger.Info("Starting devops-agent (Go)",
		slog.String("grpcPort", cfg.GRPCPort),
		slog.String("httpPort", cfg.HTTPPort),
		slog.String("aiGovAddr", cfg.AiGovernanceGRPCAddr),
		slog.String("ragAddr", cfg.RagServiceGRPCAddr),
	)

	// 1. PostgreSQL
	db, err := gorm.Open(postgres.Open(cfg.PostgresDSN), &gorm.Config{
		Logger: gormlogger.Default.LogMode(gormlogger.Warn),
	})
	if err != nil {
		logger.Warn("Could not connect to PostgreSQL at startup", slog.Any("error", err))
	} else {
		if sqlDB, err := db.DB(); err == nil {
			sqlDB.SetMaxIdleConns(5)
			sqlDB.SetMaxOpenConns(20)
			sqlDB.SetConnMaxLifetime(5 * time.Minute)
		}
		logger.Info("Connected to PostgreSQL")
	}

	// 2. Redis
	redisClient := redis.NewClient(&redis.Options{
		Addr:     cfg.RedisAddr,
		Password: cfg.RedisPassword,
	})
	if err := redisClient.Ping(context.Background()).Err(); err != nil {
		logger.Warn("Redis ping failed at startup", slog.Any("error", err))
	}

	// 3. RabbitMQ
	rabbitClient, err := rabbitmq.NewClient(cfg.RabbitMQURI, logger)
	if err != nil {
		logger.Warn("Could not connect to RabbitMQ at startup", slog.Any("error", err))
	}

	// 4. External gRPC Clients (AiGovernance + DevOps-RAG)
	var aiStub ai.AiExecutionStub
	aiConn, err := grpc.NewClient(cfg.AiGovernanceGRPCAddr, grpc.WithTransportCredentials(insecure.NewCredentials()))
	if err != nil {
		logger.Warn("Failed to create AiGovernance gRPC client", slog.Any("error", err))
	} else {
		defer aiConn.Close()
		aiStub = aigovpb.NewAiExecutionServiceClient(aiConn)
	}

	var ragStub rag.RagQueryStub
	ragConn, err := grpc.NewClient(cfg.RagServiceGRPCAddr, grpc.WithTransportCredentials(insecure.NewCredentials()))
	if err != nil {
		logger.Warn("Failed to create DevOps-RAG gRPC client", slog.Any("error", err))
	} else {
		defer ragConn.Close()
		ragStub = devopsragpb.NewDevOpsRagServiceClient(ragConn)
	}

	// 5. Wire Repositories & Application Services
	uow := pg.NewUnitOfWork(db)
	incidentRepo := pg.NewIncidentRepository(db)
	rcaRepo := pg.NewRcaAnalysisRepository(db)
	existingRuleRepo := pg.NewExistingRuleRepository(db)
	pendingRuleRepo := pg.NewPendingRuleRepository(db)
	selfConfigRepo := pg.NewSelfConfigRepository(db)
	approvalRepo := pg.NewPrApprovalRecordRepository(db)
	outboxRepo := pg.NewOutboxRepository(db)

	m := metrics.NewDevOpsMetrics(prometheus.DefaultRegisterer)
	redaction := security.NewRedactionService()
	dedup := application.NewDedupService(redisClient, logger)
	antiFlap := application.NewAntiFlappingTracker(redisClient, logger)
	classifier := application.NewSeverityClassifier()

	aiGovClient := ai.NewGrpcAiGovernanceClient(aiStub, logger)
	ragClient := rag.NewDevOpsRagClient(ragStub, logger)

	ingestHandler := application.NewIngestAlertHandler(
		incidentRepo, outboxRepo, uow, dedup, antiFlap, classifier, redaction, m, logger,
	)
	rcaOrchestrator := application.NewRcaOrchestratorService(
		incidentRepo, rcaRepo, redaction, ragClient, aiGovClient, outboxRepo, m, logger,
	)
	incidentService := application.NewIncidentService(incidentRepo, outboxRepo, uow)
	ruleService := application.NewRuleService(existingRuleRepo, pendingRuleRepo, outboxRepo, uow)
	selfConfigService := application.NewSelfConfigService(selfConfigRepo, outboxRepo, uow)

	// 6. Start Background Workers
	rootCtx, rootCancel := context.WithCancel(context.Background())
	defer rootCancel()

	var wg sync.WaitGroup
	rcaWorker := workers.NewRcaAnalysisWorker(incidentRepo, outboxRepo, uow, rcaOrchestrator, m, cfg.RcaPollInterval, cfg.RcaBatchSize, logger)
	outboxWorker := workers.NewOutboxPollerWorker(outboxRepo, uow, rabbitClient, m, cfg.OutboxPollInterval, logger)
	approvalWorker := workers.NewApprovalTimeoutWorker(approvalRepo, outboxRepo, uow, m, cfg.ApprovalTimeoutCheckInterval, logger)

	wg.Add(3)
	go func() { defer wg.Done(); rcaWorker.Start(rootCtx) }()
	go func() { defer wg.Done(); outboxWorker.Start(rootCtx) }()
	go func() { defer wg.Done(); approvalWorker.Start(rootCtx) }()

	// 7. Start HTTP server (Prometheus webhook, health, metrics)
	httpServer := &http.Server{
		Addr: fmt.Sprintf(":%s", cfg.HTTPPort),
		Handler: httphandler.NewHTTPHandler(ingestHandler, cfg.AlertmanagerWebhookToken, func() bool {
			return db != nil
		}),
		ReadHeaderTimeout: 10 * time.Second,
	}
	go func() {
		logger.Info("HTTP server listening", slog.String("port", cfg.HTTPPort))
		if err := httpServer.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
			logger.Error("HTTP server error", slog.Any("error", err))
		}
	}()

	// 8. Start gRPC server
	grpcServer := grpc.NewServer(
		grpc.ChainUnaryInterceptor(
			interceptors.UnaryRecoveryInterceptor(logger),
			interceptors.UnaryMetadataInterceptor(),
			interceptors.UnaryLoggingInterceptor(logger),
		),
	)
	devopsagentpb.RegisterDevOpsIngestionServiceServer(grpcServer, grpchandler.NewIngestionGrpcHandler(ingestHandler))
	devopsagentpb.RegisterDevOpsIncidentServiceServer(grpcServer, grpchandler.NewIncidentGrpcHandler(incidentService))
	devopsagentpb.RegisterDevOpsRuleServiceServer(grpcServer, grpchandler.NewRuleGrpcHandler(ruleService))
	devopsagentpb.RegisterDevOpsConfigServiceServer(grpcServer, grpchandler.NewSelfConfigGrpcHandler(selfConfigService))

	healthServer := health.NewServer()
	grpc_health_v1.RegisterHealthServer(grpcServer, healthServer)
	healthServer.SetServingStatus("", grpc_health_v1.HealthCheckResponse_SERVING)
	reflection.Register(grpcServer)

	lis, err := net.Listen("tcp", fmt.Sprintf(":%s", cfg.GRPCPort))
	if err != nil {
		logger.Error("Failed to listen on gRPC port", slog.Any("error", err))
		os.Exit(1)
	}

	sigCh := make(chan os.Signal, 1)
	signal.Notify(sigCh, os.Interrupt, syscall.SIGTERM)
	go func() {
		<-sigCh
		logger.Info("Shutting down devops-agent...")
		rootCancel()
		healthServer.SetServingStatus("", grpc_health_v1.HealthCheckResponse_NOT_SERVING)
		shutdownCtx, cancel := context.WithTimeout(context.Background(), 10*time.Second)
		defer cancel()
		_ = httpServer.Shutdown(shutdownCtx)
		grpcServer.GracefulStop()
		wg.Wait()
		if rabbitClient != nil {
			_ = rabbitClient.Close()
		}
		if redisClient != nil {
			_ = redisClient.Close()
		}
		if db != nil {
			if sqlDB, err := db.DB(); err == nil && sqlDB != nil {
				_ = sqlDB.Close()
			}
		}
	}()

	logger.Info("gRPC server listening", slog.String("port", cfg.GRPCPort))
	if err := grpcServer.Serve(lis); err != nil && !errors.Is(err, grpc.ErrServerStopped) {
		logger.Error("gRPC server error", slog.Any("error", err))
	}
}

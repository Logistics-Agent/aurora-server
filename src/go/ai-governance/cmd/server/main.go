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

	"aurora/ai-governance/internal/config"
	gwapp "aurora/ai-governance/internal/gateway/application"
	gwcache "aurora/ai-governance/internal/gateway/infrastructure/cache"
	"aurora/ai-governance/internal/gateway/infrastructure/credential"
	gwpersist "aurora/ai-governance/internal/gateway/infrastructure/persistence"
	"aurora/ai-governance/internal/gateway/infrastructure/provider"
	govapp "aurora/ai-governance/internal/governance/application"
	govdomain "aurora/ai-governance/internal/governance/domain"
	govcache "aurora/ai-governance/internal/governance/infrastructure/cache"
	"aurora/ai-governance/internal/governance/infrastructure/messaging"
	govpersist "aurora/ai-governance/internal/governance/infrastructure/persistence"
	govsvc "aurora/ai-governance/internal/governance/infrastructure/service"
	"aurora/ai-governance/internal/metrics"
	"aurora/ai-governance/internal/orchestration"
	grpchandler "aurora/ai-governance/internal/transport/grpc"
	httphandler "aurora/ai-governance/internal/transport/http"
	"aurora/ai-governance/proto/aigovpb"
	"aurora/ai-governance/proto/iampb"
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
	logger.Info("Starting ai-governance (Go)",
		slog.String("grpcPort", cfg.GRPCPort),
		slog.String("httpPort", cfg.HTTPPort),
		slog.String("iamAddr", cfg.IamTenantGRPCAddr),
	)

	// 1. PostgreSQL (bounded retry at startup)
	var db *gorm.DB
	var err error
	for attempt := 1; attempt <= 5; attempt++ {
		db, err = gorm.Open(postgres.Open(cfg.PostgresDSN), &gorm.Config{
			Logger: gormlogger.Default.LogMode(gormlogger.Warn),
		})
		if err == nil {
			if sqlDB, dbErr := db.DB(); dbErr == nil {
				sqlDB.SetMaxIdleConns(2)
				sqlDB.SetMaxOpenConns(10)
				sqlDB.SetConnMaxLifetime(time.Hour)
				if pingErr := sqlDB.Ping(); pingErr == nil {
					break
				} else {
					err = pingErr
				}
			}
		}
		logger.Warn("Waiting for PostgreSQL...", slog.Int("attempt", attempt), slog.Any("error", err))
		time.Sleep(2 * time.Second)
	}
	if err != nil {
		logger.Warn("Could not connect to PostgreSQL at startup", slog.Any("error", err))
	} else {
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

	// 3. IAM gRPC client
	var iamClient govsvc.IamTenantClient
	iamConn, err := grpc.NewClient(cfg.IamTenantGRPCAddr, grpc.WithTransportCredentials(insecure.NewCredentials()))
	if err != nil {
		logger.Warn("Failed to create IamTenant gRPC client", slog.Any("error", err))
	} else {
		defer iamConn.Close()
		iamClient = iampb.NewIamServiceClient(iamConn)
	}

	// 4. RabbitMQ
	rootCtx, rootCancel := context.WithCancel(context.Background())
	defer rootCancel()

	var wg sync.WaitGroup
	rabbitClient, err := rabbitmq.NewClient(cfg.RabbitMQURI, logger)
	if err != nil {
		logger.Warn("Could not connect to RabbitMQ at startup", slog.Any("error", err))
	}

	// 5. Wire Governance Context
	planRepo := govpersist.NewPlanRepository(db)
	usageRepo := govpersist.NewUsageRecordRepository(db)
	processedRepo := govpersist.NewProcessedEventRepository(db)
	uow := govpersist.NewUnitOfWork(db)

	periodCalc := govapp.NewPeriodKeyCalculator()
	quotaAdapter := govcache.NewQuotaRedisAdapter(redisClient, logger)
	tenantPlanResolver := govsvc.NewCachedTenantPlanResolver(redisClient, iamClient, cfg.TenantPlanCacheTTL, logger)
	policyService := govapp.NewGovernancePolicyService(tenantPlanResolver, planRepo, quotaAdapter, periodCalc, logger)
	quotaSyncService := govapp.NewQuotaSyncService(quotaAdapter, usageRepo, periodCalc, logger)
	auditPublisher := messaging.NewRabbitMqPolicyAuditPublisher(rabbitClient, logger)
	usagePublisher := messaging.NewUsageEventPublisher(rabbitClient, logger)

	if rabbitClient != nil {
		usageConsumer := messaging.NewAiUsageEventConsumer(rabbitClient, processedRepo, uow, quotaSyncService, logger)
		wg.Add(1)
		go func() {
			defer wg.Done()
			if err := usageConsumer.Start(rootCtx); err != nil && rootCtx.Err() == nil {
				logger.Error("AiUsageEventConsumer stopped with error", slog.Any("error", err))
			}
		}()
	}

	// 6. Wire Gateway Context
	slotRepo := gwpersist.NewProviderSlotRepository(db)
	poolPolicyRepo := gwpersist.NewServiceProviderPoolPolicyRepository(db)

	rateWindowPolicy := provider.NewUtcRateWindowPolicy()
	ratePolicies := map[govdomain.AiProvider]gwapp.ProviderRateWindowPolicy{
		govdomain.AiProviderGemini:      rateWindowPolicy,
		govdomain.AiProviderAzureOpenAI: rateWindowPolicy,
	}
	capacityAdapter := gwcache.NewRedisCapacityAdapter(redisClient, ratePolicies, logger)
	limitPolicy := gwapp.NewProviderCapacityLimitPolicy(cfg.CapacityHeadroomRatio)
	capacityService := gwapp.NewCapacityReservationService(capacityAdapter, limitPolicy)
	routingService := gwapp.NewProviderRoutingService(slotRepo, poolPolicyRepo, capacityService, logger)

	credResolver := credential.NewKeyVaultCredentialResolver(cfg.AzureKeyVaultURL, cfg.CredentialCacheTTL, logger)
	providerClients := map[govdomain.AiProvider]gwapp.AiProviderClient{
		govdomain.AiProviderGemini:      provider.NewGeminiProviderClient(credResolver, cfg.GeminiBaseURL, logger),
		govdomain.AiProviderAzureOpenAI: provider.NewAzureOpenAiProviderClient(credResolver, cfg.AzureOpenAIEndpoint, logger),
	}
	aiExecutionService := gwapp.NewAiExecutionService(routingService, capacityService, providerClients, slotRepo, logger)

	// 7. Wire Orchestration & Metrics
	m := metrics.NewMetrics(prometheus.DefaultRegisterer)
	executeAiService := orchestration.NewExecuteAiService(policyService, aiExecutionService, auditPublisher, usagePublisher, m)

	// 8. Start HTTP server (health & Prometheus)
	httpServer := &http.Server{
		Addr: fmt.Sprintf(":%s", cfg.HTTPPort),
		Handler: httphandler.NewHTTPHandler(func() bool {
			if db == nil {
				return false
			}
			sqlDB, dbErr := db.DB()
			if dbErr != nil || sqlDB == nil {
				return false
			}
			pingCtx, cancel := context.WithTimeout(context.Background(), 2*time.Second)
			defer cancel()
			if err := sqlDB.PingContext(pingCtx); err != nil {
				return false
			}
			if redisClient != nil {
				if err := redisClient.Ping(pingCtx).Err(); err != nil {
					return false
				}
			}
			return true
		}),
		ReadHeaderTimeout: 10 * time.Second,
	}
	go func() {
		logger.Info("HTTP server listening", slog.String("port", cfg.HTTPPort))
		if err := httpServer.ListenAndServe(); err != nil && !errors.Is(err, http.ErrServerClosed) {
			logger.Error("HTTP server error", slog.Any("error", err))
		}
	}()

	// 9. Start gRPC server
	grpcServer := grpc.NewServer(
		grpc.ChainUnaryInterceptor(
			interceptors.UnaryRecoveryInterceptor(logger),
			interceptors.UnaryMetadataInterceptor(),
			interceptors.UnaryLoggingInterceptor(logger),
		),
	)
	aigovpb.RegisterAiGovernanceServiceServer(grpcServer, grpchandler.NewPolicyGrpcHandler(policyService))
	aigovpb.RegisterAiExecutionServiceServer(grpcServer, grpchandler.NewAiExecutionGrpcHandler(executeAiService, logger))

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
		logger.Info("Shutting down ai-governance...")
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

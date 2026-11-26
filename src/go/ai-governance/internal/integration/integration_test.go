package integration_test

import (
	"context"
	"fmt"
	"io"
	"log/slog"
	"net"
	"net/http"
	"net/http/httptest"
	"os"
	"strings"
	"sync"
	"sync/atomic"
	"testing"
	"time"

	gwapp "aurora/ai-governance/internal/gateway/application"
	gwdomain "aurora/ai-governance/internal/gateway/domain"
	gwcache "aurora/ai-governance/internal/gateway/infrastructure/cache"
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
	"aurora/ai-governance/internal/shared"
	grpchandler "aurora/ai-governance/internal/transport/grpc"
	"aurora/ai-governance/proto/aigovpb"
	"aurora/ai-governance/proto/commonpb"
	"aurora/ai-governance/proto/iampb"
	"aurora/shared/constants"
	"aurora/shared/interceptors"
	"aurora/shared/rabbitmq"

	"github.com/google/uuid"
	"github.com/prometheus/client_golang/prometheus"
	amqp "github.com/rabbitmq/amqp091-go"
	"github.com/redis/go-redis/v9"
	"google.golang.org/grpc"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/credentials/insecure"
	"google.golang.org/grpc/metadata"
	"google.golang.org/grpc/status"
	"gorm.io/driver/postgres"
	"gorm.io/gorm"
	gormlogger "gorm.io/gorm/logger"
)

func requireIntegration(t *testing.T) {
	t.Helper()
	if os.Getenv("INTEGRATION_TEST") != "1" {
		t.Skip("set INTEGRATION_TEST=1 to run real infrastructure integration tests")
	}
}

func connectInfra(t *testing.T) (*gorm.DB, *redis.Client, *rabbitmq.Client) {
	t.Helper()
	dsn := os.Getenv("TEST_AIGOV_DSN")
	if dsn == "" {
		dsn = "host=localhost user=aurora_admin password=aurora_secure_password_2026! dbname=ai_governance_db port=5433 sslmode=disable TimeZone=UTC"
	}
	db, err := gorm.Open(postgres.Open(dsn), &gorm.Config{
		Logger: gormlogger.Default.LogMode(gormlogger.Silent),
	})
	if err != nil {
		t.Fatalf("failed to connect postgres: %v", err)
	}

	redisAddr := os.Getenv("TEST_REDIS_ADDR")
	if redisAddr == "" {
		redisAddr = "localhost:6379"
	}
	rdb := redis.NewClient(&redis.Options{Addr: redisAddr})
	if err := rdb.Ping(context.Background()).Err(); err != nil {
		t.Fatalf("failed to ping redis: %v", err)
	}

	rabbitURI := os.Getenv("TEST_RABBITMQ_URI")
	if rabbitURI == "" {
		rabbitURI = "amqp://aurora_admin:aurora_secure_password_2026!@localhost:5672/"
	}
	logger := slog.New(slog.NewTextHandler(io.Discard, nil))
	rmq, err := rabbitmq.NewClient(rabbitURI, logger)
	if err != nil {
		t.Fatalf("failed to connect rabbitmq: %v", err)
	}

	t.Cleanup(func() {
		_ = rmq.Close()
		_ = rdb.Close()
		if sqlDB, err := db.DB(); err == nil && sqlDB != nil {
			_ = sqlDB.Close()
		}
	})
	return db, rdb, rmq
}

type stubIamServer struct {
	iampb.UnimplementedIamServiceServer
	tenants map[string]*iampb.TenantResponse
}

func (s *stubIamServer) GetTenant(_ context.Context, req *iampb.GetTenantRequest) (*iampb.TenantResponse, error) {
	id := req.GetId()
	if resp, ok := s.tenants[id]; ok {
		return resp, nil
	}
	return nil, status.Errorf(codes.NotFound, "tenant %s not found", id)
}

type staticCredPort struct{ key string }

func (s staticCredPort) ResolveSecret(ref string) (string, error) {
	if ref == "" {
		return "", fmt.Errorf("secretRef cannot be null")
	}
	return s.key, nil
}
func (s staticCredPort) Evict(_ string) {}

func TestIntegration_AiGovernance_PolicyCacheAndFailClosed(t *testing.T) {
	requireIntegration(t)
	db, rdb, _ := connectInfra(t)
	logger := slog.New(slog.NewTextHandler(io.Discard, nil))
	ctx := context.Background()

	tenantID := uuid.New()
	missingTenantID := uuid.New()
	cacheKey := "ai-governance:tenant-plan:" + tenantID.String()
	t.Cleanup(func() {
		_ = rdb.Del(ctx, cacheKey).Err()
	})

	// Start stub IAM gRPC server
	iamLis, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		t.Fatalf("iam listen: %v", err)
	}
	iamSrv := grpc.NewServer()
	iampb.RegisterIamServiceServer(iamSrv, &stubIamServer{
		tenants: map[string]*iampb.TenantResponse{
			tenantID.String(): {
				Id:       tenantID.String(),
				PlanType: commonpb.PlanType_STANDARD,
				Status:   commonpb.TenantStatus_TENANT_STATUS_ACTIVE,
			},
		},
	})
	go func() { _ = iamSrv.Serve(iamLis) }()
	defer iamSrv.Stop()

	iamConn, err := grpc.NewClient(iamLis.Addr().String(), grpc.WithTransportCredentials(insecure.NewCredentials()))
	if err != nil {
		t.Fatalf("iam dial: %v", err)
	}
	defer iamConn.Close()

	planRepo := govpersist.NewPlanRepository(db)
	quotaAdapter := govcache.NewQuotaRedisAdapter(rdb, logger)
	resolver := govsvc.NewCachedTenantPlanResolver(rdb, iampb.NewIamServiceClient(iamConn), 5*time.Minute, logger)
	policySvc := govapp.NewGovernancePolicyService(resolver, planRepo, quotaAdapter, govapp.NewPeriodKeyCalculator(), logger)

	// Start real AiGovernanceService gRPC server
	govLis, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		t.Fatalf("gov listen: %v", err)
	}
	govSrv := grpc.NewServer(grpc.ChainUnaryInterceptor(interceptors.UnaryMetadataInterceptor()))
	aigovpb.RegisterAiGovernanceServiceServer(govSrv, grpchandler.NewPolicyGrpcHandler(policySvc))
	go func() { _ = govSrv.Serve(govLis) }()
	defer govSrv.Stop()

	govConn, err := grpc.NewClient(govLis.Addr().String(), grpc.WithTransportCredentials(insecure.NewCredentials()))
	if err != nil {
		t.Fatalf("gov dial: %v", err)
	}
	defer govConn.Close()
	client := aigovpb.NewAiGovernanceServiceClient(govConn)

	// 1. Missing x-service-id -> UNAUTHENTICATED
	_, err = client.ExecutePolicy(ctx, &aigovpb.ExecutePolicyRequest{CapabilityCode: "route.plan"})
	if status.Code(err) != codes.Unauthenticated {
		t.Fatalf("expected Unauthenticated, got %v", status.Code(err))
	}

	// 2. Trusted internal devops-agent + devops.rca -> ALLOWED without tenant
	devopsCtx := metadata.NewOutgoingContext(ctx, metadata.Pairs(constants.HeaderServiceID, "devops-agent"))
	polResp, err := client.ExecutePolicy(devopsCtx, &aigovpb.ExecutePolicyRequest{
		CapabilityCode:       "devops.rca",
		EstimatedInputTokens: 200,
		MaxOutputTokens:      500,
	})
	if err != nil || !polResp.GetAllowed() {
		t.Fatalf("expected devops.rca allowed, err=%v resp=%+v", err, polResp)
	}

	// 3. Unauthorized internal cross-capability: devops-agent + compliance.embed without tenant -> DENIED TENANT_NOT_FOUND
	crossResp, err := client.ExecutePolicy(devopsCtx, &aigovpb.ExecutePolicyRequest{CapabilityCode: "compliance.embed"})
	if err != nil || crossResp.GetAllowed() || crossResp.GetDenyReason() != "TENANT_NOT_FOUND" {
		t.Fatalf("expected cross-capability bypass blocked with TENANT_NOT_FOUND, err=%v resp=%+v", err, crossResp)
	}

	// 4. Tenant-scoped policy check -> populates Redis cache via IAM gRPC
	tenantCtx := metadata.NewOutgoingContext(ctx, metadata.Pairs(
		constants.HeaderServiceID, "route-planning-agent",
		constants.HeaderTenantID, tenantID.String(),
	))
	tResp, err := client.ExecutePolicy(tenantCtx, &aigovpb.ExecutePolicyRequest{
		CapabilityCode:       "route.plan",
		EstimatedInputTokens: 100,
		MaxOutputTokens:      200,
	})
	if err != nil || !tResp.GetAllowed() {
		t.Fatalf("expected route.plan allowed for STANDARD tenant, err=%v resp=%+v", err, tResp)
	}
	if cached, err := rdb.Get(ctx, cacheKey).Result(); err != nil || !strings.Contains(cached, "STANDARD") {
		t.Fatalf("expected Redis tenant-plan cache populated, err=%v val=%q", err, cached)
	}

	// 5. Stop IAM server -> cached tenant still works; uncached tenant fails closed with POLICY_ERROR
	iamSrv.Stop()
	tRespCached, err := client.ExecutePolicy(tenantCtx, &aigovpb.ExecutePolicyRequest{CapabilityCode: "route.plan"})
	if err != nil || !tRespCached.GetAllowed() {
		t.Fatalf("expected cached tenant still allowed after IAM stop, err=%v resp=%+v", err, tRespCached)
	}

	missCtx := metadata.NewOutgoingContext(ctx, metadata.Pairs(
		constants.HeaderServiceID, "route-planning-agent",
		constants.HeaderTenantID, missingTenantID.String(),
	))
	missResp, err := client.ExecutePolicy(missCtx, &aigovpb.ExecutePolicyRequest{CapabilityCode: "route.plan"})
	if err != nil || missResp.GetAllowed() || missResp.GetDenyReason() != "POLICY_ERROR" {
		t.Fatalf("expected fail-closed POLICY_ERROR when IAM unreachable, err=%v resp=%+v", err, missResp)
	}
}

func TestIntegration_AiGovernance_ConcurrentCapacityReservationAndRelease(t *testing.T) {
	requireIntegration(t)
	_, rdb, _ := connectInfra(t)
	logger := slog.New(slog.NewTextHandler(io.Discard, nil))
	ctx := context.Background()

	alias := "test-conc-slot-" + uuid.NewString()[:8]
	slot := &gwdomain.ProviderSlot{
		ID:        uuid.New(),
		Provider:  govdomain.AiProviderGemini,
		Operation: shared.AiOperationGenerate,
		SlotAlias: alias,
		RpmLimit:  15, // effective = floor(15 * 0.8) = 12
		TpmLimit:  250000,
		RpdLimit:  500,
		Priority:  1,
		Enabled:   true,
	}

	winPolicy := provider.NewUtcRateWindowPolicy()
	adapter := gwcache.NewRedisCapacityAdapter(rdb, map[govdomain.AiProvider]gwapp.ProviderRateWindowPolicy{
		govdomain.AiProviderGemini: winPolicy,
	}, logger)
	capSvc := gwapp.NewCapacityReservationService(adapter, gwapp.NewProviderCapacityLimitPolicy(0.20))

	t.Cleanup(func() {
		keys, _ := rdb.Keys(ctx, "ai:capacity:gemini:"+alias+":*").Result()
		if len(keys) > 0 {
			_ = rdb.Del(ctx, keys...).Err()
		}
	})

	// 25 concurrent TryReserve requests; effective RPM = 12
	const totalGoroutines = 25
	var wg sync.WaitGroup
	var successCount atomic.Int32
	reservations := make([]*gwdomain.ProviderReservation, totalGoroutines)

	for i := 0; i < totalGoroutines; i++ {
		wg.Add(1)
		go func(idx int) {
			defer wg.Done()
			res, err := capSvc.TryReserve(ctx, slot, 1000)
			if err != nil {
				t.Errorf("TryReserve returned unexpected error: %v", err)
				return
			}
			if res != nil {
				successCount.Add(1)
				reservations[idx] = res
			}
		}(i)
	}
	wg.Wait()

	if successCount.Load() != 12 {
		t.Fatalf("expected exactly 12 reservations under 25 concurrent goroutines, got %d", successCount.Load())
	}

	sc := capSvc.GetSlotCapacity(ctx, slot)
	if sc.CurrentRpm != 12 || sc.CurrentTpm != 12000 || sc.CurrentRpd != 12 {
		t.Fatalf("unexpected Redis counters after reservation: %+v", sc)
	}

	// Reconcile one reservation (reserved 1000 -> actual 450, delta = -550)
	var sampleRes *gwdomain.ProviderReservation
	for _, r := range reservations {
		if r != nil {
			sampleRes = r
			break
		}
	}
	capSvc.Reconcile(ctx, sampleRes, 450)
	scAfterReconcile := capSvc.GetSlotCapacity(ctx, slot)
	if scAfterReconcile.CurrentTpm != 11450 {
		t.Fatalf("expected TPM=11450 after reconciliation, got %d", scAfterReconcile.CurrentTpm)
	}

	// Release on a non-existent/expired key must NOT create a negative or TTL-less key
	ghost := &gwdomain.ProviderReservation{
		ReservationID:  "ghost",
		Slot:           slot,
		ReservedTokens: 500,
		RpmKey:         "ai:capacity:gemini:" + alias + ":rpm:expired",
		TpmKey:         "ai:capacity:gemini:" + alias + ":tpm:expired",
		RpdKey:         "ai:capacity:gemini:" + alias + ":rpd:expired",
	}
	capSvc.Release(ctx, ghost)
	exists, _ := rdb.Exists(ctx, ghost.RpmKey, ghost.TpmKey, ghost.RpdKey).Result()
	if exists != 0 {
		t.Fatalf("expected ghost Release not to create keys in Redis, exists=%d", exists)
	}
}

func TestIntegration_AiGovernance_RabbitMQQuotaConsumptionAndIdempotency(t *testing.T) {
	requireIntegration(t)
	db, rdb, rmq := connectInfra(t)
	logger := slog.New(slog.NewTextHandler(io.Discard, nil))
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	tenantID := uuid.New()
	eventID := "evt-int-" + uuid.NewString()
	ts := time.Now().UTC()

	t.Cleanup(func() {
		_ = db.Exec("DELETE FROM usage_records WHERE tenant_id = ?", tenantID).Error
		_ = db.Exec("DELETE FROM processed_events WHERE event_id = ?", eventID).Error
		keys, _ := rdb.Keys(ctx, "ai:tenant-quota:"+tenantID.String()+":*").Result()
		if len(keys) > 0 {
			_ = rdb.Del(ctx, keys...).Err()
		}
	})

	processedRepo := govpersist.NewProcessedEventRepository(db)
	usageRepo := govpersist.NewUsageRecordRepository(db)
	uow := govpersist.NewUnitOfWork(db)
	quotaAdapter := govcache.NewQuotaRedisAdapter(rdb, logger)
	calc := govapp.NewPeriodKeyCalculator()
	syncSvc := govapp.NewQuotaSyncService(quotaAdapter, usageRepo, calc, logger)

	consumer := messaging.NewAiUsageEventConsumer(rmq, processedRepo, uow, syncSvc, logger)
	if err := consumer.Start(ctx); err != nil {
		t.Fatalf("consumer start: %v", err)
	}

	// Verify policy audit queue was declared & bound
	auditPub := messaging.NewRabbitMqPolicyAuditPublisher(rmq, logger)
	auditPub.PublishPolicyDecision(ctx, govapp.PolicyAuditEvent{
		DecisionID:      uuid.NewString(),
		TenantID:        tenantID.String(),
		CallerServiceID: "integration-test",
		CapabilityCode:  "route.plan",
		Operation:       "GENERATE",
		Allowed:         true,
		PolicyVersion:   "v1",
		TimestampMillis: ts.UnixMilli(),
	})

	// Publish usage event + duplicate via real RabbitMQ AND hammer HandleUsageEvent concurrently
	usagePub := messaging.NewUsageEventPublisher(rmq, logger)
	evt := messaging.AiUsageEvent{
		EventID:         eventID,
		TenantID:        &tenantID,
		CallerServiceID: "route-planning-agent",
		CapabilityCode:  "route.plan",
		Operation:       shared.AiOperationGenerate,
		Provider:        "GEMINI",
		SlotAlias:       "gemini-3.5-flash-lite",
		InputTokens:     100,
		OutputTokens:    50,
		DurationMs:      42,
		Success:         true,
		Timestamp:       ts,
	}
	usagePub.PublishUsageEvent(ctx, evt)
	usagePub.PublishUsageEvent(ctx, evt) // duplicate over AMQP

	// Also race 10 concurrent in-process HandleUsageEvent calls for the same eventId
	var wg sync.WaitGroup
	for i := 0; i < 10; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			if err := consumer.HandleUsageEvent(ctx, evt); err != nil {
				t.Errorf("concurrent HandleUsageEvent failed: %v", err)
			}
		}()
	}
	wg.Wait()

	// Wait briefly for RabbitMQ consumer deliveries to settle
	deadline := time.Now().Add(5 * time.Second)
	var processedCount int64
	for time.Now().Before(deadline) {
		_ = db.Model(&govdomain.ProcessedEvent{}).Where("event_id = ?", eventID).Count(&processedCount).Error
		if processedCount == 1 {
			break
		}
		time.Sleep(100 * time.Millisecond)
	}
	if processedCount != 1 {
		t.Fatalf("expected exactly 1 row in processed_events, got %d", processedCount)
	}

	var recs []govdomain.UsageRecord
	if err := db.Where("tenant_id = ?", tenantID).Find(&recs).Error; err != nil {
		t.Fatalf("query usage_records: %v", err)
	}
	if len(recs) != 4 {
		t.Fatalf("expected 4 usage_records (REQ/MIN, REQ/DAY, TOK/MIN, TOK/MONTH), got %d", len(recs))
	}
	for _, rec := range recs {
		expected := int64(1)
		if rec.QuotaMetric == govdomain.QuotaMetricTokens {
			expected = 150
		}
		if rec.UsageValue != expected {
			t.Fatalf("usage_record %s/%s expected %d, got %d", rec.QuotaMetric, rec.QuotaPeriod, expected, rec.UsageValue)
		}
	}

	// Verify Redis quota mirrors Postgres
	reqMin := quotaAdapter.GetCurrentUsage(ctx, tenantID, govdomain.QuotaMetricRequests, govdomain.QuotaPeriodMinute, calc.Calculate(govdomain.QuotaPeriodMinute, ts))
	tokMin := quotaAdapter.GetCurrentUsage(ctx, tenantID, govdomain.QuotaMetricTokens, govdomain.QuotaPeriodMinute, calc.Calculate(govdomain.QuotaPeriodMinute, ts))
	if reqMin != 1 || tokMin != 150 {
		t.Fatalf("expected Redis quota req=1 tok=150, got req=%d tok=%d", reqMin, tokMin)
	}

	// Verify policy audit queue received the audit message
	rmqConn, err := amqp.Dial("amqp://aurora_admin:aurora_secure_password_2026!@localhost:5672/")
	if err == nil {
		defer rmqConn.Close()
		ch, _ := rmqConn.Channel()
		if ch != nil {
			defer ch.Close()
			d, ok, _ := ch.Get(messaging.QueuePolicyAudit, true)
			if !ok || len(d.Body) == 0 {
				t.Fatalf("expected policy audit message in %s", messaging.QueuePolicyAudit)
			}
		}
	}
}

func TestIntegration_AiGovernance_ExecutionFailoverCooldownAndErrors(t *testing.T) {
	requireIntegration(t)
	db, rdb, rmq := connectInfra(t)
	logger := slog.New(slog.NewTextHandler(io.Discard, nil))
	ctx := context.Background()

	poolID := uuid.New()
	poolCode := "test-pool-" + uuid.NewString()[:8]
	slot429ID := uuid.New()
	slot500ID := uuid.New()
	slotOkID := uuid.New()
	slot429Alias := "slot-429-" + uuid.NewString()[:6]
	slot500Alias := "slot-500-" + uuid.NewString()[:6]
	slotOkAlias := "slot-ok-" + uuid.NewString()[:6]

	now := time.Now().UTC()
	if err := db.Create(&gwdomain.ProviderPool{ID: poolID, Code: poolCode, Name: "Test Pool", CreatedAt: now, CreatedBy: "test", Version: 1}).Error; err != nil {
		t.Fatalf("create pool: %v", err)
	}
	slots := []gwdomain.ProviderSlot{
		{ID: slot429ID, PoolID: poolID, Provider: govdomain.AiProviderAzureOpenAI, Operation: shared.AiOperationGenerate, SlotAlias: slot429Alias, SecretRef: "k1", ModelName: "deploy-429", RpmLimit: 20, TpmLimit: 100000, RpdLimit: 1000, Priority: 1, Weight: 1, Enabled: true, CreatedAt: now, Version: 1},
		{ID: slot500ID, PoolID: poolID, Provider: govdomain.AiProviderGemini, Operation: shared.AiOperationGenerate, SlotAlias: slot500Alias, SecretRef: "k2", ModelName: "model-500", RpmLimit: 20, TpmLimit: 100000, RpdLimit: 1000, Priority: 2, Weight: 1, Enabled: true, CreatedAt: now, Version: 1},
		{ID: slotOkID, PoolID: poolID, Provider: govdomain.AiProviderGemini, Operation: shared.AiOperationGenerate, SlotAlias: slotOkAlias, SecretRef: "k3", ModelName: "model-ok", RpmLimit: 20, TpmLimit: 100000, RpdLimit: 1000, Priority: 3, Weight: 1, Enabled: true, CreatedAt: now, Version: 1},
	}
	for i := range slots {
		if err := db.Create(&slots[i]).Error; err != nil {
			t.Fatalf("create slot: %v", err)
		}
	}
	t.Cleanup(func() {
		_ = db.Exec("DELETE FROM provider_slots WHERE pool_id = ?", poolID).Error
		_ = db.Exec("DELETE FROM provider_pools WHERE id = ?", poolID).Error
		for _, a := range []string{slot429Alias, slot500Alias, slotOkAlias} {
			keys, _ := rdb.Keys(ctx, "ai:capacity:*:"+a+":*").Result()
			if len(keys) > 0 {
				_ = rdb.Del(ctx, keys...).Err()
			}
		}
	})

	// Mock HTTP endpoints for Azure 429, Gemini 500, Gemini 200
	providerSrv := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		switch {
		case strings.Contains(r.URL.Path, "deploy-429"):
			w.WriteHeader(http.StatusTooManyRequests)
			_, _ = w.Write([]byte(`{"error":"rate limit"}`))
		case strings.Contains(r.URL.Path, "model-500"):
			w.WriteHeader(http.StatusInternalServerError)
			_, _ = w.Write([]byte(`{"error":"internal failure"}`))
		case strings.Contains(r.URL.Path, "model-ok"):
			w.WriteHeader(http.StatusOK)
			_, _ = w.Write([]byte(`{"candidates":[{"content":{"parts":[{"text":"{\"answer\":\"ok\"}"}]}}],"usageMetadata":{"promptTokenCount":40,"candidatesTokenCount":15}}`))
		default:
			w.WriteHeader(http.StatusBadRequest)
		}
	}))
	defer providerSrv.Close()

	slotRepo := gwpersist.NewProviderSlotRepository(db)
	poolPolicyRepo := gwpersist.NewServiceProviderPoolPolicyRepository(db)
	winPolicy := provider.NewUtcRateWindowPolicy()
	capAdapter := gwcache.NewRedisCapacityAdapter(rdb, map[govdomain.AiProvider]gwapp.ProviderRateWindowPolicy{
		govdomain.AiProviderGemini:      winPolicy,
		govdomain.AiProviderAzureOpenAI: winPolicy,
	}, logger)
	capSvc := gwapp.NewCapacityReservationService(capAdapter, gwapp.NewProviderCapacityLimitPolicy(0.20))
	routing := gwapp.NewProviderRoutingService(slotRepo, poolPolicyRepo, capSvc, logger)

	cred := staticCredPort{key: "real-live-test-key"}
	execSvc := gwapp.NewAiExecutionService(routing, capSvc, map[govdomain.AiProvider]gwapp.AiProviderClient{
		govdomain.AiProviderAzureOpenAI: provider.NewAzureOpenAiProviderClient(cred, providerSrv.URL, logger),
		govdomain.AiProviderGemini:      provider.NewGeminiProviderClient(cred, providerSrv.URL, logger),
	}, slotRepo, logger)

	decision := govdomain.GovernanceDecision{
		Allowed:              true,
		DecisionID:           uuid.NewString(),
		AllowedProviders:     map[govdomain.AiProvider]struct{}{govdomain.AiProviderAzureOpenAI: {}, govdomain.AiProviderGemini: {}},
		AllowedProviderPools: map[string]struct{}{poolCode: {}},
	}

	res, err := execSvc.Generate(ctx, decision, gwdomain.AiGenerateRequest{
		CapabilityCode:       "compliance.answer",
		Prompt:               "test",
		EstimatedInputTokens: 100,
		MaxOutputTokens:      200,
	}, "unmapped-service", shared.NewTokenBudget(100, 200))
	if err != nil {
		t.Fatalf("expected failover to succeed on model-ok, got %v", err)
	}
	if !strings.Contains(res.Content, `"answer":"ok"`) || res.InputTokens != 40 || res.OutputTokens != 15 {
		t.Fatalf("unexpected execution result: %+v", res)
	}

	// Verify 429 slot got cooldown_until persisted in Postgres and reservation released in Redis
	var updated429 gwdomain.ProviderSlot
	if err := db.First(&updated429, "id = ?", slot429ID).Error; err != nil {
		t.Fatalf("query slot429: %v", err)
	}
	if updated429.CooldownUntil == nil || !updated429.CooldownUntil.After(time.Now().UTC()) {
		t.Fatalf("expected cooldown_until set in Postgres for 429 slot, got %v", updated429.CooldownUntil)
	}
	if cap429 := capSvc.GetSlotCapacity(ctx, &slots[0]); cap429.CurrentRpm != 0 || cap429.CurrentTpm != 0 {
		t.Fatalf("expected 429 slot capacity released in Redis, got %+v", cap429)
	}

	// Verify 500 slot did NOT release reservation (uncertain state)
	if cap500 := capSvc.GetSlotCapacity(ctx, &slots[1]); cap500.CurrentRpm != 1 || cap500.CurrentTpm != 300 {
		t.Fatalf("expected 500 slot reservation kept in Redis (rpm=1, tpm=300), got %+v", cap500)
	}

	// Verify ok slot reconciled TPM to actual 40+15=55
	if capOk := capSvc.GetSlotCapacity(ctx, &slots[2]); capOk.CurrentRpm != 1 || capOk.CurrentTpm != 55 {
		t.Fatalf("expected ok slot reconciled TPM=55, got %+v", capOk)
	}

	// Verify unused var sanity
	_ = orchestration.NewExecuteAiService(nil, execSvc, nil, messaging.NewUsageEventPublisher(rmq, logger), metrics.NewMetrics(prometheus.NewRegistry()))
}

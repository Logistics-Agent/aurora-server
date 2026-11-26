package integration_test

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"log/slog"
	"net"
	"net/http"
	"net/http/httptest"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strings"
	"sync"
	"sync/atomic"
	"testing"
	"time"

	"aurora/devops-agent/internal/application"
	"aurora/devops-agent/internal/domain"
	"aurora/devops-agent/internal/infrastructure/ai"
	"aurora/devops-agent/internal/infrastructure/metrics"
	pg "aurora/devops-agent/internal/infrastructure/persistence/postgres"
	"aurora/devops-agent/internal/infrastructure/rag"
	"aurora/devops-agent/internal/infrastructure/security"
	"aurora/devops-agent/internal/infrastructure/workers"
	httphandler "aurora/devops-agent/internal/transport/http"
	"aurora/devops-agent/proto/aigovpb"
	"aurora/shared/rabbitmq"

	"github.com/google/uuid"
	"github.com/prometheus/client_golang/prometheus"
	amqp "github.com/rabbitmq/amqp091-go"
	"github.com/redis/go-redis/v9"
	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
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

func connectDevOpsInfra(t *testing.T) (*gorm.DB, *gorm.DB, *redis.Client, *rabbitmq.Client) {
	t.Helper()
	devopsDSN := "host=localhost user=aurora_admin password=aurora_secure_password_2026! dbname=devops_agent_db port=5433 sslmode=disable TimeZone=UTC"
	devopsDB, err := gorm.Open(postgres.Open(devopsDSN), &gorm.Config{Logger: gormlogger.Default.LogMode(gormlogger.Silent)})
	if err != nil {
		t.Fatalf("connect devops_agent_db: %v", err)
	}

	aiGovDSN := "host=localhost user=aurora_admin password=aurora_secure_password_2026! dbname=ai_governance_db port=5433 sslmode=disable TimeZone=UTC"
	aiGovDB, err := gorm.Open(postgres.Open(aiGovDSN), &gorm.Config{Logger: gormlogger.Default.LogMode(gormlogger.Silent)})
	if err != nil {
		t.Fatalf("connect ai_governance_db: %v", err)
	}

	rdb := redis.NewClient(&redis.Options{Addr: "localhost:6379"})
	if err := rdb.Ping(context.Background()).Err(); err != nil {
		t.Fatalf("ping redis: %v", err)
	}

	logger := slog.New(slog.NewTextHandler(io.Discard, nil))
	rmq, err := rabbitmq.NewClient("amqp://aurora_admin:aurora_secure_password_2026!@localhost:5672/", logger)
	if err != nil {
		t.Fatalf("connect rabbitmq: %v", err)
	}

	t.Cleanup(func() {
		_ = rmq.Close()
		_ = rdb.Close()
		if s, err := devopsDB.DB(); err == nil && s != nil {
			_ = s.Close()
		}
		if s, err := aiGovDB.DB(); err == nil && s != nil {
			_ = s.Close()
		}
	})
	return devopsDB, aiGovDB, rdb, rmq
}

func TestIntegration_DevOps_ConcurrentWebhookDedupRedactionAndAntiFlap(t *testing.T) {
	requireIntegration(t)
	db, _, rdb, _ := connectDevOpsInfra(t)
	logger := slog.New(slog.NewTextHandler(io.Discard, nil))
	ctx := context.Background()

	tag := uuid.NewString()[:8]
	alertName := "DatabaseTimeout_" + tag
	sig := alertName + ":orders:prod"

	t.Cleanup(func() {
		_ = db.Exec("DELETE FROM audit_event_outbox WHERE incident_id IN (SELECT id FROM incidents WHERE error_signature LIKE ?)", "%"+tag+"%").Error
		_ = db.Exec("DELETE FROM incidents WHERE error_signature LIKE ?", "%"+tag+"%").Error
	})

	uow := pg.NewUnitOfWork(db)
	incRepo := pg.NewIncidentRepository(db)
	outboxRepo := pg.NewOutboxRepository(db)
	dedup := application.NewDedupService(rdb, logger)
	antiFlap := application.NewAntiFlappingTracker(rdb, logger)
	m := metrics.NewDevOpsMetrics(prometheus.NewRegistry())

	ingest := application.NewIngestAlertHandler(
		incRepo, outboxRepo, uow, dedup, antiFlap,
		application.NewSeverityClassifier(), security.NewRedactionService(), m, logger,
	)

	srv := httptest.NewServer(httphandler.NewHTTPHandler(ingest, "int-webhook-token", func() bool { return true }))
	defer srv.Close()

	payload := map[string]any{
		"alerts": []map[string]any{
			{
				"status": "firing",
				"labels": map[string]string{
					"alertname":   alertName,
					"service":     "orders",
					"environment": "prod",
				},
				"annotations": map[string]string{
					"description": "Bearer secret-jwt-xyz Server=db.internal;Password=TopSecret123; contact ops@aurora.io",
				},
			},
		},
	}
	rawPayload, _ := json.Marshal(payload)

	// 20 concurrent identical alerts
	const n = 20
	var wg sync.WaitGroup
	var nonDupCount, dupCount atomic.Int32
	corrIDs := make(chan string, n)

	for i := 0; i < n; i++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			req, _ := http.NewRequest(http.MethodPost, srv.URL+"/api/v1/alerts/prometheus", bytes.NewReader(rawPayload))
			req.Header.Set("Authorization", "Bearer int-webhook-token")
			req.Header.Set("Content-Type", "application/json")
			resp, err := http.DefaultClient.Do(req)
			if err != nil {
				t.Errorf("webhook request failed: %v", err)
				return
			}
			defer resp.Body.Close()
			if resp.StatusCode != http.StatusAccepted {
				t.Errorf("expected 202, got %d", resp.StatusCode)
				return
			}
			var res []application.IngestAlertResult
			if err := json.NewDecoder(resp.Body).Decode(&res); err != nil || len(res) != 1 {
				t.Errorf("unexpected response: err=%v res=%+v", err, res)
				return
			}
			corrIDs <- res[0].CorrelationID
			if res[0].Duplicated {
				dupCount.Add(1)
			} else {
				nonDupCount.Add(1)
			}
		}()
	}
	wg.Wait()
	close(corrIDs)

	if nonDupCount.Load() != 1 || dupCount.Load() != 19 {
		t.Fatalf("expected 1 new + 19 duplicates under 20 concurrent alerts, got new=%d dup=%d", nonDupCount.Load(), dupCount.Load())
	}

	var firstCorr string
	for c := range corrIDs {
		if firstCorr == "" {
			firstCorr = c
		} else if c != firstCorr {
			t.Fatalf("expected identical correlationId across duplicates, got %s vs %s", firstCorr, c)
		}
	}

	var inc domain.Incident
	if err := db.First(&inc, "correlation_id = ?", firstCorr).Error; err != nil {
		t.Fatalf("query persisted incident: %v", err)
	}
	if strings.Contains(inc.EvidenceJson, "TopSecret123") || strings.Contains(inc.EvidenceJson, "ops@aurora.io") {
		t.Fatalf("raw secrets leaked into persisted evidence_json: %s", inc.EvidenceJson)
	}

	var outboxCount int64
	_ = db.Model(&domain.AuditEventOutbox{}).Where("incident_id = ?", inc.ID).Count(&outboxCount).Error
	if outboxCount != 2 {
		t.Fatalf("expected 2 outbox events (INCIDENT_CREATED, INCIDENT_DETECTED), got %d", outboxCount)
	}

	// Anti-flapping: 5 occurrences of unique Low signatures -> 5th returns true and escalates
	flapKey := "flap-test-" + tag
	for i := 1; i <= 4; i++ {
		flapping, err := antiFlap.RecordEventAndCheckFlapping(ctx, flapKey)
		if err != nil || flapping {
			t.Fatalf("attempt %d should not flap, err=%v flapping=%v", i, err, flapping)
		}
	}
	flapping, err := antiFlap.RecordEventAndCheckFlapping(ctx, flapKey)
	if err != nil || !flapping {
		t.Fatalf("5th occurrence should trigger flapping, err=%v flapping=%v", err, flapping)
	}

	// Clean up Redis dedup/flap keys
	_ = rdb.Del(ctx, "devops:dedup:"+dedup.ComputeDedupKey("prometheus", sig, 0)).Err()
}

func freeTCPPort(t *testing.T) string {
	t.Helper()
	l, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		t.Fatalf("freeTCPPort: %v", err)
	}
	addr := l.Addr().(*net.TCPAddr)
	_ = l.Close()
	return fmt.Sprintf("%d", addr.Port)
}

func startAiGovernanceProcess(t *testing.T) (grpcAddr string, stopFn func()) {
	t.Helper()
	binName := "ai-gov-test"
	if runtime.GOOS == "windows" {
		binName += ".exe"
	}
	binPath := filepath.Join(t.TempDir(), binName)
	aiGovDir, err := filepath.Abs(filepath.Join("..", "..", "..", "ai-governance"))
	if err != nil {
		t.Fatalf("resolve ai-governance dir: %v", err)
	}

	buildCmd := exec.Command("go", "build", "-o", binPath, "./cmd/server")
	buildCmd.Dir = aiGovDir
	if out, err := buildCmd.CombinedOutput(); err != nil {
		t.Fatalf("build ai-governance binary: %v\n%s", err, string(out))
	}

	grpcPort := freeTCPPort(t)
	httpPort := freeTCPPort(t)

	cmd := exec.Command(binPath)
	cmd.Env = append(os.Environ(),
		"GRPC_PORT="+grpcPort,
		"HTTP_PORT="+httpPort,
		"POSTGRES_DSN=host=localhost user=aurora_admin password=aurora_secure_password_2026! dbname=ai_governance_db port=5433 sslmode=disable TimeZone=UTC",
		"REDIS_ADDR=localhost:6379",
		"RABBITMQ_URI=amqp://aurora_admin:aurora_secure_password_2026!@localhost:5672/",
	)
	if err := cmd.Start(); err != nil {
		t.Fatalf("start ai-governance binary: %v", err)
	}

	stopped := false
	stop := func() {
		if stopped {
			return
		}
		stopped = true
		if cmd.Process != nil {
			_ = cmd.Process.Kill()
			_, _ = cmd.Process.Wait()
		}
	}
	t.Cleanup(stop)

	healthURL := fmt.Sprintf("http://127.0.0.1:%s/actuator/health", httpPort)
	deadline := time.Now().Add(20 * time.Second)
	for time.Now().Before(deadline) {
		resp, err := http.Get(healthURL)
		if err == nil {
			_ = resp.Body.Close()
			if resp.StatusCode == http.StatusOK {
				return "127.0.0.1:" + grpcPort, stop
			}
		}
		time.Sleep(200 * time.Millisecond)
	}
	stop()
	t.Fatalf("ai-governance binary did not become healthy at %s", healthURL)
	return "", stop
}

func TestIntegration_CrossService_DevOpsToAiGovernanceAndWorkersAndOutbox(t *testing.T) {
	requireIntegration(t)
	devopsDB, _, rdb, rmq := connectDevOpsInfra(t)
	logger := slog.New(slog.NewTextHandler(io.Discard, nil))
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	// 1. Start real AI Governance microservice process backed by real ai_governance_db + Redis + RabbitMQ
	govAddr, stopGov := startAiGovernanceProcess(t)

	// 2. Wire DevOps Agent gRPC client -> live AI Governance server
	conn, err := grpc.NewClient(govAddr, grpc.WithTransportCredentials(insecure.NewCredentials()))
	if err != nil {
		t.Fatalf("dial ai-governance: %v", err)
	}
	defer conn.Close()

	uow := pg.NewUnitOfWork(devopsDB)
	incRepo := pg.NewIncidentRepository(devopsDB)
	rcaRepo := pg.NewRcaAnalysisRepository(devopsDB)
	outboxRepo := pg.NewOutboxRepository(devopsDB)
	approvalRepo := pg.NewPrApprovalRecordRepository(devopsDB)
	m := metrics.NewDevOpsMetrics(prometheus.NewRegistry())

	aiClient := ai.NewGrpcAiGovernanceClient(aigovpb.NewAiExecutionServiceClient(conn), logger)
	ragClient := rag.NewDevOpsRagClient(nil, logger)
	rcaOrch := application.NewRcaOrchestratorService(incRepo, rcaRepo, security.NewRedactionService(), ragClient, aiClient, outboxRepo, m, logger)

	tag := uuid.NewString()[:8]
	// Insert 6 NEW High-severity incidents and 5 expired PR approvals
	incIDs := make([]uuid.UUID, 6)
	corrIDs := make([]string, 6)
	now := time.Now().UTC()
	for i := 0; i < 6; i++ {
		incIDs[i] = uuid.New()
		corrIDs[i] = "corr-int-" + tag + "-" + string(rune('a'+i))
		inc := &domain.Incident{
			BaseAuditable:    domain.BaseAuditable{ID: incIDs[i], CreatedAt: now, UpdatedAt: now},
			CorrelationID:    corrIDs[i],
			DedupKey:         "dedup-" + corrIDs[i],
			Source:           "prometheus",
			ErrorSignature:   "DatabaseTimeout_" + tag,
			EvidenceJson:     `{"msg":"timeout"}`,
			Severity:         domain.SeverityHigh,
			OriginalSeverity: domain.SeverityHigh,
			Status:           domain.IncidentStatusNew,
			AffectedService:  "orders",
			ImpactScore:      75.0,
		}
		if err := devopsDB.Create(inc).Error; err != nil {
			t.Fatalf("seed incident: %v", err)
		}
	}

	approvalIDs := make([]uuid.UUID, 5)
	for i := 0; i < 5; i++ {
		approvalIDs[i] = uuid.New()
		rec := &domain.PrApprovalRecord{
			BaseAuditable:    domain.BaseAuditable{ID: approvalIDs[i], CreatedAt: now, UpdatedAt: now},
			IncidentID:       incIDs[0],
			RecommendationID: uuid.New(),
			Status:           domain.ApprovalStatusPending,
			Stage:            domain.ApprovalStageMerge,
			Severity:         domain.SeverityHigh,
			TimeoutMinutes:   30,
			ExpiresAt:        now.Add(-5 * time.Minute),
		}
		if err := devopsDB.Create(rec).Error; err != nil {
			t.Fatalf("seed approval: %v", err)
		}
	}

	t.Cleanup(func() {
		_ = devopsDB.Exec("DELETE FROM pr_approval_records WHERE id IN ?", approvalIDs).Error
		_ = devopsDB.Exec("DELETE FROM rca_analyses WHERE incident_id IN ?", incIDs).Error
		_ = devopsDB.Exec("DELETE FROM audit_event_outbox WHERE incident_id IN ?", incIDs).Error
		_ = devopsDB.Exec("DELETE FROM incidents WHERE id IN ?", incIDs).Error
		keys, _ := rdb.Keys(ctx, "ai:capacity:*").Result()
		if len(keys) > 0 {
			_ = rdb.Del(ctx, keys...).Err()
		}
	})

	// 3. Run 4 concurrent RcaAnalysisWorkers claiming batchSize=1 across 6 NEW incidents
	var rcaWg sync.WaitGroup
	for w := 0; w < 4; w++ {
		worker := workers.NewRcaAnalysisWorker(incRepo, outboxRepo, uow, rcaOrch, m, time.Second, 1, logger)
		rcaWg.Add(1)
		go func() {
			defer rcaWg.Done()
			for round := 0; round < 4; round++ {
				_ = worker.ProcessPendingIncidents(ctx)
			}
		}()
	}
	rcaWg.Wait()

	var rcaCount int64
	_ = devopsDB.Model(&domain.RcaAnalysis{}).Where("incident_id IN ?", incIDs).Count(&rcaCount).Error
	if rcaCount != 6 {
		t.Fatalf("expected exactly 6 RCA analyses (no double processing), got %d", rcaCount)
	}

	var sampleRca domain.RcaAnalysis
	if err := devopsDB.First(&sampleRca, "incident_id = ?", incIDs[0]).Error; err != nil {
		t.Fatalf("query sample rca: %v", err)
	}
	if sampleRca.GovernanceDecisionID == "" || sampleRca.RagAugmented || sampleRca.InputTokens <= 0 {
		t.Fatalf("unexpected RCA governance metadata: %+v", sampleRca)
	}

	var updatedInc domain.Incident
	_ = devopsDB.First(&updatedInc, "id = ?", incIDs[0]).Error
	if updatedInc.Status != domain.IncidentStatusWaitingApproval {
		t.Fatalf("expected WAITING_APPROVAL after RCA, got %s", updatedInc.Status)
	}

	// 4. Run 4 concurrent ApprovalTimeoutWorkers on 5 expired approvals
	var appWg sync.WaitGroup
	for w := 0; w < 4; w++ {
		appWorker := workers.NewApprovalTimeoutWorker(approvalRepo, outboxRepo, uow, m, time.Second, logger)
		appWg.Add(1)
		go func() {
			defer appWg.Done()
			_ = appWorker.ProcessExpiredApprovals(ctx)
		}()
	}
	appWg.Wait()

	var expiredCount int64
	_ = devopsDB.Model(&domain.PrApprovalRecord{}).Where("id IN ? AND status = ?", approvalIDs, domain.ApprovalStatusExpired).Count(&expiredCount).Error
	if expiredCount != 5 {
		t.Fatalf("expected 5 expired approvals, got %d", expiredCount)
	}
	var rejOutboxCount int64
	_ = devopsDB.Model(&domain.AuditEventOutbox{}).Where("incident_id = ? AND action_type = ?", incIDs[0], domain.AuditActionApprovalRejected).Count(&rejOutboxCount).Error
	if rejOutboxCount != 5 {
		t.Fatalf("expected exactly 5 APPROVAL_REJECTED outbox rows (no double-expiry), got %d", rejOutboxCount)
	}

	// 5. Real OutboxPollerWorker -> RabbitMQ publishing & consumer verification
	testAuditQueue := "test.devops.audit." + tag
	if err := rmq.DeclareTopicQueue(testAuditQueue, workers.AuditExchange, "devops.audit.#"); err != nil {
		t.Fatalf("declare test audit queue: %v", err)
	}

	outboxWorker := workers.NewOutboxPollerWorker(outboxRepo, uow, rmq, m, time.Second, logger)
	if err := outboxWorker.ProcessOutboxEvents(ctx); err != nil {
		t.Fatalf("outbox poller failed: %v", err)
	}

	var unprocessed int64
	_ = devopsDB.Model(&domain.AuditEventOutbox{}).Where("incident_id IN ? AND processed = false", incIDs).Count(&unprocessed).Error
	if unprocessed != 0 {
		t.Fatalf("expected 0 unprocessed outbox rows after poller, got %d", unprocessed)
	}

	amqpConn, err := amqp.Dial("amqp://aurora_admin:aurora_secure_password_2026!@localhost:5672/")
	if err != nil {
		t.Fatalf("amqp dial verify: %v", err)
	}
	defer amqpConn.Close()
	ch, _ := amqpConn.Channel()
	defer ch.Close()
	d, ok, err := ch.Get(testAuditQueue, true)
	if err != nil || !ok || len(d.Body) == 0 {
		t.Fatalf("expected published audit message in RabbitMQ, ok=%v err=%v", ok, err)
	}
	_, _ = ch.QueueDelete(testAuditQueue, false, false, false)

	// 6. Failure mode: Redis down -> IngestAlert fails closed
	downRedis := redis.NewClient(&redis.Options{Addr: "127.0.0.1:59999"})
	defer downRedis.Close()
	failIngest := application.NewIngestAlertHandler(
		incRepo, outboxRepo, uow, application.NewDedupService(downRedis, logger), nil,
		application.NewSeverityClassifier(), security.NewRedactionService(), m, logger,
	)
	if _, err := failIngest.Handle(ctx, application.IngestAlertCommand{
		Source: "prometheus", ErrorSignature: "RedisDownTest_" + tag, PayloadJson: "{}",
	}); err == nil {
		t.Fatalf("expected IngestAlert to fail closed when Redis is unavailable")
	}

	// 7. Failure mode: AI Governance down -> RcaAnalysisWorker marks AI_ANALYSIS_FAILED (not stuck in NEW)
	stopGov()
	failIncID := uuid.New()
	failCorr := "corr-fail-" + tag
	_ = devopsDB.Create(&domain.Incident{
		BaseAuditable:    domain.BaseAuditable{ID: failIncID, CreatedAt: now, UpdatedAt: now},
		CorrelationID:    failCorr,
		DedupKey:         "dedup-" + failCorr,
		Source:           "prometheus",
		ErrorSignature:   "ServiceUnavailable_" + tag,
		EvidenceJson:     "{}",
		Severity:         domain.SeverityHigh,
		OriginalSeverity: domain.SeverityHigh,
		Status:           domain.IncidentStatusNew,
	}).Error
	t.Cleanup(func() {
		_ = devopsDB.Exec("DELETE FROM audit_event_outbox WHERE incident_id = ?", failIncID).Error
		_ = devopsDB.Exec("DELETE FROM incidents WHERE id = ?", failIncID).Error
	})

	failWorker := workers.NewRcaAnalysisWorker(incRepo, outboxRepo, uow, rcaOrch, m, time.Second, 1, logger)
	_ = failWorker.ProcessPendingIncidents(ctx)

	var failedInc domain.Incident
	_ = devopsDB.First(&failedInc, "id = ?", failIncID).Error
	if failedInc.Status != domain.IncidentStatusAiAnalysisFailed {
		t.Fatalf("expected AI_ANALYSIS_FAILED when AI Governance is down, got %s", failedInc.Status)
	}
}

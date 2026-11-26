package application_test

import (
	"context"
	"io"
	"log/slog"
	"strings"
	"testing"

	"aurora/devops-agent/internal/application"
	"aurora/devops-agent/internal/domain"
	"aurora/devops-agent/internal/infrastructure/metrics"
	"aurora/devops-agent/internal/infrastructure/security"
	sharedctx "aurora/shared/context"

	"github.com/google/uuid"
	"github.com/prometheus/client_golang/prometheus"
)

type memIncidentRepo struct {
	byCorr map[string]*domain.Incident
}

func newMemIncidentRepo() *memIncidentRepo {
	return &memIncidentRepo{byCorr: make(map[string]*domain.Incident)}
}
func (r *memIncidentRepo) Save(_ context.Context, inc *domain.Incident) error {
	r.byCorr[inc.CorrelationID] = inc
	return nil
}
func (r *memIncidentRepo) FindByCorrelationID(_ context.Context, corrID string) (*domain.Incident, error) {
	return r.byCorr[corrID], nil
}
func (r *memIncidentRepo) List(_ context.Context, _ string, _, _ int32) ([]*domain.Incident, int64, error) {
	return nil, 0, nil
}
func (r *memIncidentRepo) FindAndLockByStatus(_ context.Context, _ domain.IncidentStatus, _ int) ([]*domain.Incident, error) {
	return nil, nil
}

type memRcaRepo struct{ saved []*domain.RcaAnalysis }

func (r *memRcaRepo) Save(_ context.Context, a *domain.RcaAnalysis) error {
	r.saved = append(r.saved, a)
	return nil
}

type memOutboxRepo struct{ actions []domain.AuditActionType }

func (r *memOutboxRepo) Enqueue(_ context.Context, _ string, _ *uuid.UUID, action domain.AuditActionType, _, _ string) error {
	r.actions = append(r.actions, action)
	return nil
}
func (r *memOutboxRepo) FindAndLockPendingBatch(_ context.Context, _ int) ([]*domain.AuditEventOutbox, error) {
	return nil, nil
}
func (r *memOutboxRepo) Save(_ context.Context, _ *domain.AuditEventOutbox) error { return nil }

type noopUoW struct{}

func (noopUoW) WithTransaction(ctx context.Context, fn func(txCtx context.Context) error) error {
	return fn(ctx)
}

type memDedupStore struct {
	seen map[string]string
}

func (m *memDedupStore) ComputeDedupKey(_, sig string, _ int64) string {
	return strings.ToLower(strings.TrimSpace(sig))
}
func (m *memDedupStore) CheckAndStore(_ context.Context, key, corrID string) (string, bool, error) {
	if existing, ok := m.seen[key]; ok {
		return existing, true, nil
	}
	m.seen[key] = corrID
	return "", false, nil
}

type stubAntiFlap struct{ flapping bool }

func (s *stubAntiFlap) RecordEventAndCheckFlapping(_ context.Context, _ string) (bool, error) {
	return s.flapping, nil
}

type stubRag struct{ res application.RagRetrievalResult }

func (s *stubRag) QueryKnowledge(_ context.Context, _ domain.RedactedIncidentContext) application.RagRetrievalResult {
	return s.res
}

type stubAiGov struct {
	calls int
	res   application.GenerateResult
}

func (s *stubAiGov) Generate(_ context.Context, _ application.GenerateCommand) (application.GenerateResult, error) {
	s.calls++
	return s.res, nil
}

func TestSeverityClassifier(t *testing.T) {
	c := application.NewSeverityClassifier()
	r1 := c.Classify("prometheus", "PodCrashLoopOOM", "orders", "production")
	if r1.Severity != domain.SeverityCritical || r1.ImpactScore != 95.0 {
		t.Fatalf("expected Critical/95, got %+v", r1)
	}
	r2 := c.Classify("prometheus", "DatabaseTimeout", "orders", "staging")
	if r2.Severity != domain.SeverityHigh || r2.ImpactScore != 65.0 {
		t.Fatalf("expected High/65, got %+v", r2)
	}
}

func TestIngestAlertHandler_DedupAndAntiFlap(t *testing.T) {
	reg := prometheus.NewRegistry()
	m := metrics.NewDevOpsMetrics(reg)
	logger := slog.New(slog.NewTextHandler(io.Discard, nil))
	incRepo := newMemIncidentRepo()
	outbox := &memOutboxRepo{}
	dedup := &memDedupStore{seen: make(map[string]string)}

	handler := application.NewIngestAlertHandler(
		incRepo, outbox, noopUoW{}, dedup, &stubAntiFlap{flapping: true},
		application.NewSeverityClassifier(), security.NewRedactionService(), m, logger,
	)

	// 1. Low signature with flapping -> escalated to Medium
	res1, err := handler.Handle(context.Background(), application.IngestAlertCommand{
		Source:          "prometheus",
		ErrorSignature:  "MinorNotice:billing:prod",
		PayloadJson:     `{"email":"ops@aurora.io"}`,
		AffectedService: "billing",
		Environment:     "prod",
	})
	if err != nil || res1.Duplicated {
		t.Fatalf("unexpected ingest result: err=%v res=%+v", err, res1)
	}
	inc := incRepo.byCorr[res1.CorrelationID]
	if inc.Severity != domain.SeverityMedium || inc.OriginalSeverity != domain.SeverityLow {
		t.Fatalf("expected flapping escalation Low->Medium, got %s (orig %s)", inc.Severity, inc.OriginalSeverity)
	}
	if strings.Contains(inc.EvidenceJson, "ops@aurora.io") {
		t.Fatalf("expected evidence to be redacted: %s", inc.EvidenceJson)
	}
	if len(outbox.actions) != 2 {
		t.Fatalf("expected 2 outbox events (INCIDENT_CREATED, INCIDENT_DETECTED), got %v", outbox.actions)
	}

	// 2. Duplicate alert
	res2, err := handler.Handle(context.Background(), application.IngestAlertCommand{
		Source:         "loki",
		ErrorSignature: "MinorNotice:billing:prod",
	})
	if err != nil || !res2.Duplicated || res2.CorrelationID != res1.CorrelationID {
		t.Fatalf("expected duplicate with matching correlationId, got %+v", res2)
	}
}

func TestRcaOrchestrator_LowBypassAndHighGovernanceMetadata(t *testing.T) {
	reg := prometheus.NewRegistry()
	m := metrics.NewDevOpsMetrics(reg)
	logger := slog.New(slog.NewTextHandler(io.Discard, nil))
	incRepo := newMemIncidentRepo()
	rcaRepo := &memRcaRepo{}
	outbox := &memOutboxRepo{}
	aiGov := &stubAiGov{res: application.GenerateResult{
		Content:          "Restart pod; admin@aurora.io",
		InputTokens:      120,
		OutputTokens:     40,
		DecisionID:       "dec-123",
		AutomationLevel:  "SUPERVISED_AUTONOMOUS",
		RequiresApproval: false,
		Model:            "gpt-4o",
		Provider:         "AZURE_OPENAI",
	}}
	rag := &stubRag{res: application.RagRetrievalResult{Success: false, FailureReason: "UNAVAILABLE"}}

	orch := application.NewRcaOrchestratorService(incRepo, rcaRepo, security.NewRedactionService(), rag, aiGov, outbox, m, logger)

	// LOW severity should skip LLM
	lowInc := &domain.Incident{
		BaseAuditable:  domain.BaseAuditable{ID: uuid.New()},
		CorrelationID:  "corr-low",
		Severity:       domain.SeverityLow,
		Status:         domain.IncidentStatusNew,
		ErrorSignature: "MinorNotice",
	}
	if _, err := orch.ExecuteRca(context.Background(), lowInc, "{}"); err != nil {
		t.Fatalf("unexpected error for low severity: %v", err)
	}
	if lowInc.Status != domain.IncidentStatusRuleAnalysis || aiGov.calls != 0 {
		t.Fatalf("expected LOW severity to transition to RULE_ANALYSIS with 0 LLM calls, got status=%s calls=%d", lowInc.Status, aiGov.calls)
	}

	// HIGH severity should execute RCA and wait for approval
	highInc := &domain.Incident{
		BaseAuditable:   domain.BaseAuditable{ID: uuid.New()},
		CorrelationID:   "corr-high",
		Severity:        domain.SeverityHigh,
		Status:          domain.IncidentStatusNew,
		ErrorSignature:  "DatabaseTimeout",
		AffectedService: "orders",
	}
	analysis, err := orch.ExecuteRca(context.Background(), highInc, `{"token":"Bearer secret123"}`)
	if err != nil {
		t.Fatalf("unexpected error for high severity: %v", err)
	}
	if aiGov.calls != 1 || analysis == nil || analysis.GovernanceDecisionID != "dec-123" || analysis.RagAugmented {
		t.Fatalf("unexpected RCA analysis: %+v calls=%d", analysis, aiGov.calls)
	}
	if highInc.Status != domain.IncidentStatusWaitingApproval {
		t.Fatalf("expected WAITING_APPROVAL for High severity, got %s", highInc.Status)
	}
	if strings.Contains(highInc.RcaRecommendation, "admin@aurora.io") {
		t.Fatalf("expected recommendation to be redacted: %s", highInc.RcaRecommendation)
	}
}

type memRuleRepo struct{}

func (memRuleRepo) Save(_ context.Context, _ *domain.ExistingRule) error        { return nil }
func (memRuleRepo) FindByID(_ context.Context, _ uuid.UUID) (*domain.ExistingRule, error) {
	return nil, nil
}
func (memRuleRepo) DeleteByID(_ context.Context, _ uuid.UUID) error                     { return nil }
func (memRuleRepo) List(_ context.Context, _, _ int32) ([]*domain.ExistingRule, int64, error) {
	return nil, 0, nil
}

type memPendingRepo struct{}

func (memPendingRepo) Save(_ context.Context, _ *domain.PendingRule) error { return nil }
func (memPendingRepo) FindByID(_ context.Context, _ uuid.UUID) (*domain.PendingRule, error) {
	return nil, nil
}
func (memPendingRepo) ListByStatus(_ context.Context, _ string, _, _ int32) ([]*domain.PendingRule, int64, error) {
	return nil, 0, nil
}

func TestRuleService_RequiresSystemAdmin(t *testing.T) {
	svc := application.NewRuleService(memRuleRepo{}, memPendingRepo{}, &memOutboxRepo{}, noopUoW{})
	_, err := svc.CreateRule(context.Background(), application.CreateRuleCommand{Name: "r1"})
	if err != application.ErrForbidden {
		t.Fatalf("expected ErrForbidden without SYSTEM_ADMIN, got %v", err)
	}

	adminCtx := sharedctx.WithUserRole(context.Background(), "SYSTEM_ADMIN")
	rule, err := svc.CreateRule(adminCtx, application.CreateRuleCommand{Name: "r1", ErrorSignaturePattern: "OOM", ActionType: "RESTART_POD"})
	if err != nil || rule.RuleName != "r1" {
		t.Fatalf("expected rule creation to succeed for SYSTEM_ADMIN, got err=%v rule=%+v", err, rule)
	}
}

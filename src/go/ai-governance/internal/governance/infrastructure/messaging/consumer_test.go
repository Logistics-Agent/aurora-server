package messaging_test

import (
	"context"
	"encoding/json"
	"io"
	"log/slog"
	"testing"
	"time"

	govapp "aurora/ai-governance/internal/governance/application"
	"aurora/ai-governance/internal/governance/domain"
	"aurora/ai-governance/internal/governance/infrastructure/messaging"
	"aurora/ai-governance/internal/shared"

	"github.com/google/uuid"
)

type stubProcessedRepo struct {
	seen map[string]bool
}

func (s *stubProcessedRepo) ExistsByEventID(_ context.Context, id string) (bool, error) {
	return s.seen[id], nil
}
func (s *stubProcessedRepo) Save(_ context.Context, e *domain.ProcessedEvent) error {
	s.seen[e.EventID] = true
	return nil
}
func (s *stubProcessedRepo) TryInsert(_ context.Context, e *domain.ProcessedEvent) (bool, error) {
	if s.seen[e.EventID] {
		return false, nil
	}
	s.seen[e.EventID] = true
	return true, nil
}

type stubUsageRepo struct {
	calls int
}

func (s *stubUsageRepo) UpsertIncrement(_ context.Context, _ uuid.UUID, _ domain.QuotaMetric, _ domain.QuotaPeriod, _ string, _ int64) error {
	s.calls++
	return nil
}

type stubQuotaStore struct {
	calls int
}

func (s *stubQuotaStore) IncrementUsage(_ context.Context, _ uuid.UUID, _ domain.QuotaMetric, _ domain.QuotaPeriod, _ domain.PeriodKey, _ int64) {
	s.calls++
}

func TestAiUsageEvent_SlotAliasJsonAndIdempotentConsumer(t *testing.T) {
	tid := uuid.New()
	evt := messaging.AiUsageEvent{
		EventID:         "evt-1",
		TenantID:        &tid,
		CallerServiceID: "route-planning-agent",
		CapabilityCode:  "route.plan",
		Operation:       shared.AiOperationGenerate,
		Provider:        "GEMINI",
		SlotAlias:       "gemini-3.5-flash-lite",
		InputTokens:     100,
		OutputTokens:    50,
		Success:         true,
		Timestamp:       time.Now().UTC(),
	}
	raw, err := json.Marshal(evt)
	if err != nil {
		t.Fatalf("marshal error: %v", err)
	}
	var m map[string]any
	_ = json.Unmarshal(raw, &m)
	if _, ok := m["slotAlias"]; !ok {
		t.Fatalf("expected slotAlias JSON field matching Java AiUsageEvent, got %s", string(raw))
	}

	processed := &stubProcessedRepo{seen: make(map[string]bool)}
	usageRepo := &stubUsageRepo{}
	quotaStore := &stubQuotaStore{}
	syncSvc := govapp.NewQuotaSyncService(quotaStore, usageRepo, govapp.NewPeriodKeyCalculator(), slog.New(slog.NewTextHandler(io.Discard, nil)))
	consumer := messaging.NewAiUsageEventConsumer(nil, processed, nil, syncSvc, slog.New(slog.NewTextHandler(io.Discard, nil)))

	if err := consumer.HandleUsageEvent(context.Background(), evt); err != nil {
		t.Fatalf("unexpected error on first event: %v", err)
	}
	if err := consumer.HandleUsageEvent(context.Background(), evt); err != nil {
		t.Fatalf("unexpected error on duplicate event: %v", err)
	}
	// First event records 4 dimensions (REQ/MIN, REQ/DAY, TOK/MIN, TOK/MONTH); duplicate must be ignored
	if usageRepo.calls != 4 || quotaStore.calls != 4 {
		t.Fatalf("expected exactly 4 durable and 4 cache increments, got usage=%d cache=%d", usageRepo.calls, quotaStore.calls)
	}
}

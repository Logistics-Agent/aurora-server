package application_test

import (
	"context"
	"errors"
	"io"
	"log/slog"
	"testing"
	"time"

	gwapp "aurora/ai-governance/internal/gateway/application"
	"aurora/ai-governance/internal/gateway/domain"
	govdomain "aurora/ai-governance/internal/governance/domain"
	"aurora/ai-governance/internal/shared"

	"github.com/google/uuid"
)

type stubCapacityPort struct {
	reserveFailAliases map[string]bool
	released           []string
	reconciled         []int64
	capacities         map[string]domain.SlotCapacity
}

func (s *stubCapacityPort) TryReserve(_ context.Context, slot *domain.ProviderSlot, _ domain.ProviderCapacityLimits, reqTokens int64) (*domain.ProviderReservation, error) {
	if s.reserveFailAliases != nil && s.reserveFailAliases[slot.SlotAlias] {
		return nil, nil
	}
	return &domain.ProviderReservation{
		ReservationID:  uuid.NewString(),
		Slot:           slot,
		ReservedTokens: reqTokens,
	}, nil
}

func (s *stubCapacityPort) Reconcile(_ context.Context, _ *domain.ProviderReservation, actualTokens int64) {
	s.reconciled = append(s.reconciled, actualTokens)
}

func (s *stubCapacityPort) Release(_ context.Context, reservation *domain.ProviderReservation) {
	s.released = append(s.released, reservation.Slot.SlotAlias)
}

func (s *stubCapacityPort) GetSlotCapacity(_ context.Context, slot *domain.ProviderSlot) domain.SlotCapacity {
	if s.capacities == nil {
		return domain.SlotCapacity{}
	}
	return s.capacities[slot.SlotAlias]
}

type stubSlotRepo struct {
	slots         []*domain.ProviderSlot
	savedCooldown []string
}

func (s *stubSlotRepo) FindActiveCandidateSlots(_ context.Context, _ []string, _ []govdomain.AiProvider, _ shared.AiOperation) ([]*domain.ProviderSlot, error) {
	return s.slots, nil
}

func (s *stubSlotRepo) SaveCooldown(_ context.Context, slot *domain.ProviderSlot) error {
	s.savedCooldown = append(s.savedCooldown, slot.SlotAlias)
	return nil
}

type stubPoolPolicyRepo struct {
	policies []*domain.ServiceProviderPoolPolicy
}

func (s *stubPoolPolicyRepo) FindByServiceIDOrderByPriorityAsc(_ context.Context, _ string) ([]*domain.ServiceProviderPoolPolicy, error) {
	return s.policies, nil
}

type stubProviderClient struct {
	fn func(slot *domain.ProviderSlot) (domain.AiGenerateResult, error)
}

func (s *stubProviderClient) Generate(_ context.Context, slot *domain.ProviderSlot, _ domain.AiGenerateRequest) (domain.AiGenerateResult, error) {
	return s.fn(slot)
}

func (s *stubProviderClient) Embed(_ context.Context, _ *domain.ProviderSlot, _ domain.AiEmbeddingRequest) (domain.AiEmbeddingResult, error) {
	return domain.AiEmbeddingResult{}, nil
}

func TestProviderCapacityLimitPolicy(t *testing.T) {
	policy := gwapp.NewProviderCapacityLimitPolicy(0.20)
	slot := &domain.ProviderSlot{RpmLimit: 15, TpmLimit: 250000, RpdLimit: 500}
	limits := policy.EffectiveLimits(slot)
	if limits.RpmLimit != 12 || limits.TpmLimit != 200000 || limits.RpdLimit != 400 {
		t.Fatalf("unexpected effective limits: %+v", limits)
	}

	minPolicy := gwapp.NewProviderCapacityLimitPolicy(0.99) // clamped to 0.90
	minSlot := &domain.ProviderSlot{RpmLimit: 1, TpmLimit: 1, RpdLimit: 0}
	minLimits := minPolicy.EffectiveLimits(minSlot)
	if minLimits.RpmLimit != 1 || minLimits.TpmLimit != 1 || minLimits.RpdLimit != 0 {
		t.Fatalf("expected min floor 1 for >0 and 0 for 0, got %+v", minLimits)
	}
}

func TestProviderRoutingService_FiltersCooldownAndRanksByPriorityThenUtil(t *testing.T) {
	future := time.Now().UTC().Add(time.Minute)
	slots := []*domain.ProviderSlot{
		{SlotAlias: "cooldown-slot", Provider: govdomain.AiProviderGemini, Priority: 1, RpmLimit: 100, TpmLimit: 10000, RpdLimit: 1000, CooldownUntil: &future},
		{SlotAlias: "p2-low-util", Provider: govdomain.AiProviderGemini, Priority: 2, RpmLimit: 100, TpmLimit: 10000, RpdLimit: 1000},
		{SlotAlias: "p1-high-util", Provider: govdomain.AiProviderGemini, Priority: 1, RpmLimit: 100, TpmLimit: 10000, RpdLimit: 1000},
		{SlotAlias: "p1-low-util", Provider: govdomain.AiProviderGemini, Priority: 1, RpmLimit: 100, TpmLimit: 10000, RpdLimit: 1000},
	}

	capPort := &stubCapacityPort{
		capacities: map[string]domain.SlotCapacity{
			"p1-high-util": {CurrentRpm: 50},
			"p1-low-util":  {CurrentRpm: 5},
			"p2-low-util":  {CurrentRpm: 0},
		},
	}
	capSvc := gwapp.NewCapacityReservationService(capPort, gwapp.NewProviderCapacityLimitPolicy(0.20))
	routing := gwapp.NewProviderRoutingService(&stubSlotRepo{slots: slots}, &stubPoolPolicyRepo{}, capSvc, slog.New(slog.NewTextHandler(io.Discard, nil)))

	got, err := routing.GetCandidates(
		context.Background(),
		map[govdomain.AiProvider]struct{}{govdomain.AiProviderGemini: {}},
		map[string]struct{}{"shared-ai": {}},
		shared.AiOperationGenerate,
		"route-planning-agent",
		shared.NewTokenBudget(100, 100),
	)
	if err != nil {
		t.Fatalf("unexpected error: %v", err)
	}
	if len(got) != 3 {
		t.Fatalf("expected 3 non-cooldown candidates, got %d", len(got))
	}
	if got[0].SlotAlias != "p1-low-util" || got[1].SlotAlias != "p1-high-util" || got[2].SlotAlias != "p2-low-util" {
		t.Fatalf("unexpected candidate ordering: %s, %s, %s", got[0].SlotAlias, got[1].SlotAlias, got[2].SlotAlias)
	}
}

func TestAiExecutionService_FailoverAndReleaseSemantics(t *testing.T) {
	slot1 := &domain.ProviderSlot{SlotAlias: "slot-429", Provider: govdomain.AiProviderAzureOpenAI, Priority: 1, RpmLimit: 100, TpmLimit: 10000, RpdLimit: 1000}
	slot2 := &domain.ProviderSlot{SlotAlias: "slot-500", Provider: govdomain.AiProviderGemini, Priority: 2, RpmLimit: 100, TpmLimit: 10000, RpdLimit: 1000}
	slot3 := &domain.ProviderSlot{SlotAlias: "slot-ok", Provider: govdomain.AiProviderGemini, Priority: 3, RpmLimit: 100, TpmLimit: 10000, RpdLimit: 1000}

	capPort := &stubCapacityPort{}
	capSvc := gwapp.NewCapacityReservationService(capPort, gwapp.NewProviderCapacityLimitPolicy(0.20))
	slotRepo := &stubSlotRepo{slots: []*domain.ProviderSlot{slot1, slot2, slot3}}
	routing := gwapp.NewProviderRoutingService(slotRepo, &stubPoolPolicyRepo{}, capSvc, slog.New(slog.NewTextHandler(io.Discard, nil)))

	clients := map[govdomain.AiProvider]gwapp.AiProviderClient{
		govdomain.AiProviderAzureOpenAI: &stubProviderClient{fn: func(_ *domain.ProviderSlot) (domain.AiGenerateResult, error) {
			return domain.AiGenerateResult{}, errors.New("HTTP 429 Too Many Requests")
		}},
		govdomain.AiProviderGemini: &stubProviderClient{fn: func(s *domain.ProviderSlot) (domain.AiGenerateResult, error) {
			if s.SlotAlias == "slot-500" {
				return domain.AiGenerateResult{}, errors.New("HTTP 503 upstream timeout")
			}
			return domain.AiGenerateResult{Content: "ok", InputTokens: 20, OutputTokens: 30, Model: "gemini-1.5-flash", Provider: "GEMINI"}, nil
		}},
	}

	execSvc := gwapp.NewAiExecutionService(routing, capSvc, clients, slotRepo, slog.New(slog.NewTextHandler(io.Discard, nil)))
	decision := govdomain.GovernanceDecision{
		Allowed:              true,
		AllowedProviders:     map[govdomain.AiProvider]struct{}{govdomain.AiProviderAzureOpenAI: {}, govdomain.AiProviderGemini: {}},
		AllowedProviderPools: map[string]struct{}{"shared-ai": {}},
	}

	res, err := execSvc.Generate(context.Background(), decision, domain.AiGenerateRequest{CapabilityCode: "devops.rca", Prompt: "test"}, "devops-agent", shared.NewTokenBudget(100, 100))
	if err != nil {
		t.Fatalf("expected failover to succeed on slot-ok, got err: %v", err)
	}
	if res.Content != "ok" {
		t.Fatalf("unexpected content: %s", res.Content)
	}
	// slot-429 should be released and put in cooldown; slot-500 should NOT be released (uncertain)
	if len(capPort.released) != 1 || capPort.released[0] != "slot-429" {
		t.Fatalf("expected only slot-429 released, got %v", capPort.released)
	}
	if len(slotRepo.savedCooldown) != 1 || slotRepo.savedCooldown[0] != "slot-429" {
		t.Fatalf("expected cooldown persisted for slot-429, got %v", slotRepo.savedCooldown)
	}
	if len(capPort.reconciled) != 1 || capPort.reconciled[0] != 50 {
		t.Fatalf("expected actual tokens 50 reconciled, got %v", capPort.reconciled)
	}
}

package grpc_test

import (
	"context"
	"io"
	"log/slog"
	"testing"

	gwapp "aurora/ai-governance/internal/gateway/application"
	gwdomain "aurora/ai-governance/internal/gateway/domain"
	govapp "aurora/ai-governance/internal/governance/application"
	"aurora/ai-governance/internal/governance/domain"
	"aurora/ai-governance/internal/orchestration"
	"aurora/ai-governance/internal/shared"
	grpchandler "aurora/ai-governance/internal/transport/grpc"
	"aurora/ai-governance/proto/aigovpb"
	sharedctx "aurora/shared/context"

	"github.com/google/uuid"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/status"
)

type mockPlanRepo struct{ plan *domain.Plan }

func (m *mockPlanRepo) FindByCode(_ context.Context, _ string) (*domain.Plan, error) {
	return m.plan, nil
}

type mockResolver struct{}

func (m *mockResolver) Resolve(_ context.Context, tid uuid.UUID) domain.TenantPlanResult {
	return domain.TenantPlanResult{Kind: domain.TenantPlanNotFound, Reason: tid.String()}
}

type mockQuotaPort struct{}

func (m *mockQuotaPort) GetCurrentUsage(_ context.Context, _ uuid.UUID, _ domain.QuotaMetric, _ domain.QuotaPeriod, _ domain.PeriodKey) int64 {
	return 0
}

type mockProvider struct{}

func (m *mockProvider) Generate(_ context.Context, slot *gwdomain.ProviderSlot, _ gwdomain.AiGenerateRequest) (gwdomain.AiGenerateResult, error) {
	return gwdomain.AiGenerateResult{Content: "rca output", InputTokens: 10, OutputTokens: 20, Model: slot.ModelName, Provider: "GEMINI"}, nil
}

func (m *mockProvider) Embed(_ context.Context, slot *gwdomain.ProviderSlot, _ gwdomain.AiEmbeddingRequest) (gwdomain.AiEmbeddingResult, error) {
	return gwdomain.AiEmbeddingResult{Vector: []float32{1, 0}, InputTokens: 5, Model: slot.ModelName, Provider: "GEMINI"}, nil
}

type mockSlotRepo struct{ slots []*gwdomain.ProviderSlot }

func (m *mockSlotRepo) FindActiveCandidateSlots(_ context.Context, _ []string, _ []domain.AiProvider, _ shared.AiOperation) ([]*gwdomain.ProviderSlot, error) {
	return m.slots, nil
}
func (m *mockSlotRepo) SaveCooldown(_ context.Context, _ *gwdomain.ProviderSlot) error { return nil }

type mockPoolPolicyRepo struct{}

func (m *mockPoolPolicyRepo) FindByServiceIDOrderByPriorityAsc(_ context.Context, _ string) ([]*gwdomain.ServiceProviderPoolPolicy, error) {
	return nil, nil
}

type mockCapacityPort struct{}

func (m *mockCapacityPort) TryReserve(_ context.Context, slot *gwdomain.ProviderSlot, _ gwdomain.ProviderCapacityLimits, req int64) (*gwdomain.ProviderReservation, error) {
	return &gwdomain.ProviderReservation{ReservationID: "r1", Slot: slot, ReservedTokens: req}, nil
}
func (m *mockCapacityPort) Reconcile(_ context.Context, _ *gwdomain.ProviderReservation, _ int64) {}
func (m *mockCapacityPort) Release(_ context.Context, _ *gwdomain.ProviderReservation)            {}
func (m *mockCapacityPort) GetSlotCapacity(_ context.Context, _ *gwdomain.ProviderSlot) gwdomain.SlotCapacity {
	return gwdomain.SlotCapacity{}
}

func TestGrpcHandlers_IdentityValidationAndExecution(t *testing.T) {
	logger := slog.New(slog.NewTextHandler(io.Discard, nil))
	plan := &domain.Plan{
		Code:           "ENTERPRISE",
		CloudAiEnabled: true,
		Capabilities:   []domain.PlanCapability{{CapabilityCode: "devops.rca"}},
	}
	policySvc := govapp.NewGovernancePolicyService(&mockResolver{}, &mockPlanRepo{plan: plan}, &mockQuotaPort{}, govapp.NewPeriodKeyCalculator(), logger)

	policyHandler := grpchandler.NewPolicyGrpcHandler(policySvc)

	// 1. Missing x-service-id -> UNAUTHENTICATED
	_, err := policyHandler.ExecutePolicy(context.Background(), &aigovpb.ExecutePolicyRequest{CapabilityCode: "devops.rca"})
	if status.Code(err) != codes.Unauthenticated {
		t.Fatalf("expected Unauthenticated, got %v", status.Code(err))
	}

	// 2. Invalid capability code -> INVALID_ARGUMENT
	ctx := sharedctx.WithServiceID(context.Background(), "devops-agent")
	_, err = policyHandler.ExecutePolicy(ctx, &aigovpb.ExecutePolicyRequest{CapabilityCode: "bad capability!"})
	if status.Code(err) != codes.InvalidArgument {
		t.Fatalf("expected InvalidArgument, got %v", status.Code(err))
	}

	// 3. Valid policy check for internal devops-agent
	polResp, err := policyHandler.ExecutePolicy(ctx, &aigovpb.ExecutePolicyRequest{
		CapabilityCode:       "devops.rca",
		EstimatedInputTokens: 100,
		MaxOutputTokens:      200,
	})
	if err != nil || !polResp.GetAllowed() {
		t.Fatalf("expected allowed policy response, err=%v resp=%+v", err, polResp)
	}

	// 4. Execution handler: unsafe storage_reference -> INVALID_ARGUMENT
	capSvc := gwapp.NewCapacityReservationService(&mockCapacityPort{}, gwapp.NewProviderCapacityLimitPolicy(0.20))
	slots := []*gwdomain.ProviderSlot{{SlotAlias: "g1", Provider: domain.AiProviderGemini, ModelName: "gemini-1.5-flash", RpmLimit: 15, TpmLimit: 250000, RpdLimit: 500, Priority: 1}}
	routing := gwapp.NewProviderRoutingService(&mockSlotRepo{slots: slots}, &mockPoolPolicyRepo{}, capSvc, logger)
	execSvc := gwapp.NewAiExecutionService(routing, capSvc, map[domain.AiProvider]gwapp.AiProviderClient{domain.AiProviderGemini: &mockProvider{}}, &mockSlotRepo{}, logger)
	orch := orchestration.NewExecuteAiService(policySvc, execSvc, nil, nil, nil)
	execHandler := grpchandler.NewAiExecutionGrpcHandler(orch, logger)

	_, err = execHandler.Generate(ctx, &aigovpb.AiGenerateRequest{
		CapabilityCode: "devops.rca",
		Prompt:         "analyze",
		InputParts: []*aigovpb.AiInputPart{
			{Value: &aigovpb.AiInputPart_File{File: &aigovpb.AiFileReference{StorageReference: "https://evil.example/ssrf"}}},
		},
	})
	if status.Code(err) != codes.InvalidArgument {
		t.Fatalf("expected InvalidArgument on SSRF storage_reference, got %v", status.Code(err))
	}

	// 5. Valid Generate
	genResp, err := execHandler.Generate(ctx, &aigovpb.AiGenerateRequest{
		CapabilityCode:       "devops.rca",
		Prompt:               "analyze pod crash",
		EstimatedInputTokens: 10,
		MaxOutputTokens:      100,
	})
	if err != nil || genResp.GetContent() != "rca output" || genResp.GetProvider() != "GEMINI" {
		t.Fatalf("unexpected generate response: err=%v resp=%+v", err, genResp)
	}
}

package application_test

import (
	"context"
	"errors"
	"io"
	"log/slog"
	"testing"
	"time"

	govapp "aurora/ai-governance/internal/governance/application"
	"aurora/ai-governance/internal/governance/domain"
	"aurora/ai-governance/internal/shared"

	"github.com/google/uuid"
)

type stubTenantPlanResolver struct {
	res domain.TenantPlanResult
}

func (s *stubTenantPlanResolver) Resolve(_ context.Context, _ uuid.UUID) domain.TenantPlanResult {
	return s.res
}

type stubPlanRepo struct {
	plan *domain.Plan
	err  error
}

func (s *stubPlanRepo) FindByCode(_ context.Context, _ string) (*domain.Plan, error) {
	return s.plan, s.err
}

type stubQuotaPort struct {
	usage map[domain.QuotaMetric]int64
}

func (s *stubQuotaPort) GetCurrentUsage(_ context.Context, _ uuid.UUID, metric domain.QuotaMetric, _ domain.QuotaPeriod, _ domain.PeriodKey) int64 {
	if s.usage == nil {
		return 0
	}
	return s.usage[metric]
}

func testLogger() *slog.Logger {
	return slog.New(slog.NewTextHandler(io.Discard, nil))
}

func TestPeriodKeyCalculator(t *testing.T) {
	calc := govapp.NewPeriodKeyCalculator()
	ts := time.Date(2026, 8, 17, 22, 38, 45, 0, time.UTC)

	if got := calc.Calculate(domain.QuotaPeriodMinute, ts).Value; got != "2026-08-17T22:38" {
		t.Fatalf("unexpected minute key: %s", got)
	}
	if got := calc.Calculate(domain.QuotaPeriodDay, ts).Value; got != "2026-08-17" {
		t.Fatalf("unexpected day key: %s", got)
	}
	if got := calc.Calculate(domain.QuotaPeriodMonth, ts).Value; got != "2026-08" {
		t.Fatalf("unexpected month key: %s", got)
	}
}

func TestGovernancePolicyService_TrustedInternalBypass(t *testing.T) {
	plan := &domain.Plan{
		Code:           "ENTERPRISE",
		CloudAiEnabled: true,
		Capabilities: []domain.PlanCapability{
			{CapabilityCode: "devops.diagnose"},
		},
	}
	svc := govapp.NewGovernancePolicyService(
		&stubTenantPlanResolver{},
		&stubPlanRepo{plan: plan},
		&stubQuotaPort{},
		govapp.NewPeriodKeyCalculator(),
		testLogger(),
	)

	decision := svc.Evaluate(context.Background(), nil, "devops-agent", "devops.rca", shared.AiOperationGenerate, shared.NewTokenBudget(100, 500))
	if !decision.Allowed {
		t.Fatalf("expected trusted internal devops.rca to be allowed, got denied: %v", decision.DenyReason)
	}
	if decision.PolicyVersion != "v1" {
		t.Fatalf("expected policy version v1, got %s", decision.PolicyVersion)
	}
}

func TestGovernancePolicyService_MissingTenantDenied(t *testing.T) {
	svc := govapp.NewGovernancePolicyService(
		&stubTenantPlanResolver{},
		&stubPlanRepo{},
		&stubQuotaPort{},
		govapp.NewPeriodKeyCalculator(),
		testLogger(),
	)

	decision := svc.Evaluate(context.Background(), nil, "route-planning-agent", "route.plan", shared.AiOperationGenerate, shared.NewTokenBudget(100, 200))
	if decision.Allowed || decision.DenyReason == nil || *decision.DenyReason != domain.DenyReasonTenantNotFound {
		t.Fatalf("expected TENANT_NOT_FOUND, got allowed=%v reason=%v", decision.Allowed, decision.DenyReason)
	}
}

func TestGovernancePolicyService_QuotaThreshold95Percent(t *testing.T) {
	tid := uuid.New()
	defProv := domain.AiProviderGemini
	plan := &domain.Plan{
		Code:            "STANDARD",
		CloudAiEnabled:  true,
		DefaultProvider: &defProv,
		Capabilities: []domain.PlanCapability{
			{CapabilityCode: "assistant.general", AllowedProviders: "GEMINI"},
		},
		Quotas: []domain.PlanQuota{
			{QuotaMetric: domain.QuotaMetricRequests, QuotaPeriod: domain.QuotaPeriodMinute, LimitValue: 100},
		},
	}
	resolver := &stubTenantPlanResolver{
		res: domain.TenantPlanResult{
			Kind: domain.TenantPlanSuccess,
			Info: &domain.TenantPlanInfo{TenantID: tid, PlanCode: "STANDARD", Status: domain.TenantStatusActive, CloudAiEnabled: true},
		},
	}

	// 94 + 1 = 95 <= 95 -> allowed
	svcOK := govapp.NewGovernancePolicyService(resolver, &stubPlanRepo{plan: plan}, &stubQuotaPort{usage: map[domain.QuotaMetric]int64{domain.QuotaMetricRequests: 94}}, govapp.NewPeriodKeyCalculator(), testLogger())
	decOK := svcOK.Evaluate(context.Background(), &tid, "customer-assistant", "assistant.general", shared.AiOperationGenerate, shared.NewTokenBudget(10, 10))
	if !decOK.Allowed {
		t.Fatalf("expected allowed at 95%% threshold, got denied: %v", decOK.DenyReason)
	}

	// 95 + 1 = 96 > 95 -> denied
	svcExceed := govapp.NewGovernancePolicyService(resolver, &stubPlanRepo{plan: plan}, &stubQuotaPort{usage: map[domain.QuotaMetric]int64{domain.QuotaMetricRequests: 95}}, govapp.NewPeriodKeyCalculator(), testLogger())
	decExceed := svcExceed.Evaluate(context.Background(), &tid, "customer-assistant", "assistant.general", shared.AiOperationGenerate, shared.NewTokenBudget(10, 10))
	if decExceed.Allowed || decExceed.DenyReason == nil || *decExceed.DenyReason != domain.DenyReasonQuotaExceeded {
		t.Fatalf("expected QUOTA_EXCEEDED, got allowed=%v reason=%v", decExceed.Allowed, decExceed.DenyReason)
	}
}

func TestGovernancePolicyService_FailClosedOnIamUnavailableOrRepoError(t *testing.T) {
	tid := uuid.New()
	svcIamDown := govapp.NewGovernancePolicyService(
		&stubTenantPlanResolver{res: domain.TenantPlanResult{Kind: domain.TenantPlanIamUnavailable, Reason: "timeout"}},
		&stubPlanRepo{},
		&stubQuotaPort{},
		govapp.NewPeriodKeyCalculator(),
		testLogger(),
	)
	dec := svcIamDown.Evaluate(context.Background(), &tid, "mail-service", "mail.security", shared.AiOperationGenerate, shared.NewTokenBudget(10, 10))
	if dec.Allowed || dec.DenyReason == nil || *dec.DenyReason != domain.DenyReasonPolicyError {
		t.Fatalf("expected POLICY_ERROR on IAM unavailable, got %v", dec.DenyReason)
	}

	svcRepoErr := govapp.NewGovernancePolicyService(
		&stubTenantPlanResolver{res: domain.TenantPlanResult{
			Kind: domain.TenantPlanSuccess,
			Info: &domain.TenantPlanInfo{TenantID: tid, PlanCode: "STANDARD", Status: domain.TenantStatusActive, CloudAiEnabled: true},
		}},
		&stubPlanRepo{err: errors.New("db connection refused")},
		&stubQuotaPort{},
		govapp.NewPeriodKeyCalculator(),
		testLogger(),
	)
	dec2 := svcRepoErr.Evaluate(context.Background(), &tid, "mail-service", "mail.security", shared.AiOperationGenerate, shared.NewTokenBudget(10, 10))
	if dec2.Allowed || dec2.DenyReason == nil || *dec2.DenyReason != domain.DenyReasonPolicyError {
		t.Fatalf("expected POLICY_ERROR on repo failure, got %v", dec2.DenyReason)
	}
}

package services_test

import (
	"context"
	"testing"
	"time"

	"aurora/route-planning-agent/internal/application/dtos"
	"aurora/route-planning-agent/internal/application/services"
	"aurora/route-planning-agent/internal/domain/entities"
	"aurora/route-planning-agent/internal/domain/enums"
	domainrules "aurora/route-planning-agent/internal/domain/rules"

	"github.com/google/uuid"
)

type mockApprovalRepoForGov struct {
	approvals map[uuid.UUID]*entities.ApprovalRequest
}

func (m *mockApprovalRepoForGov) GetByID(ctx context.Context, tenantID, id uuid.UUID) (*entities.ApprovalRequest, error) {
	if a, ok := m.approvals[id]; ok && a.TenantID == tenantID {
		return a, nil
	}
	return nil, nil
}
func (m *mockApprovalRepoForGov) GetPendingByRouteID(ctx context.Context, tenantID, routeID uuid.UUID) (*entities.ApprovalRequest, error) {
	for _, a := range m.approvals {
		if a.TenantID == tenantID && a.RouteID == routeID && a.Status == enums.ApprovalStatusPending {
			return a, nil
		}
	}
	return nil, nil
}
func (m *mockApprovalRepoForGov) GetValidApproved(ctx context.Context, tenantID, routeID uuid.UUID, routeVersion int32, policyID string, policyVersion int32) (*entities.ApprovalRequest, error) {
	for _, a := range m.approvals {
		if a.TenantID == tenantID && a.RouteID == routeID && a.Status == enums.ApprovalStatusApproved &&
			a.RouteVersion == routeVersion && a.PolicyID == policyID && a.PolicyVersion == policyVersion {
			return a, nil
		}
	}
	return nil, nil
}
func (m *mockApprovalRepoForGov) ListPending(ctx context.Context, tenantID uuid.UUID, page, limit int32) ([]entities.ApprovalRequest, int64, error) {
	return nil, 0, nil
}
func (m *mockApprovalRepoForGov) Create(ctx context.Context, req *entities.ApprovalRequest) error {
	m.approvals[req.ID] = req
	return nil
}
func (m *mockApprovalRepoForGov) Update(ctx context.Context, req *entities.ApprovalRequest) error {
	m.approvals[req.ID] = req
	return nil
}
func (m *mockApprovalRepoForGov) CreateDecisionAuditLog(ctx context.Context, log *entities.RouteDecisionAuditLog) error {
	return nil
}

type mockRiskRepoForGov struct {
	assessments map[uuid.UUID]*entities.RiskAssessment
}

func (m *mockRiskRepoForGov) GetConfig(ctx context.Context, tenantID uuid.UUID) (*entities.TenantRiskPolicyConfig, error) {
	return nil, nil
}
func (m *mockRiskRepoForGov) UpsertConfig(ctx context.Context, cfg *entities.TenantRiskPolicyConfig) error {
	return nil
}
func (m *mockRiskRepoForGov) GetPolicyByID(ctx context.Context, tenantID, id uuid.UUID) (*entities.TenantRiskPolicy, error) {
	return nil, nil
}
func (m *mockRiskRepoForGov) GetActivePolicy(ctx context.Context, tenantID uuid.UUID, scope string) (*entities.TenantRiskPolicy, error) {
	return nil, nil
}
func (m *mockRiskRepoForGov) GetActivePoliciesByScope(ctx context.Context, tenantID uuid.UUID, scope string) ([]entities.TenantRiskPolicy, error) {
	return nil, nil
}
func (m *mockRiskRepoForGov) GetMaxVersion(ctx context.Context, tenantID uuid.UUID, scope string) (int32, error) {
	return 0, nil
}
func (m *mockRiskRepoForGov) GetPolicyByVersion(ctx context.Context, tenantID uuid.UUID, scope string, version int32) (*entities.TenantRiskPolicy, error) {
	return nil, nil
}
func (m *mockRiskRepoForGov) ListPolicyVersions(ctx context.Context, tenantID uuid.UUID, scope string, page, limit int32) ([]entities.TenantRiskPolicy, int64, error) {
	return nil, 0, nil
}
func (m *mockRiskRepoForGov) CreatePolicyDraft(ctx context.Context, policy *entities.TenantRiskPolicy) error {
	return nil
}
func (m *mockRiskRepoForGov) UpdatePolicyDraft(ctx context.Context, policy *entities.TenantRiskPolicy) error {
	return nil
}
func (m *mockRiskRepoForGov) DeletePolicyDraft(ctx context.Context, tenantID, id uuid.UUID) error {
	return nil
}
func (m *mockRiskRepoForGov) CreateRiskAssessment(ctx context.Context, assessment *entities.RiskAssessment) error {
	m.assessments[assessment.RouteID] = assessment
	return nil
}
func (m *mockRiskRepoForGov) GetLatestAssessment(ctx context.Context, tenantID, routeID uuid.UUID) (*entities.RiskAssessment, error) {
	if a, ok := m.assessments[routeID]; ok && a.TenantID == tenantID {
		return a, nil
	}
	return nil, nil
}

func TestGovernanceServiceAssessRoute(t *testing.T) {
	approvalRepo := &mockApprovalRepoForGov{approvals: make(map[uuid.UUID]*entities.ApprovalRequest)}
	riskRepo := &mockRiskRepoForGov{assessments: make(map[uuid.UUID]*entities.RiskAssessment)}
	govService := services.NewRouteGovernanceService(riskRepo, approvalRepo)

	ctx := context.Background()
	effectivePolicy := &services.EffectiveRiskPolicy{
		PolicyID: "policy-test",
		Version:  1,
		Source:   enums.RiskPolicySourcePlatformDefault,
	}

	route := &entities.Route{
		Version: 1,
	}
	route.ID = uuid.New()
	route.TenantID = uuid.New()

	t.Run("LOW deterministic + HIGH AI => HIGH", func(t *testing.T) {
		ruleResults := []domainrules.RuleResult{
			{RuleName: "R1", Passed: true, RiskLevel: enums.RouteRiskLevelLow},
		}
		aiResult := &dtos.RouteAiResult{
			Success: true,
			Recommendation: dtos.RouteRecommendationDto{
				RiskLevel: "High",
			},
		}

		res, err := govService.AssessRoute(ctx, route, effectivePolicy, ruleResults, nil, aiResult)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if res.RiskLevel != enums.RouteRiskLevelHigh {
			t.Errorf("expected RiskLevel High, got %v", res.RiskLevel)
		}
	})

	t.Run("HIGH deterministic + LOW AI => HIGH (AI cannot reduce risk)", func(t *testing.T) {
		ruleResults := []domainrules.RuleResult{
			{RuleName: "R1", Passed: false, RiskLevel: enums.RouteRiskLevelHigh, Message: "Heavy load"},
		}
		aiResult := &dtos.RouteAiResult{
			Success: true,
			Recommendation: dtos.RouteRecommendationDto{
				RiskLevel: "Low",
			},
		}

		res, err := govService.AssessRoute(ctx, route, effectivePolicy, ruleResults, nil, aiResult)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if res.RiskLevel != enums.RouteRiskLevelHigh {
			t.Errorf("expected RiskLevel High, got %v", res.RiskLevel)
		}
	})

	t.Run("CRITICAL deterministic + LOW AI => CRITICAL", func(t *testing.T) {
		ruleResults := []domainrules.RuleResult{
			{RuleName: "R1", Passed: false, RiskLevel: enums.RouteRiskLevelCritical, Message: "Dangerous"},
		}
		aiResult := &dtos.RouteAiResult{
			Success: true,
			Recommendation: dtos.RouteRecommendationDto{
				RiskLevel: "Low",
			},
		}

		res, err := govService.AssessRoute(ctx, route, effectivePolicy, ruleResults, nil, aiResult)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if res.RiskLevel != enums.RouteRiskLevelCritical {
			t.Errorf("expected RiskLevel Critical, got %v", res.RiskLevel)
		}
		if res.Decision != enums.GovernanceDecisionBlocked {
			t.Errorf("expected Decision Blocked, got %v", res.Decision)
		}
	})

	t.Run("MEDIUM deterministic + AI unavailable => MEDIUM", func(t *testing.T) {
		ruleResults := []domainrules.RuleResult{
			{RuleName: "R1", Passed: false, RiskLevel: enums.RouteRiskLevelMedium, Message: "Medium risk"},
		}

		res, err := govService.AssessRoute(ctx, route, effectivePolicy, ruleResults, nil, nil)
		if err != nil {
			t.Fatalf("unexpected error: %v", err)
		}
		if res.RiskLevel != enums.RouteRiskLevelMedium {
			t.Errorf("expected RiskLevel Medium, got %v", res.RiskLevel)
		}
		if res.Decision != enums.GovernanceDecisionStaffAllowed {
			t.Errorf("expected Decision StaffAllowed, got %v", res.Decision)
		}
	})
}

func TestGovernanceServiceValidateExecutionAuthorized(t *testing.T) {
	approvalRepo := &mockApprovalRepoForGov{approvals: make(map[uuid.UUID]*entities.ApprovalRequest)}
	riskRepo := &mockRiskRepoForGov{assessments: make(map[uuid.UUID]*entities.RiskAssessment)}
	govService := services.NewRouteGovernanceService(riskRepo, approvalRepo)

	ctx := context.Background()
	effectivePolicy := &services.EffectiveRiskPolicy{
		PolicyID: "policy-test",
		Version:  2,
		Source:   enums.RiskPolicySourceTenant,
	}

	tenantID := uuid.New()
	routeID := uuid.New()

	setAssessment := func(riskLevel enums.RouteRiskLevel, decision enums.GovernanceDecision, routeVer, polVer int32) {
		riskRepo.assessments[routeID] = &entities.RiskAssessment{
			RouteID:            routeID,
			RouteVersion:       routeVer,
			PolicyID:           effectivePolicy.PolicyID,
			PolicyVersion:      polVer,
			RiskLevel:          riskLevel,
			GovernanceDecision: decision,
			ReasonDetails:      "test assessment",
		}
		riskRepo.assessments[routeID].TenantID = tenantID
	}

	t.Run("Low risk => Authorized", func(t *testing.T) {
		route := &entities.Route{
			RiskLevel:          enums.RouteRiskLevelLow,
			GovernanceDecision: enums.GovernanceDecisionNoApprovalRequired,
			Version:            1,
		}
		route.ID = routeID
		route.TenantID = tenantID
		setAssessment(enums.RouteRiskLevelLow, enums.GovernanceDecisionNoApprovalRequired, 1, 2)

		err := govService.ValidateExecutionAuthorized(ctx, route, effectivePolicy)
		if err != nil {
			t.Errorf("expected authorized, got %v", err)
		}
	})

	t.Run("Medium risk => Authorized (StaffAllowed)", func(t *testing.T) {
		route := &entities.Route{
			RiskLevel:          enums.RouteRiskLevelMedium,
			GovernanceDecision: enums.GovernanceDecisionStaffAllowed,
			Version:            1,
		}
		route.ID = routeID
		route.TenantID = tenantID
		setAssessment(enums.RouteRiskLevelMedium, enums.GovernanceDecisionStaffAllowed, 1, 2)

		err := govService.ValidateExecutionAuthorized(ctx, route, effectivePolicy)
		if err != nil {
			t.Errorf("expected authorized, got %v", err)
		}
	})

	t.Run("Critical risk => Blocked", func(t *testing.T) {
		route := &entities.Route{
			RiskLevel:          enums.RouteRiskLevelCritical,
			GovernanceDecision: enums.GovernanceDecisionBlocked,
			Version:            1,
		}
		route.ID = routeID
		route.TenantID = tenantID
		setAssessment(enums.RouteRiskLevelCritical, enums.GovernanceDecisionBlocked, 1, 2)

		err := govService.ValidateExecutionAuthorized(ctx, route, effectivePolicy)
		if err == nil {
			t.Errorf("expected error for Critical blocked route, got nil")
		}
	})

	t.Run("High risk without approval => Denied", func(t *testing.T) {
		route := &entities.Route{
			RiskLevel:          enums.RouteRiskLevelHigh,
			GovernanceDecision: enums.GovernanceDecisionManagerApprovalRequired,
			Version:            1,
		}
		route.ID = routeID
		route.TenantID = tenantID
		setAssessment(enums.RouteRiskLevelHigh, enums.GovernanceDecisionManagerApprovalRequired, 1, 2)

		err := govService.ValidateExecutionAuthorized(ctx, route, effectivePolicy)
		if err == nil {
			t.Errorf("expected error for unapproved high risk route, got nil")
		}
	})

	t.Run("High risk with stale version approval => Denied", func(t *testing.T) {
		route := &entities.Route{
			RiskLevel:          enums.RouteRiskLevelHigh,
			GovernanceDecision: enums.GovernanceDecisionManagerApprovalRequired,
			Version:            2, // Route is version 2
		}
		route.ID = routeID
		route.TenantID = tenantID
		setAssessment(enums.RouteRiskLevelHigh, enums.GovernanceDecisionManagerApprovalRequired, 2, 2)

		// Approval was granted for version 1
		apprID := uuid.New()
		now := time.Now().UTC()
		approvalRepo.approvals[apprID] = &entities.ApprovalRequest{
			RouteID:       routeID,
			RouteVersion:  1, // Stale!
			PolicyID:      "policy-test",
			PolicyVersion: 2,
			Status:        enums.ApprovalStatusApproved,
			ReviewedAt:    &now,
		}
		approvalRepo.approvals[apprID].ID = apprID
		approvalRepo.approvals[apprID].TenantID = tenantID

		err := govService.ValidateExecutionAuthorized(ctx, route, effectivePolicy)
		if err == nil {
			t.Errorf("expected error for stale version approval, got nil")
		}
	})

	t.Run("High risk with valid approved ticket => Authorized", func(t *testing.T) {
		route := &entities.Route{
			RiskLevel:          enums.RouteRiskLevelHigh,
			GovernanceDecision: enums.GovernanceDecisionManagerApprovalRequired,
			Version:            2,
		}
		route.ID = routeID
		route.TenantID = tenantID
		setAssessment(enums.RouteRiskLevelHigh, enums.GovernanceDecisionManagerApprovalRequired, 2, 2)

		// Valid approval for route version 2 and policy version 2
		apprID := uuid.New()
		now := time.Now().UTC()
		approvalRepo.approvals[apprID] = &entities.ApprovalRequest{
			RouteID:       routeID,
			RouteVersion:  2,
			PolicyID:      "policy-test",
			PolicyVersion: 2,
			Status:        enums.ApprovalStatusApproved,
			ReviewedAt:    &now,
		}
		approvalRepo.approvals[apprID].ID = apprID
		approvalRepo.approvals[apprID].TenantID = tenantID

		err := govService.ValidateExecutionAuthorized(ctx, route, effectivePolicy)
		if err != nil {
			t.Errorf("expected authorized, got %v", err)
		}
	})
}

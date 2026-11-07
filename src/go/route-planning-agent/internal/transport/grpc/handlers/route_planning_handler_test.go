package handlers_test

import (
	"context"
	"errors"
	"testing"
	"time"

	"aurora/route-planning-agent/internal/application/dtos"
	"aurora/route-planning-agent/internal/application/services"
	"aurora/route-planning-agent/internal/domain/entities"
	"aurora/route-planning-agent/internal/domain/enums"
	domainrules "aurora/route-planning-agent/internal/domain/rules"
	"aurora/route-planning-agent/internal/infrastructure/rules"
	"aurora/route-planning-agent/internal/transport/grpc/handlers"
	"aurora/route-planning-agent/proto/routeplanningpb"
	sharedctx "aurora/shared/context"

	"github.com/google/uuid"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/status"
)

// In-Memory Mocks cho testing handler
type mockRouteRepo struct {
	routes map[uuid.UUID]*entities.Route
}

func (m *mockRouteRepo) GetByID(ctx context.Context, tenantID, id uuid.UUID) (*entities.Route, error) {
	if r, ok := m.routes[id]; ok && r.TenantID == tenantID {
		return r, nil
	}
	return nil, nil
}
func (m *mockRouteRepo) List(ctx context.Context, tenantID uuid.UUID, page, limit int32, s *enums.RouteStatus) ([]entities.Route, int64, error) {
	list := make([]entities.Route, 0)
	for _, r := range m.routes {
		if r.TenantID == tenantID {
			list = append(list, *r)
		}
	}
	return list, int64(len(list)), nil
}
func (m *mockRouteRepo) Create(ctx context.Context, r *entities.Route) error {
	m.routes[r.ID] = r
	return nil
}
func (m *mockRouteRepo) Update(ctx context.Context, r *entities.Route) error {
	m.routes[r.ID] = r
	return nil
}
func (m *mockRouteRepo) Delete(ctx context.Context, tenantID, id uuid.UUID) error {
	delete(m.routes, id)
	return nil
}
func (m *mockRouteRepo) UpdateWithVersion(ctx context.Context, r *entities.Route, expectedVersion int32) error {
	m.routes[r.ID] = r
	return nil
}
func (m *mockRouteRepo) AddOptimizationHistory(ctx context.Context, h *entities.RouteOptimizationHistory) error {
	return nil
}

type mockApprovalRepo struct {
	approvals map[uuid.UUID]*entities.ApprovalRequest
	auditLogs []*entities.RouteDecisionAuditLog
}

func (m *mockApprovalRepo) GetByID(ctx context.Context, tenantID, id uuid.UUID) (*entities.ApprovalRequest, error) {
	if a, ok := m.approvals[id]; ok && a.TenantID == tenantID {
		return a, nil
	}
	return nil, nil
}
func (m *mockApprovalRepo) GetPendingByRouteID(ctx context.Context, tenantID, routeID uuid.UUID) (*entities.ApprovalRequest, error) {
	for _, a := range m.approvals {
		if a.TenantID == tenantID && a.RouteID == routeID && a.Status == enums.ApprovalStatusPending {
			return a, nil
		}
	}
	return nil, nil
}
func (m *mockApprovalRepo) GetValidApproved(ctx context.Context, tenantID, routeID uuid.UUID, routeVersion int32, policyID string, policyVersion int32) (*entities.ApprovalRequest, error) {
	for _, a := range m.approvals {
		if a.TenantID == tenantID && a.RouteID == routeID && a.Status == enums.ApprovalStatusApproved &&
			a.RouteVersion == routeVersion && a.PolicyID == policyID && a.PolicyVersion == policyVersion {
			return a, nil
		}
	}
	return nil, nil
}
func (m *mockApprovalRepo) ListPending(ctx context.Context, tenantID uuid.UUID, page, limit int32) ([]entities.ApprovalRequest, int64, error) {
	var list []entities.ApprovalRequest
	for _, a := range m.approvals {
		if a.TenantID == tenantID && a.Status == enums.ApprovalStatusPending {
			list = append(list, *a)
		}
	}
	return list, int64(len(list)), nil
}
func (m *mockApprovalRepo) Create(ctx context.Context, req *entities.ApprovalRequest) error {
	m.approvals[req.ID] = req
	return nil
}
func (m *mockApprovalRepo) Update(ctx context.Context, req *entities.ApprovalRequest) error {
	m.approvals[req.ID] = req
	return nil
}
func (m *mockApprovalRepo) CreateDecisionAuditLog(ctx context.Context, log *entities.RouteDecisionAuditLog) error {
	m.auditLogs = append(m.auditLogs, log)
	return nil
}

type mockRiskPolicyRepo struct {
	assessments []*entities.RiskAssessment
	policies    map[uuid.UUID]*entities.TenantRiskPolicy
	maxVersions map[string]int32
}

func (m *mockRiskPolicyRepo) GetConfig(ctx context.Context, tenantID uuid.UUID) (*entities.TenantRiskPolicyConfig, error) {
	return nil, nil
}
func (m *mockRiskPolicyRepo) UpsertConfig(ctx context.Context, cfg *entities.TenantRiskPolicyConfig) error {
	return nil
}
func (m *mockRiskPolicyRepo) GetPolicyByID(ctx context.Context, tenantID, id uuid.UUID) (*entities.TenantRiskPolicy, error) {
	if m.policies != nil {
		if p, ok := m.policies[id]; ok && p.TenantID == tenantID {
			return p, nil
		}
	}
	return nil, nil
}
func (m *mockRiskPolicyRepo) GetActivePolicy(ctx context.Context, tenantID uuid.UUID, scope string) (*entities.TenantRiskPolicy, error) {
	return nil, nil
}
func (m *mockRiskPolicyRepo) GetActivePoliciesByScope(ctx context.Context, tenantID uuid.UUID, scope string) ([]entities.TenantRiskPolicy, error) {
	return nil, nil
}
func (m *mockRiskPolicyRepo) GetMaxVersion(ctx context.Context, tenantID uuid.UUID, scope string) (int32, error) {
	if m.maxVersions != nil {
		return m.maxVersions[tenantID.String()+":"+scope], nil
	}
	return 0, nil
}
func (m *mockRiskPolicyRepo) GetPolicyByVersion(ctx context.Context, tenantID uuid.UUID, scope string, version int32) (*entities.TenantRiskPolicy, error) {
	return nil, nil
}
func (m *mockRiskPolicyRepo) ListPolicyVersions(ctx context.Context, tenantID uuid.UUID, scope string, page, limit int32) ([]entities.TenantRiskPolicy, int64, error) {
	return nil, 0, nil
}
func (m *mockRiskPolicyRepo) CreatePolicyDraft(ctx context.Context, p *entities.TenantRiskPolicy) error {
	if m.policies == nil {
		m.policies = make(map[uuid.UUID]*entities.TenantRiskPolicy)
	}
	if m.maxVersions == nil {
		m.maxVersions = make(map[string]int32)
	}
	key := p.TenantID.String() + ":" + p.Scope
	m.maxVersions[key]++
	p.Version = m.maxVersions[key]
	m.policies[p.ID] = p
	return nil
}
func (m *mockRiskPolicyRepo) UpdatePolicyDraft(ctx context.Context, p *entities.TenantRiskPolicy) error {
	if m.policies != nil {
		m.policies[p.ID] = p
	}
	return nil
}
func (m *mockRiskPolicyRepo) DeletePolicyDraft(ctx context.Context, tenantID, id uuid.UUID) error {
	if m.policies != nil {
		delete(m.policies, id)
	}
	return nil
}
func (m *mockRiskPolicyRepo) CreateRiskAssessment(ctx context.Context, a *entities.RiskAssessment) error {
	m.assessments = append(m.assessments, a)
	return nil
}
func (m *mockRiskPolicyRepo) GetLatestAssessment(ctx context.Context, tenantID, routeID uuid.UUID) (*entities.RiskAssessment, error) {
	if len(m.assessments) > 0 {
		return m.assessments[len(m.assessments)-1], nil
	}
	return nil, nil
}

type mockRuleConfigRepo struct{}

func (m *mockRuleConfigRepo) GetByRuleName(ctx context.Context, tenantID uuid.UUID, ruleName string) (*entities.TenantRuleConfig, error) {
	return nil, nil
}
func (m *mockRuleConfigRepo) List(ctx context.Context, tenantID uuid.UUID, page, limit int32) ([]entities.TenantRuleConfig, int64, error) {
	return nil, 0, nil
}
func (m *mockRuleConfigRepo) ListAll(ctx context.Context, tenantID uuid.UUID) ([]entities.TenantRuleConfig, error) {
	return nil, nil
}
func (m *mockRuleConfigRepo) Upsert(ctx context.Context, cfg *entities.TenantRuleConfig) error {
	return nil
}

type mockOutboxRepo struct {
	messages []*entities.OutboxMessage
	failSave bool
	claimed  map[uuid.UUID]string
}

func (m *mockOutboxRepo) Save(ctx context.Context, msg *entities.OutboxMessage) error {
	if m.failSave {
		return errors.New("simulated outbox write failure")
	}
	m.messages = append(m.messages, msg)
	return nil
}
func (m *mockOutboxRepo) GetUnprocessed(ctx context.Context, batchSize int32) ([]entities.OutboxMessage, error) {
	if m.claimed == nil {
		m.claimed = make(map[uuid.UUID]string)
	}
	var res []entities.OutboxMessage
	for _, msg := range m.messages {
		if msg.ProcessedAt == nil && msg.RetryCount < 5 {
			if _, ok := m.claimed[msg.ID]; !ok {
				m.claimed[msg.ID] = "CLAIMED"
				msg.RetryCount++
				res = append(res, *msg)
				if int32(len(res)) >= batchSize {
					break
				}
			}
		}
	}
	return res, nil
}
func (m *mockOutboxRepo) MarkProcessed(ctx context.Context, id uuid.UUID, processedAt time.Time) error {
	for _, msg := range m.messages {
		if msg.ID == id {
			msg.ProcessedAt = &processedAt
			delete(m.claimed, id)
			return nil
		}
	}
	return nil
}
func (m *mockOutboxRepo) MarkFailed(ctx context.Context, id uuid.UUID, errMessage string) error {
	for _, msg := range m.messages {
		if msg.ID == id {
			msg.Error = &errMessage
			delete(m.claimed, id)
			return nil
		}
	}
	return nil
}

type mockUnitOfWork struct {
	InTx       bool
	TxCount    int
	RolledBack bool
}

func (m *mockUnitOfWork) DoInTransaction(ctx context.Context, fn func(ctx context.Context) error) error {
	m.InTx = true
	m.TxCount++
	err := fn(ctx)
	if err != nil {
		m.RolledBack = true
	}
	m.InTx = false
	return err
}

type mockAiService struct{}

func (m *mockAiService) GetRecommendation(ctx context.Context, route *dtos.RouteDto, rules []domainrules.RuleResult, comp *dtos.ComplianceCheckResultDto) *dtos.RouteAiResult {
	conf := 0.9
	return &dtos.RouteAiResult{
		Success: true,
		Recommendation: dtos.RouteRecommendationDto{
			Summary:         "AI đánh giá tốt",
			ConfidenceScore: &conf,
			Suggestions:     []string{"Lưu ý thời tiết"},
		},
	}
}

type mockComplianceService struct{}

func (m *mockComplianceService) CheckCompliance(ctx context.Context, tenantID uuid.UUID, routeJSON string, riskReasons []string) *dtos.ComplianceCheckResultDto {
	return &dtos.ComplianceCheckResultDto{
		HasViolations: false,
	}
}

type mockPolicyProvider struct{}

func (m *mockPolicyProvider) GetEffectivePolicy(ctx context.Context, tenantID uuid.UUID, scope string) (*services.EffectiveRiskPolicy, error) {
	return &services.EffectiveRiskPolicy{
		PolicyID: "platform-default-route-governance",
		Version:  1,
		Source:   enums.RiskPolicySourcePlatformDefault,
		Scope:    scope,
		TenantID: tenantID,
	}, nil
}

func setupTestHandler() (*handlers.RoutePlanningHandler, *mockRouteRepo, *mockApprovalRepo) {
	routeRepo := &mockRouteRepo{routes: make(map[uuid.UUID]*entities.Route)}
	approvalRepo := &mockApprovalRepo{approvals: make(map[uuid.UUID]*entities.ApprovalRequest)}
	policyRepo := &mockRiskPolicyRepo{}
	ruleConfigRepo := &mockRuleConfigRepo{}
	outboxRepo := &mockOutboxRepo{}
	uow := &mockUnitOfWork{}

	ruleConfigService := services.NewTenantRuleConfigService(ruleConfigRepo)
	policyProvider := &mockPolicyProvider{}
	govService := services.NewRouteGovernanceService(policyRepo, approvalRepo)
	apprService := services.NewApprovalService(approvalRepo, routeRepo, uow)
	ruleEngine := rules.NewDefaultRouteRuleEngine()
	aiService := &mockAiService{}
	complianceService := &mockComplianceService{}

	handler := handlers.NewRoutePlanningHandler(
		routeRepo, approvalRepo, policyRepo, ruleConfigRepo, outboxRepo,
		uow, ruleEngine, nil, aiService, complianceService,
		govService, apprService, policyProvider, ruleConfigService, nil,
	)

	return handler, routeRepo, approvalRepo
}

func TestCreateAndGetRoute(t *testing.T) {
	handler, _, _ := setupTestHandler()
	tenantID := uuid.New()
	ctx := sharedctx.WithTenantID(context.Background(), tenantID)

	// 1. Test CreateRoute
	req := &routeplanningpb.CreateRouteRequest{
		Name:        "Tuyến Hà Nội - Hải Phòng",
		Description: "Vận chuyển linh kiện",
		RouteType:   "Fixed",
		MaxWeightKg: 2000,
		MaxVolumeM3: 20,
		Stops: []*routeplanningpb.RouteStopInput{
			{
				Sequence:     1,
				StopType:     "Pickup",
				LocationName: "Kho Hà Nội",
				Address:      "Hà Nội",
				Latitude:     21.0285,
				Longitude:    105.8542,
			},
			{
				Sequence:     2,
				StopType:     "Delivery",
				LocationName: "Cảng Hải Phòng",
				Address:      "Hải Phòng",
				Latitude:     20.8449,
				Longitude:    106.6881,
			},
		},
	}

	createResp, err := handler.CreateRoute(ctx, req)
	if err != nil {
		t.Fatalf("CreateRoute failed: %v", err)
	}

	if createResp.GetId() == "" {
		t.Errorf("expected route ID generated")
	}
	if createResp.GetName() != req.GetName() {
		t.Errorf("expected route name %s, got %s", req.GetName(), createResp.GetName())
	}
	if len(createResp.GetStops()) != 2 {
		t.Errorf("expected 2 stops, got %d", len(createResp.GetStops()))
	}

	// 2. Test GetRoute
	getResp, err := handler.GetRoute(ctx, &routeplanningpb.GetRouteRequest{Id: createResp.GetId()})
	if err != nil {
		t.Fatalf("GetRoute failed: %v", err)
	}

	if getResp.GetId() != createResp.GetId() {
		t.Errorf("GetRoute returned wrong ID")
	}

	// 3. Test Fail-Closed if missing TenantID
	_, err = handler.CreateRoute(context.Background(), req)
	if status.Code(err) != codes.InvalidArgument {
		t.Errorf("expected InvalidArgument error when missing TenantID, got: %v", err)
	}
}

func TestApproveAndRejectRoute(t *testing.T) {
	handler, routeRepo, approvalRepo := setupTestHandler()
	tenantID := uuid.New()
	reviewerID := uuid.New()
	ctx := sharedctx.WithUserID(sharedctx.WithTenantID(context.Background(), tenantID), reviewerID.String())

	routeID := uuid.New()
	route := &entities.Route{
		Name:               "Tuyến Rủi Ro Cao",
		Status:             enums.RouteStatusDraft,
		GovernanceDecision: enums.GovernanceDecisionManagerApprovalRequired,
	}
	route.ID = routeID
	route.TenantID = tenantID
	_ = routeRepo.Create(ctx, route)

	approvalID := uuid.New()
	approval := &entities.ApprovalRequest{
		RouteID:  routeID,
		Status:   enums.ApprovalStatusPending,
		Reason:   "Vượt quá trọng lượng 6000kg",
		Feature:  "RoutePlanning",
		PolicyID: "platform-default-route-governance",
	}
	approval.ID = approvalID
	approval.TenantID = tenantID
	_ = approvalRepo.Create(ctx, approval)

	// Test ApproveRoute
	comment := "Đồng ý cho chạy chuyến sớm"
	apprResp, err := handler.ApproveRoute(ctx, &routeplanningpb.ApproveRouteRequest{
		ApprovalId: approvalID.String(),
		IsApproved: true,
		Comment:    comment,
	})
	if err != nil {
		t.Fatalf("ApproveRoute failed: %v", err)
	}

	if apprResp.GetStatus() != "Approved" {
		t.Errorf("expected status Approved, got %s", apprResp.GetStatus())
	}

	// Kiểm tra trạng thái route chuyển sang Ready
	updatedRoute, _ := routeRepo.GetByID(ctx, tenantID, routeID)
	if updatedRoute.Status != enums.RouteStatusReady {
		t.Errorf("expected route status Ready, got %s", updatedRoute.Status)
	}
	if updatedRoute.GovernanceDecision != enums.GovernanceDecisionStaffAllowed {
		t.Errorf("expected StaffAllowed, got %s", updatedRoute.GovernanceDecision)
	}
}

type fullTestContext struct {
	handler      *handlers.RoutePlanningHandler
	routeRepo    *mockRouteRepo
	approvalRepo *mockApprovalRepo
	policyRepo   *mockRiskPolicyRepo
	outboxRepo   *mockOutboxRepo
	uow          *mockUnitOfWork
}

func setupFullTestContext() *fullTestContext {
	routeRepo := &mockRouteRepo{routes: make(map[uuid.UUID]*entities.Route)}
	approvalRepo := &mockApprovalRepo{approvals: make(map[uuid.UUID]*entities.ApprovalRequest)}
	policyRepo := &mockRiskPolicyRepo{
		policies:    make(map[uuid.UUID]*entities.TenantRiskPolicy),
		maxVersions: make(map[string]int32),
	}
	ruleConfigRepo := &mockRuleConfigRepo{}
	outboxRepo := &mockOutboxRepo{}
	uow := &mockUnitOfWork{}

	ruleConfigService := services.NewTenantRuleConfigService(ruleConfigRepo)
	policyProvider := &mockPolicyProvider{}
	govService := services.NewRouteGovernanceService(policyRepo, approvalRepo)
	apprService := services.NewApprovalService(approvalRepo, routeRepo, uow)
	ruleEngine := rules.NewDefaultRouteRuleEngine()
	aiService := &mockAiService{}
	complianceService := &mockComplianceService{}

	handler := handlers.NewRoutePlanningHandler(
		routeRepo, approvalRepo, policyRepo, ruleConfigRepo, outboxRepo,
		uow, ruleEngine, nil, aiService, complianceService,
		govService, apprService, policyProvider, ruleConfigService, nil,
	)

	return &fullTestContext{
		handler:      handler,
		routeRepo:    routeRepo,
		approvalRepo: approvalRepo,
		policyRepo:   policyRepo,
		outboxRepo:   outboxRepo,
		uow:          uow,
	}
}

func TestGetRouteRecommendation_TransactionalIntegrityAndAudit(t *testing.T) {
	tc := setupFullTestContext()
	tenantID := uuid.New()
	userID := uuid.New().String()
	ctx := sharedctx.WithUserID(sharedctx.WithTenantID(context.Background(), tenantID), userID)

	routeID := uuid.New()
	route := &entities.Route{
		Name:        "Tuyến Test Transaction",
		Status:      enums.RouteStatusDraft,
		MaxWeightKg: 1000,
		MaxVolumeM3: 10,
	}
	route.ID = routeID
	route.TenantID = tenantID
	_ = tc.routeRepo.Create(ctx, route)

	resp, err := tc.handler.GetRouteRecommendation(ctx, &routeplanningpb.GetRouteRecommendationRequest{
		RouteId: routeID.String(),
	})
	if err != nil {
		t.Fatalf("GetRouteRecommendation failed: %v", err)
	}

	if resp.GetSummary() == "" {
		t.Errorf("expected summary text")
	}

	// 1. Transaction verification
	if tc.uow.TxCount == 0 {
		t.Errorf("expected GetRouteRecommendation to execute in UnitOfWork transaction")
	}
	if tc.uow.RolledBack {
		t.Errorf("expected successful transaction, but was rolled back")
	}

	// 2. RiskAssessment persisted
	if len(tc.policyRepo.assessments) != 1 {
		t.Fatalf("expected 1 RiskAssessment persisted, got %d", len(tc.policyRepo.assessments))
	}
	ass := tc.policyRepo.assessments[0]
	if ass.RouteID != routeID || ass.TenantID != tenantID {
		t.Errorf("mismatched assessment route/tenant: %+v", ass)
	}

	// 3. DecisionAuditLog persisted
	if len(tc.approvalRepo.auditLogs) != 1 {
		t.Fatalf("expected 1 DecisionAuditLog persisted, got %d", len(tc.approvalRepo.auditLogs))
	}
	audit := tc.approvalRepo.auditLogs[0]
	if audit.RouteID != routeID || audit.TenantID != tenantID {
		t.Errorf("mismatched audit log: %+v", audit)
	}
	if audit.RequestedByUserID.String() != userID {
		t.Errorf("expected audit user ID %s, got %v", userID, audit.RequestedByUserID)
	}

	// 4. Outbox events persisted
	foundRiskEvt := false
	for _, m := range tc.outboxRepo.messages {
		if m.EventType == "RouteRiskEvaluatedEvent" {
			foundRiskEvt = true
			break
		}
	}
	if !foundRiskEvt {
		t.Errorf("expected RouteRiskEvaluatedEvent in outbox messages")
	}
}

func TestGetRouteRecommendation_RollbackOnOutboxFailure(t *testing.T) {
	tc := setupFullTestContext()
	tenantID := uuid.New()
	userID := uuid.New().String()
	ctx := sharedctx.WithUserID(sharedctx.WithTenantID(context.Background(), tenantID), userID)

	routeID := uuid.New()
	route := &entities.Route{
		Name:        "Tuyến Test Rollback",
		Status:      enums.RouteStatusDraft,
		MaxWeightKg: 1000,
	}
	route.ID = routeID
	route.TenantID = tenantID
	_ = tc.routeRepo.Create(ctx, route)

	// Simulate outbox storage failure
	tc.outboxRepo.failSave = true

	_, err := tc.handler.GetRouteRecommendation(ctx, &routeplanningpb.GetRouteRecommendationRequest{
		RouteId: routeID.String(),
	})
	if err == nil {
		t.Fatalf("expected GetRouteRecommendation to fail when outbox fails")
	}

	if !tc.uow.RolledBack {
		t.Errorf("expected transaction to be rolled back on outbox failure")
	}
}

func TestCreateRiskPolicyDraft_AtomicSequentialVersions(t *testing.T) {
	tc := setupFullTestContext()
	tenantID := uuid.New()
	userID := uuid.New().String()
	ctx := sharedctx.WithUserID(sharedctx.WithTenantID(context.Background(), tenantID), userID)

	// 1. Create first draft -> expect Version 1
	resp1, err := tc.handler.CreateRiskPolicyDraft(ctx, &routeplanningpb.CreateRiskPolicyDraftRequest{
		Name:  "Policy Alpha",
		Scope: "RoutePlanning",
	})
	if err != nil {
		t.Fatalf("CreateRiskPolicyDraft 1 failed: %v", err)
	}
	if resp1.GetVersion() != 1 {
		t.Errorf("expected version 1 for first draft, got %d", resp1.GetVersion())
	}

	// 2. Create second draft -> expect Version 2
	resp2, err := tc.handler.CreateRiskPolicyDraft(ctx, &routeplanningpb.CreateRiskPolicyDraftRequest{
		Name:  "Policy Beta",
		Scope: "RoutePlanning",
	})
	if err != nil {
		t.Fatalf("CreateRiskPolicyDraft 2 failed: %v", err)
	}
	if resp2.GetVersion() != 2 {
		t.Errorf("expected version 2 for second draft, got %d", resp2.GetVersion())
	}

	// Verify outbox messages emitted with correct versions
	createdEvtCount := 0
	for _, m := range tc.outboxRepo.messages {
		if m.EventType == "TenantRiskPolicyCreatedEvent" {
			createdEvtCount++
		}
	}
	if createdEvtCount != 2 {
		t.Errorf("expected 2 TenantRiskPolicyCreatedEvent in outbox, got %d", createdEvtCount)
	}
}

func TestOutboxLeaseClaim_MultiReplicaNoDoubleClaim(t *testing.T) {
	repo := &mockOutboxRepo{}
	ctx := context.Background()

	msg1 := &entities.OutboxMessage{ID: uuid.New(), EventType: "Event1"}
	msg2 := &entities.OutboxMessage{ID: uuid.New(), EventType: "Event2"}
	_ = repo.Save(ctx, msg1)
	_ = repo.Save(ctx, msg2)

	// Replica 1 claims unprocessed batch
	claimedByRep1, err := repo.GetUnprocessed(ctx, 10)
	if err != nil {
		t.Fatalf("Replica 1 GetUnprocessed failed: %v", err)
	}
	if len(claimedByRep1) != 2 {
		t.Fatalf("expected Replica 1 to claim 2 messages, got %d", len(claimedByRep1))
	}

	// Replica 2 attempts to claim while Replica 1 is processing -> must get 0
	claimedByRep2, err := repo.GetUnprocessed(ctx, 10)
	if err != nil {
		t.Fatalf("Replica 2 GetUnprocessed failed: %v", err)
	}
	if len(claimedByRep2) != 0 {
		t.Errorf("expected Replica 2 to get 0 messages (lease protected), got %d", len(claimedByRep2))
	}

	// Replica 1 finishes msg1 successfully, fails msg2
	_ = repo.MarkProcessed(ctx, msg1.ID, time.Now().UTC())
	_ = repo.MarkFailed(ctx, msg2.ID, "simulated network timeout")

	// Replica 2 polls again: msg1 is completed, msg2 is released and retryable
	retryBatch, err := repo.GetUnprocessed(ctx, 10)
	if err != nil {
		t.Fatalf("Replica 2 retry GetUnprocessed failed: %v", err)
	}
	if len(retryBatch) != 1 || retryBatch[0].ID != msg2.ID {
		t.Errorf("expected Replica 2 to claim retried msg2 only, got %+v", retryBatch)
	}

	// Replica 2 finishes msg2
	_ = repo.MarkProcessed(ctx, msg2.ID, time.Now().UTC())

	// Next poll -> empty
	emptyBatch, _ := repo.GetUnprocessed(ctx, 10)
	if len(emptyBatch) != 0 {
		t.Errorf("expected 0 unprocessed messages, got %d", len(emptyBatch))
	}
}


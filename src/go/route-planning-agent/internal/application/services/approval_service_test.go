package services_test

import (
	"context"
	"testing"

	"aurora/route-planning-agent/internal/application/services"
	"aurora/route-planning-agent/internal/domain"
	"aurora/route-planning-agent/internal/domain/entities"
	"aurora/route-planning-agent/internal/domain/enums"

	"github.com/google/uuid"
)

type mockRouteRepoForApproval struct {
	routes map[uuid.UUID]*entities.Route
}

func (m *mockRouteRepoForApproval) GetByID(ctx context.Context, tenantID, id uuid.UUID) (*entities.Route, error) {
	if r, ok := m.routes[id]; ok && r.TenantID == tenantID {
		return r, nil
	}
	return nil, domain.ErrRouteNotFound
}
func (m *mockRouteRepoForApproval) Create(ctx context.Context, route *entities.Route) error {
	m.routes[route.ID] = route
	return nil
}
func (m *mockRouteRepoForApproval) Update(ctx context.Context, route *entities.Route) error {
	m.routes[route.ID] = route
	return nil
}
func (m *mockRouteRepoForApproval) UpdateWithVersion(ctx context.Context, route *entities.Route, expectedVersion int32) error {
	m.routes[route.ID] = route
	return nil
}
func (m *mockRouteRepoForApproval) Delete(ctx context.Context, tenantID, id uuid.UUID) error {
	delete(m.routes, id)
	return nil
}
func (m *mockRouteRepoForApproval) AddOptimizationHistory(ctx context.Context, h *entities.RouteOptimizationHistory) error {
	return nil
}
func (m *mockRouteRepoForApproval) List(ctx context.Context, tenantID uuid.UUID, page, limit int32, status *enums.RouteStatus) ([]entities.Route, int64, error) {
	return nil, 0, nil
}

type mockUoWForApproval struct{}

func (m *mockUoWForApproval) DoInTransaction(ctx context.Context, fn func(txCtx context.Context) error) error {
	return fn(ctx)
}

func TestApprovalServiceApproveAndReject(t *testing.T) {
	approvalRepo := &mockApprovalRepoForGov{approvals: make(map[uuid.UUID]*entities.ApprovalRequest)}
	routeRepo := &mockRouteRepoForApproval{routes: make(map[uuid.UUID]*entities.Route)}
	uow := &mockUoWForApproval{}

	approvalService := services.NewApprovalService(approvalRepo, routeRepo, uow)

	ctx := context.Background()
	tenantID := uuid.New()
	reviewerUserID := uuid.New()

	routeID := uuid.New()
	route := &entities.Route{
		Version:            1,
		Status:             enums.RouteStatusDraft,
		GovernanceDecision: enums.GovernanceDecisionManagerApprovalRequired,
	}
	route.ID = routeID
	route.TenantID = tenantID
	routeRepo.routes[routeID] = route

	t.Run("Create approval request", func(t *testing.T) {
		appr, err := approvalService.Create(
			ctx, routeID, "Overweight violation", "AI Summary", nil,
			1, "policy-1", 1, tenantID,
		)
		if err != nil {
			t.Fatalf("unexpected error creating approval: %v", err)
		}
		if appr.Status != enums.ApprovalStatusPending {
			t.Errorf("expected Pending status, got %v", appr.Status)
		}
	})

	t.Run("Approve transitions route to Ready and StaffAllowed", func(t *testing.T) {
		appr, err := approvalService.Create(
			ctx, routeID, "Overweight", "Summary", nil,
			1, "policy-1", 1, tenantID,
		)
		if err != nil {
			t.Fatalf("failed to create: %v", err)
		}

		comment := "Approved by manager"
		approved, err := approvalService.Approve(ctx, appr.ID, reviewerUserID, tenantID, &comment)
		if err != nil {
			t.Fatalf("unexpected error on approve: %v", err)
		}

		if approved.Status != enums.ApprovalStatusApproved {
			t.Errorf("expected Approved status, got %v", approved.Status)
		}
		if routeRepo.routes[routeID].Status != enums.RouteStatusReady {
			t.Errorf("expected route status Ready, got %v", routeRepo.routes[routeID].Status)
		}
		if routeRepo.routes[routeID].GovernanceDecision != enums.GovernanceDecisionStaffAllowed {
			t.Errorf("expected route decision StaffAllowed, got %v", routeRepo.routes[routeID].GovernanceDecision)
		}

		// Double approve should fail
		_, err = approvalService.Approve(ctx, appr.ID, reviewerUserID, tenantID, &comment)
		if err != domain.ErrApprovalAlreadyReviewed {
			t.Errorf("expected ErrApprovalAlreadyReviewed, got %v", err)
		}
	})

	t.Run("Reject transitions route to Draft and ManagerApprovalRequired", func(t *testing.T) {
		appr, err := approvalService.Create(
			ctx, routeID, "Overweight", "Summary", nil,
			1, "policy-1", 1, tenantID,
		)
		if err != nil {
			t.Fatalf("failed to create: %v", err)
		}

		reason := "Safety violation: truck too heavy"
		comment := "Rejected"
		rejected, err := approvalService.Reject(ctx, appr.ID, reviewerUserID, tenantID, reason, &comment)
		if err != nil {
			t.Fatalf("unexpected error on reject: %v", err)
		}

		if rejected.Status != enums.ApprovalStatusRejected {
			t.Errorf("expected Rejected status, got %v", rejected.Status)
		}
		if routeRepo.routes[routeID].Status != enums.RouteStatusDraft {
			t.Errorf("expected route status Draft, got %v", routeRepo.routes[routeID].Status)
		}
		if routeRepo.routes[routeID].GovernanceDecision != enums.GovernanceDecisionManagerApprovalRequired {
			t.Errorf("expected route decision ManagerApprovalRequired, got %v", routeRepo.routes[routeID].GovernanceDecision)
		}

		// Double reject should fail
		_, err = approvalService.Reject(ctx, appr.ID, reviewerUserID, tenantID, reason, &comment)
		if err != domain.ErrApprovalAlreadyReviewed {
			t.Errorf("expected ErrApprovalAlreadyReviewed, got %v", err)
		}
	})
}

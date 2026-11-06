package enums_test

import (
	"testing"

	"aurora/route-planning-agent/internal/domain/enums"
)

func TestEnumsOrdinalParity(t *testing.T) {
	// Kiểm tra tính tương thích ordinal chính xác với .NET EF Core

	// RouteStatus
	if enums.RouteStatusDraft != 0 {
		t.Errorf("RouteStatusDraft expected 0, got %d", enums.RouteStatusDraft)
	}
	if enums.RouteStatusOptimizing != 1 {
		t.Errorf("RouteStatusOptimizing expected 1, got %d", enums.RouteStatusOptimizing)
	}
	if enums.RouteStatusReady != 2 {
		t.Errorf("RouteStatusReady expected 2, got %d", enums.RouteStatusReady)
	}
	if enums.RouteStatusActive != 3 {
		t.Errorf("RouteStatusActive expected 3, got %d", enums.RouteStatusActive)
	}
	if enums.RouteStatusCompleted != 4 {
		t.Errorf("RouteStatusCompleted expected 4, got %d", enums.RouteStatusCompleted)
	}
	if enums.RouteStatusCancelled != 5 {
		t.Errorf("RouteStatusCancelled expected 5, got %d", enums.RouteStatusCancelled)
	}
	if enums.RouteStatusArchived != 6 {
		t.Errorf("RouteStatusArchived expected 6, got %d", enums.RouteStatusArchived)
	}

	// RouteRiskLevel
	if enums.RouteRiskLevelLow != 0 || enums.RouteRiskLevelMedium != 1 ||
		enums.RouteRiskLevelHigh != 2 || enums.RouteRiskLevelCritical != 3 {
		t.Errorf("RouteRiskLevel mismatch with .NET ordinals")
	}

	// TenantRiskPolicyStatus (bắt đầu từ 1 trong .NET)
	if enums.TenantRiskPolicyStatusDraft != 1 ||
		enums.TenantRiskPolicyStatusPendingReview != 2 ||
		enums.TenantRiskPolicyStatusActive != 3 ||
		enums.TenantRiskPolicyStatusSuperseded != 4 ||
		enums.TenantRiskPolicyStatusRejected != 5 {
		t.Errorf("TenantRiskPolicyStatus mismatch with .NET ordinals")
	}

	// GovernanceDecision
	if enums.GovernanceDecisionNoApprovalRequired != 0 ||
		enums.GovernanceDecisionStaffAllowed != 1 ||
		enums.GovernanceDecisionManagerApprovalRequired != 2 ||
		enums.GovernanceDecisionBlocked != 3 {
		t.Errorf("GovernanceDecision mismatch with .NET ordinals")
	}
}


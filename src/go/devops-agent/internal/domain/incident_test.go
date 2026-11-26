package domain_test

import (
	"testing"

	"aurora/devops-agent/internal/domain"
)

func TestIncidentStateMachine_ValidAndInvalidTransitions(t *testing.T) {
	inc := &domain.Incident{
		Status:   domain.IncidentStatusNew,
		Severity: domain.SeverityHigh,
	}

	steps := []domain.IncidentStatus{
		domain.IncidentStatusCollectingContext,
		domain.IncidentStatusContextReady,
		domain.IncidentStatusAiAnalysis,
		domain.IncidentStatusRecommendationReady,
		domain.IncidentStatusWaitingApproval,
		domain.IncidentStatusApproved,
		domain.IncidentStatusExecuting,
		domain.IncidentStatusVerifying,
		domain.IncidentStatusResolved,
		domain.IncidentStatusClosed,
	}
	for _, next := range steps {
		if err := inc.TransitionTo(next); err != nil {
			t.Fatalf("expected valid transition to %s, got err: %v", next, err)
		}
	}

	inc2 := &domain.Incident{Status: domain.IncidentStatusNew, Severity: domain.SeverityHigh}
	if err := inc2.TransitionTo(domain.IncidentStatusApproved); err == nil {
		t.Fatalf("expected invalid transition NEW -> APPROVED to fail")
	}
}

func TestIncidentStateMachine_LowSeverityNoLlmGuard(t *testing.T) {
	inc := &domain.Incident{
		Status:   domain.IncidentStatusContextReady,
		Severity: domain.SeverityLow,
	}
	if err := inc.TransitionTo(domain.IncidentStatusAiAnalysis); err == nil {
		t.Fatalf("expected LOW severity incident entering AI_ANALYSIS to fail")
	}
	if err := inc.TransitionTo(domain.IncidentStatusRuleAnalysis); err != nil {
		t.Fatalf("expected LOW severity -> RULE_ANALYSIS to succeed, got %v", err)
	}
}

func TestIncident_EscalateSeverityDisallowDowngrade(t *testing.T) {
	inc := &domain.Incident{}
	if err := inc.EscalateSeverity(domain.SeverityLow); err != nil {
		t.Fatalf("unexpected err: %v", err)
	}
	if err := inc.EscalateSeverity(domain.SeverityHigh); err != nil {
		t.Fatalf("expected upgrade Low -> High to succeed, got %v", err)
	}
	if err := inc.EscalateSeverity(domain.SeverityMedium); err == nil {
		t.Fatalf("expected downgrade High -> Medium to fail")
	}
}

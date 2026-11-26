package domain

type Severity string

const (
	SeverityLow      Severity = "Low"
	SeverityMedium   Severity = "Medium"
	SeverityHigh     Severity = "High"
	SeverityCritical Severity = "Critical"
)

func (s Severity) Weight() int {
	switch s {
	case SeverityLow:
		return 1
	case SeverityMedium:
		return 2
	case SeverityHigh:
		return 3
	case SeverityCritical:
		return 4
	default:
		return 0
	}
}

type IncidentStatus string

const (
	IncidentStatusNew                 IncidentStatus = "NEW"
	IncidentStatusCollectingContext   IncidentStatus = "COLLECTING_CONTEXT"
	IncidentStatusContextReady        IncidentStatus = "CONTEXT_READY"
	IncidentStatusContextBuildFailed  IncidentStatus = "CONTEXT_BUILD_FAILED"
	IncidentStatusRuleAnalysis        IncidentStatus = "RULE_ANALYSIS"
	IncidentStatusAiAnalysis          IncidentStatus = "AI_ANALYSIS"
	IncidentStatusAiAnalysisFailed    IncidentStatus = "AI_ANALYSIS_FAILED"
	IncidentStatusRoutingFailed       IncidentStatus = "ROUTING_FAILED"
	IncidentStatusRecommendationReady IncidentStatus = "RECOMMENDATION_READY"
	IncidentStatusWaitingApproval     IncidentStatus = "WAITING_APPROVAL"
	IncidentStatusApproved            IncidentStatus = "APPROVED"
	IncidentStatusExecuting           IncidentStatus = "EXECUTING"
	IncidentStatusVerifying           IncidentStatus = "VERIFYING"
	IncidentStatusExecutionFailed     IncidentStatus = "EXECUTION_FAILED"
	IncidentStatusResolved            IncidentStatus = "RESOLVED"
	IncidentStatusIgnored             IncidentStatus = "IGNORED"
	IncidentStatusCancelled           IncidentStatus = "CANCELLED"
	IncidentStatusClosed              IncidentStatus = "CLOSED"
)

type AuditActionType string

const (
	AuditActionIncidentCreated      AuditActionType = "INCIDENT_CREATED"
	AuditActionIncidentDetected     AuditActionType = "INCIDENT_DETECTED"
	AuditActionRcaAnalysisStarted   AuditActionType = "RCA_ANALYSIS_STARTED"
	AuditActionRcaAnalysisCompleted AuditActionType = "RCA_ANALYSIS_COMPLETED"
	AuditActionRcaAnalysisFailed    AuditActionType = "RCA_ANALYSIS_FAILED"
	AuditActionApprovalGranted      AuditActionType = "APPROVAL_GRANTED"
	AuditActionApprovalRejected     AuditActionType = "APPROVAL_REJECTED"
	AuditActionRuleApplied          AuditActionType = "RULE_APPLIED"
	AuditActionRulePromoted         AuditActionType = "RULE_PROMOTED"
	AuditActionRuleRejected         AuditActionType = "RULE_REJECTED"
	AuditActionSelfConfigUpdated    AuditActionType = "SELF_CONFIG_UPDATED"
	AuditActionActionExecuted       AuditActionType = "ACTION_EXECUTED"
	AuditActionActionBlocked        AuditActionType = "ACTION_BLOCKED"
)

type ApprovalStatus string

const (
	ApprovalStatusPending  ApprovalStatus = "PENDING"
	ApprovalStatusApproved ApprovalStatus = "APPROVED"
	ApprovalStatusRejected ApprovalStatus = "REJECTED"
	ApprovalStatusExpired  ApprovalStatus = "EXPIRED"
)

type ApprovalStage string

const (
	ApprovalStagePlan  ApprovalStage = "PLAN"
	ApprovalStageMerge ApprovalStage = "MERGE"
)

type RcaAnalysisType string

const (
	RcaAnalysisTypeInfrastructure RcaAnalysisType = "INFRASTRUCTURE"
	RcaAnalysisTypeApplication    RcaAnalysisType = "APPLICATION"
	RcaAnalysisTypeDatabase       RcaAnalysisType = "DATABASE"
	RcaAnalysisTypeNetwork        RcaAnalysisType = "NETWORK"
)

type RcaAnalysisStatus string

const (
	RcaAnalysisStatusPending   RcaAnalysisStatus = "PENDING"
	RcaAnalysisStatusRunning   RcaAnalysisStatus = "RUNNING"
	RcaAnalysisStatusCompleted RcaAnalysisStatus = "COMPLETED"
	RcaAnalysisStatusFailed    RcaAnalysisStatus = "FAILED"
	RcaAnalysisStatusCancelled RcaAnalysisStatus = "CANCELLED"
)

type ActionType string

const (
	ActionTypeRestartPod   ActionType = "RESTART_POD"
	ActionTypeClearCache   ActionType = "CLEAR_CACHE"
	ActionTypeRollback     ActionType = "ROLLBACK_RELEASE"
	ActionTypeAdjustConfig ActionType = "ADJUST_CONFIG"
)

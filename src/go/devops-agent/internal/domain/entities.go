package domain

import (
	"fmt"
	"time"

	"github.com/google/uuid"
)

var incidentTransitions = map[IncidentStatus]map[IncidentStatus]struct{}{
	IncidentStatusNew:                 {IncidentStatusCollectingContext: {}, IncidentStatusIgnored: {}, IncidentStatusCancelled: {}},
	IncidentStatusCollectingContext:   {IncidentStatusContextReady: {}, IncidentStatusContextBuildFailed: {}, IncidentStatusCancelled: {}},
	IncidentStatusContextReady:        {IncidentStatusRuleAnalysis: {}, IncidentStatusAiAnalysis: {}, IncidentStatusRoutingFailed: {}, IncidentStatusCancelled: {}},
	IncidentStatusRuleAnalysis:        {IncidentStatusRecommendationReady: {}, IncidentStatusAiAnalysis: {}, IncidentStatusCancelled: {}},
	IncidentStatusAiAnalysis:          {IncidentStatusRecommendationReady: {}, IncidentStatusAiAnalysisFailed: {}, IncidentStatusCancelled: {}},
	IncidentStatusRecommendationReady: {IncidentStatusWaitingApproval: {}, IncidentStatusApproved: {}, IncidentStatusCancelled: {}},
	IncidentStatusWaitingApproval:     {IncidentStatusApproved: {}, IncidentStatusCancelled: {}, IncidentStatusRecommendationReady: {}},
	IncidentStatusApproved:            {IncidentStatusExecuting: {}, IncidentStatusCancelled: {}},
	IncidentStatusExecuting:           {IncidentStatusVerifying: {}, IncidentStatusExecutionFailed: {}},
	IncidentStatusVerifying:           {IncidentStatusResolved: {}, IncidentStatusExecutionFailed: {}},
	IncidentStatusResolved:            {IncidentStatusClosed: {}},
	IncidentStatusIgnored:             {IncidentStatusClosed: {}},
	IncidentStatusCancelled:           {IncidentStatusClosed: {}},
	IncidentStatusContextBuildFailed:  {IncidentStatusCollectingContext: {}, IncidentStatusClosed: {}},
	IncidentStatusRoutingFailed:       {IncidentStatusContextReady: {}, IncidentStatusClosed: {}},
	IncidentStatusAiAnalysisFailed:    {IncidentStatusAiAnalysis: {}, IncidentStatusClosed: {}},
	IncidentStatusExecutionFailed:     {IncidentStatusApproved: {}, IncidentStatusClosed: {}},
}

type BaseAuditable struct {
	ID        uuid.UUID `gorm:"type:uuid;primaryKey;column:id"`
	CreatedAt time.Time `gorm:"column:created_at;not null"`
	UpdatedAt time.Time `gorm:"column:updated_at;not null"`
	CreatedBy *string   `gorm:"column:created_by;size:100"`
	UpdatedBy *string   `gorm:"column:updated_by;size:100"`
	Version   int64     `gorm:"column:version;not null;default:0"`
}

type Incident struct {
	BaseAuditable
	CorrelationID            string         `gorm:"column:correlation_id;uniqueIndex;size:64;not null"`
	DedupKey                 string         `gorm:"column:dedup_key;size:64;not null"`
	Source                   string         `gorm:"column:source;size:50;not null"`
	ErrorSignature           string         `gorm:"column:error_signature;type:text;not null"`
	EvidenceJson             string         `gorm:"column:evidence_json;type:text;not null;default:'{}'"`
	Severity                 Severity       `gorm:"column:severity;size:20;not null"`
	OriginalSeverity         Severity       `gorm:"column:original_severity;size:20;not null"`
	Status                   IncidentStatus `gorm:"column:status;size:50;not null"`
	FlapCount                int            `gorm:"column:flap_count;not null;default:0"`
	AffectedService          string         `gorm:"column:affected_service;size:100"`
	AffectedTenantID         *uuid.UUID     `gorm:"type:uuid;column:affected_tenant_id"`
	ImpactScore              float64        `gorm:"column:impact_score;type:numeric(5,2);not null;default:0.00"`
	RcaRootCause             string         `gorm:"column:rca_root_cause;type:text"`
	RcaRecommendation        string         `gorm:"column:rca_recommendation;type:text"`
	SelectedRecommendationID *uuid.UUID     `gorm:"type:uuid;column:selected_recommendation_id"`
}

func (Incident) TableName() string { return "incidents" }

func (i *Incident) TransitionTo(target IncidentStatus) error {
	if i.Status == target {
		return nil
	}
	allowed, ok := incidentTransitions[i.Status]
	if !ok {
		return fmt.Errorf("invalid transition: %s -> %s", i.Status, target)
	}
	if _, valid := allowed[target]; !valid {
		return fmt.Errorf("invalid transition: %s -> %s", i.Status, target)
	}
	if target == IncidentStatusAiAnalysis && i.Severity == SeverityLow {
		return fmt.Errorf("LOW severity incidents must not enter AI_ANALYSIS (no-LLM guard). Route to RULE_ANALYSIS instead")
	}
	i.Status = target
	return nil
}

func (i *Incident) EscalateSeverity(newSeverity Severity) error {
	if i.Severity != "" && newSeverity != "" && newSeverity.Weight() < i.Severity.Weight() {
		return fmt.Errorf("cannot downgrade severity from %s to %s", i.Severity, newSeverity)
	}
	i.Severity = newSeverity
	if i.OriginalSeverity == "" {
		i.OriginalSeverity = newSeverity
	}
	return nil
}

type RcaAnalysis struct {
	BaseAuditable
	IncidentID            uuid.UUID         `gorm:"type:uuid;column:incident_id;not null"`
	CorrelationID         string            `gorm:"column:correlation_id;size:64;not null"`
	AnalysisType          RcaAnalysisType   `gorm:"column:analysis_type;size:50;not null"`
	Status                RcaAnalysisStatus `gorm:"column:status;size:50;not null"`
	RecommendationJson    string            `gorm:"column:recommendation_json;type:jsonb"`
	RecommendationVersion int               `gorm:"column:recommendation_version;not null;default:1"`
	Confidence            *float64          `gorm:"column:confidence;type:numeric(5,4)"`
	LlmTokensUsed         int               `gorm:"column:llm_tokens_used"`
	ContextQualityScore   *float64          `gorm:"column:context_quality_score;type:numeric(3,2)"`
	WarningFlagsJson      *string           `gorm:"column:warning_flags_json;type:jsonb"`
	DurationMs            *int64            `gorm:"column:duration_ms"`
	RagAugmented          bool              `gorm:"column:rag_augmented;not null;default:true"`
	RagFailureReason      *string           `gorm:"column:rag_failure_reason;size:255"`
	GovernanceDecisionID  string            `gorm:"column:governance_decision_id;size:100"`
	AutomationLevel       string            `gorm:"column:automation_level;size:50"`
	RequiresApproval      bool              `gorm:"column:requires_approval;not null;default:false"`
	InputTokens           int64             `gorm:"column:input_tokens;not null;default:0"`
	OutputTokens          int64             `gorm:"column:output_tokens;not null;default:0"`
}

func (RcaAnalysis) TableName() string { return "rca_analyses" }

type ExistingRule struct {
	BaseAuditable
	RuleName         string     `gorm:"column:rule_name;uniqueIndex;size:100;not null"`
	ErrorPattern     string     `gorm:"column:error_pattern;type:text;not null"`
	ActionType       string     `gorm:"column:action_type;size:50;not null"`
	ActionParamsJson string     `gorm:"column:action_params_json;type:jsonb;default:'{}'"`
	Confidence       float64    `gorm:"column:confidence;type:numeric(5,4);not null;default:1.0000"`
	Active           bool       `gorm:"column:active;not null;default:true"`
	MatchCount       int        `gorm:"column:match_count;not null;default:0"`
	LastMatchedAt    *time.Time `gorm:"column:last_matched_at"`
}

func (ExistingRule) TableName() string { return "existing_rules" }

type PendingRule struct {
	BaseAuditable
	ProposedRuleName     string     `gorm:"column:proposed_rule_name;size:100;not null"`
	ErrorPattern         string     `gorm:"column:error_pattern;type:text;not null"`
	ActionType           string     `gorm:"column:action_type;size:50;not null"`
	ActionParamsJson     string     `gorm:"column:action_params_json;type:jsonb;default:'{}'"`
	SourceIncidentID     *uuid.UUID `gorm:"type:uuid;column:source_incident_id"`
	Status               string     `gorm:"column:status;size:50;not null;default:'PENDING'"`
	GovernanceDecisionID string     `gorm:"column:governance_decision_id;size:100"`
	AutomationLevel      string     `gorm:"column:automation_level;size:50"`
	RequiresApproval     bool       `gorm:"column:requires_approval;not null;default:false"`
}

func (PendingRule) TableName() string { return "pending_rules" }

type DevOpsAgentSelfConfig struct {
	BaseAuditable
	ModelProvider           *string `gorm:"column:model_provider;size:50"`
	ModelName               *string `gorm:"column:model_name;size:100"`
	ApiEndpoint             *string `gorm:"column:api_endpoint;type:text"`
	MaxTokensPerRequest     int32   `gorm:"column:max_tokens_per_request;not null;default:4096"`
	AlertThresholdUsdPerDay float64 `gorm:"column:alert_threshold_usd_per_day;type:numeric(10,4);not null;default:50.0000"`
}

func (DevOpsAgentSelfConfig) TableName() string { return "devops_agent_self_config" }

type PrApprovalRecord struct {
	BaseAuditable
	IncidentID       uuid.UUID      `gorm:"type:uuid;column:incident_id;not null"`
	Incident         *Incident      `gorm:"foreignKey:IncidentID;references:ID"`
	RecommendationID uuid.UUID      `gorm:"type:uuid;column:recommendation_id;not null"`
	GithubPrUrl      string         `gorm:"column:github_pr_url;size:500"`
	PrNumber         *int           `gorm:"column:pr_number"`
	Status           ApprovalStatus `gorm:"column:status;size:50;not null;default:'PENDING'"`
	Stage            ApprovalStage  `gorm:"column:stage;size:50;not null;default:'MERGE'"`
	Severity         Severity       `gorm:"column:severity;size:20;not null"`
	DecidedBy        *string        `gorm:"column:decided_by;size:100"`
	DecisionReason   *string        `gorm:"column:decision_reason;type:text"`
	DecidedAt        *time.Time     `gorm:"column:decided_at"`
	TimeoutMinutes   int            `gorm:"column:timeout_minutes;not null"`
	ExpiresAt        time.Time      `gorm:"column:expires_at;not null"`
}

func (PrApprovalRecord) TableName() string { return "pr_approval_records" }

type RuleApprovalRecord struct {
	BaseAuditable
	PendingRuleID uuid.UUID      `gorm:"type:uuid;column:pending_rule_id;not null"`
	Status        ApprovalStatus `gorm:"column:status;size:50;not null;default:'PENDING'"`
	ReviewedBy    *string        `gorm:"column:reviewed_by;size:100"`
	ReviewComment *string        `gorm:"column:review_comment;type:text"`
	ReviewedAt    *time.Time     `gorm:"column:reviewed_at"`
}

func (RuleApprovalRecord) TableName() string { return "rule_approval_records" }

type AuditEventOutbox struct {
	BaseAuditable
	CorrelationID string          `gorm:"column:correlation_id;size:64;not null"`
	IncidentID    *uuid.UUID      `gorm:"type:uuid;column:incident_id"`
	ActionType    AuditActionType `gorm:"column:action_type;size:100;not null"`
	Actor         string          `gorm:"column:actor;size:100;not null"`
	PayloadJson   string          `gorm:"column:payload_json;type:jsonb;not null"`
	Processed     bool            `gorm:"column:processed;not null;default:false"`
	ProcessedAt   *time.Time      `gorm:"column:processed_at"`
	RetryCount    int             `gorm:"column:retry_count;not null;default:0"`
	LastError     *string         `gorm:"column:last_error;type:text"`
}

func (AuditEventOutbox) TableName() string { return "audit_event_outbox" }

type ServiceCriticalityRegistry struct {
	BaseAuditable
	ServiceName            string   `gorm:"column:service_name;uniqueIndex;size:100;not null"`
	CriticalityTier        string   `gorm:"column:criticality_tier;size:20;not null"`
	Weight                 float64  `gorm:"column:weight;type:numeric(3,2);not null;default:1.00"`
	OwnerTeam              *string  `gorm:"column:owner_team;size:100"`
	SloAvailabilityPercent *float64 `gorm:"column:slo_availability_percent;type:numeric(5,2)"`
	SloLatencyMs           *int     `gorm:"column:slo_latency_ms"`
}

func (ServiceCriticalityRegistry) TableName() string { return "service_criticality_registry" }

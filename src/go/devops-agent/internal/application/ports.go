package application

import (
	"context"
	"time"

	"aurora/devops-agent/internal/domain"

	"github.com/google/uuid"
)

type GenerateCommand struct {
	CapabilityCode       string
	Prompt               string
	MaxOutputTokens      int64
	EstimatedInputTokens int64
	Parameters           map[string]string
}

type GenerateResult struct {
	Content          string
	InputTokens      int64
	OutputTokens     int64
	DecisionID       string
	AutomationLevel  string
	RequiresApproval bool
	Model            string
	Provider         string
}

type AiGovernanceClient interface {
	Generate(ctx context.Context, cmd GenerateCommand) (GenerateResult, error)
}

type KnowledgeSnippet struct {
	EntryID        string
	Title          string
	Content        string
	RelevanceScore float64
}

type RagRetrievalResult struct {
	Snippets      []KnowledgeSnippet
	Success       bool
	FailureReason string
}

type DevOpsRagClient interface {
	QueryKnowledge(ctx context.Context, redacted domain.RedactedIncidentContext) RagRetrievalResult
}

type IncidentRepository interface {
	Save(ctx context.Context, incident *domain.Incident) error
	FindByCorrelationID(ctx context.Context, correlationID string) (*domain.Incident, error)
	List(ctx context.Context, severity string, page, size int32) ([]*domain.Incident, int64, error)
	FindAndLockByStatus(ctx context.Context, status domain.IncidentStatus, limit int) ([]*domain.Incident, error)
}

type RcaAnalysisRepository interface {
	Save(ctx context.Context, analysis *domain.RcaAnalysis) error
}

type ExistingRuleRepository interface {
	Save(ctx context.Context, rule *domain.ExistingRule) error
	FindByID(ctx context.Context, id uuid.UUID) (*domain.ExistingRule, error)
	DeleteByID(ctx context.Context, id uuid.UUID) error
	List(ctx context.Context, page, size int32) ([]*domain.ExistingRule, int64, error)
}

type PendingRuleRepository interface {
	Save(ctx context.Context, rule *domain.PendingRule) error
	FindByID(ctx context.Context, id uuid.UUID) (*domain.PendingRule, error)
	ListByStatus(ctx context.Context, status string, page, size int32) ([]*domain.PendingRule, int64, error)
}

type SelfConfigRepository interface {
	FindFirst(ctx context.Context) (*domain.DevOpsAgentSelfConfig, error)
	Save(ctx context.Context, cfg *domain.DevOpsAgentSelfConfig) error
}

type PrApprovalRecordRepository interface {
	FindByStatusAndExpiresAtBefore(ctx context.Context, status domain.ApprovalStatus, before time.Time) ([]*domain.PrApprovalRecord, error)
	Save(ctx context.Context, record *domain.PrApprovalRecord) error
}

type OutboxRepository interface {
	Enqueue(ctx context.Context, correlationID string, incidentID *uuid.UUID, actionType domain.AuditActionType, actor, payloadJson string) error
	FindAndLockPendingBatch(ctx context.Context, limit int) ([]*domain.AuditEventOutbox, error)
	Save(ctx context.Context, item *domain.AuditEventOutbox) error
}

type UnitOfWork interface {
	WithTransaction(ctx context.Context, fn func(txCtx context.Context) error) error
}

type DedupStore interface {
	ComputeDedupKey(source, errorSignature string, timestampSeconds int64) string
	CheckAndStore(ctx context.Context, dedupKey, correlationID string) (existingCorrelationID string, isDuplicate bool, err error)
}

type AntiFlappingStore interface {
	RecordEventAndCheckFlapping(ctx context.Context, errorSignature string) (bool, error)
}

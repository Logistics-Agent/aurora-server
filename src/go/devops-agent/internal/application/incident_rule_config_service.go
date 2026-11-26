package application

import (
	"context"
	"encoding/json"
	"errors"
	"strings"
	"time"

	"aurora/devops-agent/internal/domain"
	sharedctx "aurora/shared/context"

	"github.com/google/uuid"
)

var (
	ErrForbidden       = errors.New("forbidden: SYSTEM_ADMIN role required")
	ErrNotFound        = errors.New("resource not found")
	ErrInvalidArgument = errors.New("invalid argument")
)

type IncidentService struct {
	incidentRepo IncidentRepository
	outboxRepo   OutboxRepository
	uow          UnitOfWork
}

func NewIncidentService(incidentRepo IncidentRepository, outboxRepo OutboxRepository, uow UnitOfWork) *IncidentService {
	return &IncidentService{
		incidentRepo: incidentRepo,
		outboxRepo:   outboxRepo,
		uow:          uow,
	}
}

func (s *IncidentService) GetByCorrelationID(ctx context.Context, correlationID string) (*domain.Incident, error) {
	return s.incidentRepo.FindByCorrelationID(ctx, correlationID)
}

func (s *IncidentService) List(ctx context.Context, severity string, page, size int32) ([]*domain.Incident, int64, error) {
	return s.incidentRepo.List(ctx, severity, page, size)
}

func (s *IncidentService) Approve(ctx context.Context, correlationID, comment string) (*domain.Incident, error) {
	var updated *domain.Incident
	err := s.uow.WithTransaction(ctx, func(txCtx context.Context) error {
		inc, err := s.incidentRepo.FindByCorrelationID(txCtx, correlationID)
		if err != nil {
			return err
		}
		if inc == nil {
			return ErrNotFound
		}
		if err := inc.TransitionTo(domain.IncidentStatusApproved); err != nil {
			return err
		}
		inc.UpdatedAt = time.Now().UTC()
		if err := s.incidentRepo.Save(txCtx, inc); err != nil {
			return err
		}
		payload, _ := json.Marshal(map[string]string{"comment": comment})
		if err := s.outboxRepo.Enqueue(txCtx, inc.CorrelationID, &inc.ID, domain.AuditActionApprovalGranted, resolveActor(txCtx), string(payload)); err != nil {
			return err
		}
		updated = inc
		return nil
	})
	return updated, err
}

func (s *IncidentService) Reject(ctx context.Context, correlationID, reason string) (*domain.Incident, error) {
	var updated *domain.Incident
	err := s.uow.WithTransaction(ctx, func(txCtx context.Context) error {
		inc, err := s.incidentRepo.FindByCorrelationID(txCtx, correlationID)
		if err != nil {
			return err
		}
		if inc == nil {
			return ErrNotFound
		}
		if err := inc.TransitionTo(domain.IncidentStatusCancelled); err != nil {
			return err
		}
		inc.UpdatedAt = time.Now().UTC()
		if err := s.incidentRepo.Save(txCtx, inc); err != nil {
			return err
		}
		payload, _ := json.Marshal(map[string]string{"rejectionReason": reason})
		if err := s.outboxRepo.Enqueue(txCtx, inc.CorrelationID, &inc.ID, domain.AuditActionApprovalRejected, resolveActor(txCtx), string(payload)); err != nil {
			return err
		}
		updated = inc
		return nil
	})
	return updated, err
}

type CreateRuleCommand struct {
	Name                  string
	ErrorSignaturePattern string
	TargetService         string
	TargetDeployment      string
	ActionType            string
	ActionParamsJson      string
	ScopeConstraintJson   string
}

type UpdateRuleCommand struct {
	ID                    string
	Name                  string
	ErrorSignaturePattern string
	TargetService         string
	ActionType            string
	ActionParamsJson      string
}

type RuleService struct {
	existingRepo ExistingRuleRepository
	pendingRepo  PendingRuleRepository
	outboxRepo   OutboxRepository
	uow          UnitOfWork
}

func NewRuleService(
	existingRepo ExistingRuleRepository,
	pendingRepo PendingRuleRepository,
	outboxRepo OutboxRepository,
	uow UnitOfWork,
) *RuleService {
	return &RuleService{
		existingRepo: existingRepo,
		pendingRepo:  pendingRepo,
		outboxRepo:   outboxRepo,
		uow:          uow,
	}
}

func (s *RuleService) ListExisting(ctx context.Context, page, size int32) ([]*domain.ExistingRule, int64, error) {
	return s.existingRepo.List(ctx, page, size)
}

func (s *RuleService) CreateRule(ctx context.Context, cmd CreateRuleCommand) (*domain.ExistingRule, error) {
	if !isSystemAdmin(ctx) {
		return nil, ErrForbidden
	}

	now := time.Now().UTC()
	params := cmd.ActionParamsJson
	if strings.TrimSpace(params) == "" {
		params = "{}"
	}
	rule := &domain.ExistingRule{
		BaseAuditable: domain.BaseAuditable{
			ID:        uuid.New(),
			CreatedAt: now,
			UpdatedAt: now,
			Version:   0,
		},
		RuleName:         cmd.Name,
		ErrorPattern:     cmd.ErrorSignaturePattern,
		ActionType:       cmd.ActionType,
		ActionParamsJson: params,
		Confidence:       1.0,
		Active:           true,
	}

	err := s.uow.WithTransaction(ctx, func(txCtx context.Context) error {
		if err := s.existingRepo.Save(txCtx, rule); err != nil {
			return err
		}
		payload, _ := json.Marshal(map[string]string{
			"ruleId":   rule.ID.String(),
			"ruleName": rule.RuleName,
		})
		return s.outboxRepo.Enqueue(txCtx, rule.ID.String(), nil, domain.AuditActionRuleApplied, resolveActor(txCtx), string(payload))
	})
	if err != nil {
		return nil, err
	}
	return rule, nil
}

func (s *RuleService) UpdateRule(ctx context.Context, cmd UpdateRuleCommand) (*domain.ExistingRule, error) {
	id, err := uuid.Parse(cmd.ID)
	if err != nil {
		return nil, ErrInvalidArgument
	}

	var updated *domain.ExistingRule
	err = s.uow.WithTransaction(ctx, func(txCtx context.Context) error {
		rule, err := s.existingRepo.FindByID(txCtx, id)
		if err != nil {
			return err
		}
		if rule == nil {
			return ErrNotFound
		}
		rule.RuleName = cmd.Name
		rule.ErrorPattern = cmd.ErrorSignaturePattern
		rule.ActionType = cmd.ActionType
		if strings.TrimSpace(cmd.ActionParamsJson) != "" {
			rule.ActionParamsJson = cmd.ActionParamsJson
		}
		rule.UpdatedAt = time.Now().UTC()
		if err := s.existingRepo.Save(txCtx, rule); err != nil {
			return err
		}
		updated = rule
		return nil
	})
	return updated, err
}

func (s *RuleService) DeleteRule(ctx context.Context, idStr string) error {
	id, err := uuid.Parse(idStr)
	if err != nil {
		return ErrInvalidArgument
	}
	return s.existingRepo.DeleteByID(ctx, id)
}

func (s *RuleService) ListPending(ctx context.Context, page, size int32) ([]*domain.PendingRule, int64, error) {
	return s.pendingRepo.ListByStatus(ctx, "PENDING", page, size)
}

func (s *RuleService) ApprovePending(ctx context.Context, pendingRuleID string) error {
	id, err := uuid.Parse(pendingRuleID)
	if err != nil {
		return ErrInvalidArgument
	}

	return s.uow.WithTransaction(ctx, func(txCtx context.Context) error {
		pr, err := s.pendingRepo.FindByID(txCtx, id)
		if err != nil {
			return err
		}
		if pr == nil {
			return ErrNotFound
		}

		now := time.Now().UTC()
		pr.Status = "APPROVED"
		pr.UpdatedAt = now
		if err := s.pendingRepo.Save(txCtx, pr); err != nil {
			return err
		}

		params := pr.ActionParamsJson
		if strings.TrimSpace(params) == "" {
			params = "{}"
		}
		er := &domain.ExistingRule{
			BaseAuditable: domain.BaseAuditable{
				ID:        uuid.New(),
				CreatedAt: now,
				UpdatedAt: now,
				Version:   0,
			},
			RuleName:         pr.ProposedRuleName,
			ErrorPattern:     pr.ErrorPattern,
			ActionType:       pr.ActionType,
			ActionParamsJson: params,
			Confidence:       1.0,
			Active:           true,
		}
		if err := s.existingRepo.Save(txCtx, er); err != nil {
			return err
		}

		payload, _ := json.Marshal(map[string]string{
			"pendingRuleId":  pr.ID.String(),
			"promotedRuleId": er.ID.String(),
		})
		return s.outboxRepo.Enqueue(txCtx, er.ID.String(), nil, domain.AuditActionRulePromoted, resolveActor(txCtx), string(payload))
	})
}

func (s *RuleService) RejectPending(ctx context.Context, pendingRuleID, reason string) error {
	id, err := uuid.Parse(pendingRuleID)
	if err != nil {
		return ErrInvalidArgument
	}

	return s.uow.WithTransaction(ctx, func(txCtx context.Context) error {
		pr, err := s.pendingRepo.FindByID(txCtx, id)
		if err != nil {
			return err
		}
		if pr == nil {
			return ErrNotFound
		}

		pr.Status = "REJECTED"
		pr.UpdatedAt = time.Now().UTC()
		if err := s.pendingRepo.Save(txCtx, pr); err != nil {
			return err
		}

		payload, _ := json.Marshal(map[string]string{
			"pendingRuleId": pr.ID.String(),
			"reason":        reason,
		})
		return s.outboxRepo.Enqueue(txCtx, pr.ID.String(), nil, domain.AuditActionRuleRejected, resolveActor(txCtx), string(payload))
	})
}

type UpdateSelfConfigCommand struct {
	ModelProvider           string
	ModelName               string
	ApiEndpoint             string
	MaxTokensPerRequest     int32
	AlertThresholdUsdPerDay float64
}

type SelfConfigService struct {
	repo       SelfConfigRepository
	outboxRepo OutboxRepository
	uow        UnitOfWork
}

func NewSelfConfigService(repo SelfConfigRepository, outboxRepo OutboxRepository, uow UnitOfWork) *SelfConfigService {
	return &SelfConfigService{repo: repo, outboxRepo: outboxRepo, uow: uow}
}

func (s *SelfConfigService) Get(ctx context.Context) (*domain.DevOpsAgentSelfConfig, error) {
	cfg, err := s.repo.FindFirst(ctx)
	if err != nil {
		return nil, err
	}
	if cfg != nil {
		return cfg, nil
	}
	return &domain.DevOpsAgentSelfConfig{
		MaxTokensPerRequest:     4096,
		AlertThresholdUsdPerDay: 50.0000,
	}, nil
}

func (s *SelfConfigService) Update(ctx context.Context, cmd UpdateSelfConfigCommand) (*domain.DevOpsAgentSelfConfig, error) {
	if !isSystemAdmin(ctx) {
		return nil, ErrForbidden
	}

	var saved *domain.DevOpsAgentSelfConfig
	err := s.uow.WithTransaction(ctx, func(txCtx context.Context) error {
		cfg, err := s.repo.FindFirst(txCtx)
		if err != nil {
			return err
		}
		now := time.Now().UTC()
		if cfg == nil {
			cfg = &domain.DevOpsAgentSelfConfig{
				BaseAuditable: domain.BaseAuditable{
					ID:        uuid.New(),
					CreatedAt: now,
					UpdatedAt: now,
				},
			}
		}
		if cmd.ModelProvider != "" {
			v := cmd.ModelProvider
			cfg.ModelProvider = &v
		}
		if cmd.ModelName != "" {
			v := cmd.ModelName
			cfg.ModelName = &v
		}
		if cmd.ApiEndpoint != "" {
			v := cmd.ApiEndpoint
			cfg.ApiEndpoint = &v
		}
		cfg.MaxTokensPerRequest = cmd.MaxTokensPerRequest
		cfg.AlertThresholdUsdPerDay = cmd.AlertThresholdUsdPerDay
		cfg.UpdatedAt = now
		if err := s.repo.Save(txCtx, cfg); err != nil {
			return err
		}

		payload, _ := json.Marshal(map[string]any{
			"maxTokens":      cfg.MaxTokensPerRequest,
			"alertThreshold": cfg.AlertThresholdUsdPerDay,
		})
		if err := s.outboxRepo.Enqueue(txCtx, cfg.ID.String(), nil, domain.AuditActionSelfConfigUpdated, resolveActor(txCtx), string(payload)); err != nil {
			return err
		}
		saved = cfg
		return nil
	})
	return saved, err
}

func isSystemAdmin(ctx context.Context) bool {
	role, ok := sharedctx.GetUserRole(ctx)
	if !ok {
		return false
	}
	for _, part := range strings.Split(role, ",") {
		if strings.EqualFold(strings.TrimSpace(part), "SYSTEM_ADMIN") {
			return true
		}
	}
	return false
}

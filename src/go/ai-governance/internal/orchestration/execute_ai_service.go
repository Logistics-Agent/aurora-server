package orchestration

import (
	"context"
	"time"

	gwapp "aurora/ai-governance/internal/gateway/application"
	gwdomain "aurora/ai-governance/internal/gateway/domain"
	govapp "aurora/ai-governance/internal/governance/application"
	govdomain "aurora/ai-governance/internal/governance/domain"
	"aurora/ai-governance/internal/governance/infrastructure/messaging"
	"aurora/ai-governance/internal/metrics"
	"aurora/ai-governance/internal/shared"

	"github.com/google/uuid"
)

type GenerateAiCommand struct {
	TenantID        *uuid.UUID
	UserID          *uuid.UUID
	CallerServiceID string
	CapabilityCode  string
	Prompt          string
	TokenBudget     shared.TokenBudget
	Parameters      map[string]string
	InputParts      []gwdomain.MultimodalPart
}

type EmbedAiCommand struct {
	TenantID             *uuid.UUID
	UserID               *uuid.UUID
	CallerServiceID      string
	CapabilityCode       string
	Content              string
	Dimensions           *int32
	EstimatedInputTokens int64
}

type GovernedGenerateResult struct {
	Result   *gwdomain.AiGenerateResult
	Decision govdomain.GovernanceDecision
}

type GovernedEmbedResult struct {
	Result   *gwdomain.AiEmbeddingResult
	Decision govdomain.GovernanceDecision
}

type UsagePublisher interface {
	PublishUsageEvent(ctx context.Context, event messaging.AiUsageEvent)
}

type ExecuteAiService struct {
	policyService    *govapp.GovernancePolicyService
	executionService *gwapp.AiExecutionService
	auditPort        govapp.PolicyAuditPort
	usagePublisher   UsagePublisher
	metrics          *metrics.Metrics
}

func NewExecuteAiService(
	policyService *govapp.GovernancePolicyService,
	executionService *gwapp.AiExecutionService,
	auditPort govapp.PolicyAuditPort,
	usagePublisher UsagePublisher,
	m *metrics.Metrics,
) *ExecuteAiService {
	return &ExecuteAiService{
		policyService:    policyService,
		executionService: executionService,
		auditPort:        auditPort,
		usagePublisher:   usagePublisher,
		metrics:          m,
	}
}

func (s *ExecuteAiService) Generate(ctx context.Context, cmd GenerateAiCommand) (GovernedGenerateResult, error) {
	start := time.Now()
	outcome := "success"
	defer func() {
		if s.metrics != nil {
			s.metrics.RequestDuration.WithLabelValues("generate", outcome).Observe(time.Since(start).Seconds())
		}
	}()

	decision := s.policyService.Evaluate(
		ctx,
		cmd.TenantID,
		cmd.CallerServiceID,
		cmd.CapabilityCode,
		shared.AiOperationGenerate,
		cmd.TokenBudget,
	)

	s.publishAudit(ctx, decision, cmd.TenantID, cmd.CallerServiceID, cmd.CapabilityCode, shared.AiOperationGenerate)

	if !decision.Allowed {
		outcome = "denied"
		if s.metrics != nil {
			reason := ""
			if decision.DenyReason != nil {
				reason = string(*decision.DenyReason)
			}
			s.metrics.PolicyDeniedTotal.WithLabelValues(cmd.CapabilityCode, reason).Inc()
		}
		return GovernedGenerateResult{Decision: decision}, nil
	}

	if s.metrics != nil {
		s.metrics.PolicyAllowedTotal.WithLabelValues(cmd.CapabilityCode, cmd.CallerServiceID).Inc()
	}

	req := gwdomain.AiGenerateRequest{
		CapabilityCode:       cmd.CapabilityCode,
		Prompt:               cmd.Prompt,
		MaxOutputTokens:      cmd.TokenBudget.MaxOutputTokens,
		EstimatedInputTokens: cmd.TokenBudget.EstimatedInputTokens,
		Parameters:           cmd.Parameters,
		InputParts:           cmd.InputParts,
	}

	res, err := s.executionService.Generate(ctx, decision, req, cmd.CallerServiceID, cmd.TokenBudget)
	if err != nil {
		outcome = "error"
		if s.metrics != nil {
			s.metrics.RequestsFailedTotal.WithLabelValues("generate").Inc()
		}
		return GovernedGenerateResult{}, err
	}

	if s.metrics != nil {
		s.metrics.GatewayRequestsTotal.WithLabelValues("generate", res.Provider, cmd.CallerServiceID, "success").Inc()
	}

	if s.usagePublisher != nil {
		s.usagePublisher.PublishUsageEvent(ctx, messaging.AiUsageEvent{
			EventID:         uuid.NewString(),
			TenantID:        cmd.TenantID,
			UserID:          cmd.UserID,
			CallerServiceID: cmd.CallerServiceID,
			CapabilityCode:  cmd.CapabilityCode,
			Operation:       shared.AiOperationGenerate,
			Provider:        res.Provider,
			Model:           res.Model,
			InputTokens:     res.InputTokens,
			OutputTokens:    res.OutputTokens,
			DurationMs:      time.Since(start).Milliseconds(),
			Success:         true,
			Timestamp:       time.Now().UTC(),
		})
	}

	return GovernedGenerateResult{Result: &res, Decision: decision}, nil
}

func (s *ExecuteAiService) Embed(ctx context.Context, cmd EmbedAiCommand) (GovernedEmbedResult, error) {
	start := time.Now()
	outcome := "success"
	defer func() {
		if s.metrics != nil {
			s.metrics.RequestDuration.WithLabelValues("embed", outcome).Observe(time.Since(start).Seconds())
		}
	}()

	embedBudget := shared.ForEmbedding(cmd.EstimatedInputTokens)
	decision := s.policyService.Evaluate(
		ctx,
		cmd.TenantID,
		cmd.CallerServiceID,
		cmd.CapabilityCode,
		shared.AiOperationEmbed,
		embedBudget,
	)

	s.publishAudit(ctx, decision, cmd.TenantID, cmd.CallerServiceID, cmd.CapabilityCode, shared.AiOperationEmbed)

	if !decision.Allowed {
		outcome = "denied"
		if s.metrics != nil {
			reason := ""
			if decision.DenyReason != nil {
				reason = string(*decision.DenyReason)
			}
			s.metrics.PolicyDeniedTotal.WithLabelValues(cmd.CapabilityCode, reason).Inc()
		}
		return GovernedEmbedResult{Decision: decision}, nil
	}

	if s.metrics != nil {
		s.metrics.PolicyAllowedTotal.WithLabelValues(cmd.CapabilityCode, cmd.CallerServiceID).Inc()
	}

	req := gwdomain.AiEmbeddingRequest{
		CapabilityCode:       cmd.CapabilityCode,
		Content:              cmd.Content,
		Dimensions:           cmd.Dimensions,
		EstimatedInputTokens: cmd.EstimatedInputTokens,
	}

	res, err := s.executionService.Embed(ctx, decision, req, cmd.CallerServiceID, embedBudget)
	if err != nil {
		outcome = "error"
		if s.metrics != nil {
			s.metrics.RequestsFailedTotal.WithLabelValues("embed").Inc()
		}
		return GovernedEmbedResult{}, err
	}

	if s.metrics != nil {
		s.metrics.GatewayRequestsTotal.WithLabelValues("embed", res.Provider, cmd.CallerServiceID, "success").Inc()
	}

	if s.usagePublisher != nil {
		s.usagePublisher.PublishUsageEvent(ctx, messaging.AiUsageEvent{
			EventID:         uuid.NewString(),
			TenantID:        cmd.TenantID,
			UserID:          cmd.UserID,
			CallerServiceID: cmd.CallerServiceID,
			CapabilityCode:  cmd.CapabilityCode,
			Operation:       shared.AiOperationEmbed,
			Provider:        res.Provider,
			Model:           res.Model,
			InputTokens:     res.InputTokens,
			OutputTokens:    0,
			DurationMs:      time.Since(start).Milliseconds(),
			Success:         true,
			Timestamp:       time.Now().UTC(),
		})
	}

	return GovernedEmbedResult{Result: &res, Decision: decision}, nil
}

func (s *ExecuteAiService) publishAudit(
	ctx context.Context,
	decision govdomain.GovernanceDecision,
	tenantID *uuid.UUID,
	callerServiceID string,
	capabilityCode string,
	op shared.AiOperation,
) {
	if s.auditPort == nil {
		return
	}
	tenantStr := "anonymous"
	if tenantID != nil && *tenantID != uuid.Nil {
		tenantStr = tenantID.String()
	}
	var denyReason *string
	if decision.DenyReason != nil {
		r := string(*decision.DenyReason)
		denyReason = &r
	}
	s.auditPort.PublishPolicyDecision(ctx, govapp.PolicyAuditEvent{
		DecisionID:      decision.DecisionID,
		TenantID:        tenantStr,
		CallerServiceID: callerServiceID,
		CapabilityCode:  capabilityCode,
		Operation:       string(op),
		Allowed:         decision.Allowed,
		DenyReason:      denyReason,
		PolicyVersion:   decision.PolicyVersion,
		TimestampMillis: time.Now().UTC().UnixMilli(),
	})
}

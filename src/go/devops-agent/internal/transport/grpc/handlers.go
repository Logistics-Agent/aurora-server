package grpc

import (
	"context"
	"errors"
	"math"
	"time"

	"aurora/devops-agent/internal/application"
	"aurora/devops-agent/internal/domain"
	"aurora/devops-agent/proto/devopsagentpb"

	"github.com/google/uuid"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/status"
)

type IngestionGrpcHandler struct {
	devopsagentpb.UnimplementedDevOpsIngestionServiceServer
	handler *application.IngestAlertHandler
}

func NewIngestionGrpcHandler(handler *application.IngestAlertHandler) *IngestionGrpcHandler {
	return &IngestionGrpcHandler{handler: handler}
}

func (h *IngestionGrpcHandler) IngestAlert(ctx context.Context, req *devopsagentpb.IngestAlertRequest) (*devopsagentpb.IngestAlertResponse, error) {
	res, err := h.handler.Handle(ctx, application.IngestAlertCommand{
		Source:          req.GetSource(),
		ErrorSignature:  req.GetErrorSignature(),
		PayloadJson:     req.GetPayloadJson(),
		AffectedService: req.GetAffectedService(),
		Environment:     req.GetEnvironment(),
	})
	if err != nil {
		return nil, status.Error(codes.Internal, err.Error())
	}
	return &devopsagentpb.IngestAlertResponse{
		Duplicated:    res.Duplicated,
		CorrelationId: res.CorrelationID,
		IncidentId:    res.IncidentID,
		Status:        res.Status,
	}, nil
}

type IncidentGrpcHandler struct {
	devopsagentpb.UnimplementedDevOpsIncidentServiceServer
	service *application.IncidentService
}

func NewIncidentGrpcHandler(service *application.IncidentService) *IncidentGrpcHandler {
	return &IncidentGrpcHandler{service: service}
}

func (h *IncidentGrpcHandler) GetIncident(ctx context.Context, req *devopsagentpb.GetIncidentRequest) (*devopsagentpb.IncidentResponse, error) {
	inc, err := h.service.GetByCorrelationID(ctx, req.GetCorrelationId())
	if err != nil {
		return nil, status.Error(codes.Internal, err.Error())
	}
	if inc == nil {
		return nil, status.Errorf(codes.NotFound, "Incident not found for correlationId: %s", req.GetCorrelationId())
	}
	return &devopsagentpb.IncidentResponse{
		Id:                inc.ID.String(),
		CorrelationId:     inc.CorrelationID,
		Source:            inc.Source,
		ErrorSignature:    inc.ErrorSignature,
		Severity:          string(inc.Severity),
		OriginalSeverity:  string(inc.OriginalSeverity),
		Status:            string(inc.Status),
		ImpactScore:       inc.ImpactScore,
		AffectedService:   inc.AffectedService,
		RcaRootCause:      inc.RcaRootCause,
		RcaRecommendation: inc.RcaRecommendation,
		CreatedAt:         inc.CreatedAt.Format(time.RFC3339),
	}, nil
}

func (h *IncidentGrpcHandler) ListIncidents(ctx context.Context, req *devopsagentpb.ListIncidentsRequest) (*devopsagentpb.ListIncidentsResponse, error) {
	items, total, err := h.service.List(ctx, req.GetSeverity(), req.GetPageNumber(), req.GetPageSize())
	if err != nil {
		return nil, status.Error(codes.Internal, err.Error())
	}
	size := req.GetPageSize()
	if size <= 0 {
		size = 10
	}
	totalPages := int32(math.Ceil(float64(total) / float64(size)))
	summaries := make([]*devopsagentpb.IncidentSummary, 0, len(items))
	for _, inc := range items {
		summaries = append(summaries, &devopsagentpb.IncidentSummary{
			Id:             inc.ID.String(),
			CorrelationId:  inc.CorrelationID,
			Source:         inc.Source,
			ErrorSignature: inc.ErrorSignature,
			Severity:       string(inc.Severity),
			Status:         string(inc.Status),
			ImpactScore:    inc.ImpactScore,
			CreatedAt:      inc.CreatedAt.Format(time.RFC3339),
		})
	}
	return &devopsagentpb.ListIncidentsResponse{
		Incidents:     summaries,
		TotalElements: int32(total),
		TotalPages:    totalPages,
		CurrentPage:   req.GetPageNumber(),
	}, nil
}

func (h *IncidentGrpcHandler) ApproveIncident(ctx context.Context, req *devopsagentpb.ApproveIncidentRequest) (*devopsagentpb.ApprovalOperationResponse, error) {
	inc, err := h.service.Approve(ctx, req.GetCorrelationId(), req.GetComment())
	if err != nil {
		return nil, mapAppError(err, "Incident not found for correlationId: "+req.GetCorrelationId())
	}
	return &devopsagentpb.ApprovalOperationResponse{
		Success:       true,
		CorrelationId: inc.CorrelationID,
		NewStatus:     string(inc.Status),
		Message:       "Incident approved: " + req.GetComment(),
	}, nil
}

func (h *IncidentGrpcHandler) RejectIncident(ctx context.Context, req *devopsagentpb.RejectIncidentRequest) (*devopsagentpb.ApprovalOperationResponse, error) {
	inc, err := h.service.Reject(ctx, req.GetCorrelationId(), req.GetRejectionReason())
	if err != nil {
		return nil, mapAppError(err, "Incident not found for correlationId: "+req.GetCorrelationId())
	}
	return &devopsagentpb.ApprovalOperationResponse{
		Success:       true,
		CorrelationId: inc.CorrelationID,
		NewStatus:     string(inc.Status),
		Message:       "Incident rejected: " + req.GetRejectionReason(),
	}, nil
}

type RuleGrpcHandler struct {
	devopsagentpb.UnimplementedDevOpsRuleServiceServer
	service *application.RuleService
}

func NewRuleGrpcHandler(service *application.RuleService) *RuleGrpcHandler {
	return &RuleGrpcHandler{service: service}
}

func (h *RuleGrpcHandler) ListExistingRules(ctx context.Context, req *devopsagentpb.ListRulesRequest) (*devopsagentpb.ListExistingRulesResponse, error) {
	items, total, err := h.service.ListExisting(ctx, req.GetPageNumber(), req.GetPageSize())
	if err != nil {
		return nil, status.Error(codes.Internal, err.Error())
	}
	size := req.GetPageSize()
	if size <= 0 {
		size = 10
	}
	rules := make([]*devopsagentpb.ExistingRuleSummary, 0, len(items))
	for _, r := range items {
		rules = append(rules, &devopsagentpb.ExistingRuleSummary{
			Id:                    r.ID.String(),
			Name:                  r.RuleName,
			ErrorSignaturePattern: r.ErrorPattern,
			ActionType:            r.ActionType,
			CreatedAt:             r.CreatedAt.Format(time.RFC3339),
		})
	}
	return &devopsagentpb.ListExistingRulesResponse{
		Rules:         rules,
		TotalElements: int32(total),
		TotalPages:    int32(math.Ceil(float64(total) / float64(size))),
		CurrentPage:   req.GetPageNumber(),
	}, nil
}

func (h *RuleGrpcHandler) CreateRule(ctx context.Context, req *devopsagentpb.CreateRuleRequest) (*devopsagentpb.RuleResponse, error) {
	rule, err := h.service.CreateRule(ctx, application.CreateRuleCommand{
		Name:                  req.GetName(),
		ErrorSignaturePattern: req.GetErrorSignaturePattern(),
		TargetService:         req.GetTargetService(),
		TargetDeployment:      req.GetTargetDeployment(),
		ActionType:            req.GetActionType(),
		ActionParamsJson:      req.GetActionParamsJson(),
		ScopeConstraintJson:   req.GetScopeConstraintJson(),
	})
	if err != nil {
		return nil, mapAppError(err, "")
	}
	return &devopsagentpb.RuleResponse{
		Id:                    rule.ID.String(),
		Name:                  rule.RuleName,
		ErrorSignaturePattern: rule.ErrorPattern,
		ActionType:            rule.ActionType,
		CreatedAt:             rule.CreatedAt.Format(time.RFC3339),
	}, nil
}

func (h *RuleGrpcHandler) UpdateRule(ctx context.Context, req *devopsagentpb.UpdateRuleRequest) (*devopsagentpb.RuleResponse, error) {
	rule, err := h.service.UpdateRule(ctx, application.UpdateRuleCommand{
		ID:                    req.GetId(),
		Name:                  req.GetName(),
		ErrorSignaturePattern: req.GetErrorSignaturePattern(),
		TargetService:         req.GetTargetService(),
		ActionType:            req.GetActionType(),
		ActionParamsJson:      req.GetActionParamsJson(),
	})
	if err != nil {
		return nil, mapAppError(err, "Rule not found")
	}
	return &devopsagentpb.RuleResponse{
		Id:                    rule.ID.String(),
		Name:                  rule.RuleName,
		ErrorSignaturePattern: rule.ErrorPattern,
		ActionType:            rule.ActionType,
		CreatedAt:             rule.CreatedAt.Format(time.RFC3339),
	}, nil
}

func (h *RuleGrpcHandler) DeleteRule(ctx context.Context, req *devopsagentpb.DeleteRuleRequest) (*devopsagentpb.EmptyDevOpsResponse, error) {
	if err := h.service.DeleteRule(ctx, req.GetId()); err != nil {
		return nil, mapAppError(err, "")
	}
	return &devopsagentpb.EmptyDevOpsResponse{}, nil
}

func (h *RuleGrpcHandler) ListPendingRules(ctx context.Context, req *devopsagentpb.ListRulesRequest) (*devopsagentpb.ListPendingRulesResponse, error) {
	items, total, err := h.service.ListPending(ctx, req.GetPageNumber(), req.GetPageSize())
	if err != nil {
		return nil, status.Error(codes.Internal, err.Error())
	}
	size := req.GetPageSize()
	if size <= 0 {
		size = 10
	}
	rules := make([]*devopsagentpb.PendingRuleSummary, 0, len(items))
	for _, pr := range items {
		rules = append(rules, &devopsagentpb.PendingRuleSummary{
			Id:             pr.ID.String(),
			ErrorSignature: pr.ErrorPattern,
			ProposedAction: pr.ActionType,
			Status:         pr.Status,
			CreatedAt:      pr.CreatedAt.Format(time.RFC3339),
		})
	}
	return &devopsagentpb.ListPendingRulesResponse{
		Rules:         rules,
		TotalElements: int32(total),
		TotalPages:    int32(math.Ceil(float64(total) / float64(size))),
		CurrentPage:   req.GetPageNumber(),
	}, nil
}

func (h *RuleGrpcHandler) ApprovePendingRule(ctx context.Context, req *devopsagentpb.ApproveRuleRequest) (*devopsagentpb.ApprovalOperationResponse, error) {
	if err := h.service.ApprovePending(ctx, req.GetPendingRuleId()); err != nil {
		return nil, mapAppError(err, "Pending rule not found")
	}
	return &devopsagentpb.ApprovalOperationResponse{
		Success:   true,
		NewStatus: "APPROVED",
		Message:   "Pending rule approved and promoted to active rule.",
	}, nil
}

func (h *RuleGrpcHandler) RejectPendingRule(ctx context.Context, req *devopsagentpb.RejectRuleRequest) (*devopsagentpb.ApprovalOperationResponse, error) {
	if err := h.service.RejectPending(ctx, req.GetPendingRuleId(), req.GetRejectionReason()); err != nil {
		return nil, mapAppError(err, "Pending rule not found")
	}
	return &devopsagentpb.ApprovalOperationResponse{
		Success:   true,
		NewStatus: "REJECTED",
		Message:   "Pending rule rejected: " + req.GetRejectionReason(),
	}, nil
}

type SelfConfigGrpcHandler struct {
	devopsagentpb.UnimplementedDevOpsConfigServiceServer
	service *application.SelfConfigService
}

func NewSelfConfigGrpcHandler(service *application.SelfConfigService) *SelfConfigGrpcHandler {
	return &SelfConfigGrpcHandler{service: service}
}

func (h *SelfConfigGrpcHandler) GetSelfConfig(ctx context.Context, _ *devopsagentpb.EmptyDevOpsRequest) (*devopsagentpb.SelfConfigResponse, error) {
	cfg, err := h.service.Get(ctx)
	if err != nil {
		return nil, status.Error(codes.Internal, err.Error())
	}
	return toSelfConfigResponse(cfg), nil
}

func (h *SelfConfigGrpcHandler) UpdateSelfConfig(ctx context.Context, req *devopsagentpb.UpdateSelfConfigRequest) (*devopsagentpb.SelfConfigResponse, error) {
	cfg, err := h.service.Update(ctx, application.UpdateSelfConfigCommand{
		ModelProvider:           req.GetModelProvider(),
		ModelName:               req.GetModelName(),
		ApiEndpoint:             req.GetApiEndpoint(),
		MaxTokensPerRequest:     req.GetMaxTokensPerRequest(),
		AlertThresholdUsdPerDay: req.GetAlertThresholdUsdPerDay(),
	})
	if err != nil {
		return nil, mapAppError(err, "")
	}
	return toSelfConfigResponse(cfg), nil
}

func toSelfConfigResponse(cfg *domain.DevOpsAgentSelfConfig) *devopsagentpb.SelfConfigResponse {
	resp := &devopsagentpb.SelfConfigResponse{
		MaxTokensPerRequest:     cfg.MaxTokensPerRequest,
		AlertThresholdUsdPerDay: cfg.AlertThresholdUsdPerDay,
	}
	if cfg.ID != uuid.Nil {
		resp.Id = cfg.ID.String()
	}
	if cfg.UpdatedBy != nil {
		resp.UpdatedBy = *cfg.UpdatedBy
	}
	if !cfg.UpdatedAt.IsZero() {
		resp.UpdatedAt = cfg.UpdatedAt.Format(time.RFC3339)
	}
	if cfg.ModelProvider != nil {
		resp.ModelProvider = *cfg.ModelProvider
	}
	if cfg.ModelName != nil {
		resp.ModelName = *cfg.ModelName
	}
	if cfg.ApiEndpoint != nil {
		resp.ApiEndpoint = *cfg.ApiEndpoint
	}
	return resp
}

func mapAppError(err error, notFoundMsg string) error {
	switch {
	case errors.Is(err, application.ErrForbidden):
		return status.Error(codes.PermissionDenied, err.Error())
	case errors.Is(err, application.ErrInvalidArgument):
		return status.Error(codes.InvalidArgument, err.Error())
	case errors.Is(err, application.ErrNotFound):
		if notFoundMsg != "" {
			return status.Error(codes.NotFound, notFoundMsg)
		}
		return status.Error(codes.NotFound, err.Error())
	default:
		return status.Error(codes.Internal, err.Error())
	}
}

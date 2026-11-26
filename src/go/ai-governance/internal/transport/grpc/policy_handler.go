package grpc

import (
	"context"
	"regexp"
	"sort"
	"strings"

	govapp "aurora/ai-governance/internal/governance/application"
	"aurora/ai-governance/internal/shared"
	"aurora/ai-governance/proto/aigovpb"
	sharedctx "aurora/shared/context"

	"github.com/google/uuid"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/status"
)

const (
	maxCapabilityCodeLength = 100
	maxTokenBudget          = int64(1_000_000)
)

var capabilityCodeRegex = regexp.MustCompile(`^[A-Za-z0-9._-]+$`)

type PolicyGrpcHandler struct {
	aigovpb.UnimplementedAiGovernanceServiceServer
	policyService *govapp.GovernancePolicyService
}

func NewPolicyGrpcHandler(policyService *govapp.GovernancePolicyService) *PolicyGrpcHandler {
	return &PolicyGrpcHandler{policyService: policyService}
}

func (h *PolicyGrpcHandler) ExecutePolicy(ctx context.Context, req *aigovpb.ExecutePolicyRequest) (*aigovpb.ExecutePolicyResponse, error) {
	serviceID, ok := sharedctx.GetServiceID(ctx)
	if !ok || strings.TrimSpace(serviceID) == "" {
		return nil, status.Error(codes.Unauthenticated, "Missing required x-service-id metadata header")
	}

	if err := validateCapability(req.GetCapabilityCode()); err != nil {
		return nil, status.Error(codes.InvalidArgument, err.Error())
	}
	if err := validateTokenBudget(req.GetEstimatedInputTokens(), req.GetMaxOutputTokens()); err != nil {
		return nil, status.Error(codes.InvalidArgument, err.Error())
	}

	var tenantPtr *uuid.UUID
	if tid, hasTenant := sharedctx.GetTenantID(ctx); hasTenant {
		tenantPtr = &tid
	}

	budget := shared.NewTokenBudget(req.GetEstimatedInputTokens(), req.GetMaxOutputTokens())
	decision := h.policyService.Evaluate(ctx, tenantPtr, serviceID, req.GetCapabilityCode(), shared.AiOperationGenerate, budget)

	resp := &aigovpb.ExecutePolicyResponse{
		Allowed:          decision.Allowed,
		DecisionId:       decision.DecisionID,
		RequiresApproval: decision.RequireApproval,
	}
	if decision.DenyReason != nil {
		resp.DenyReason = string(*decision.DenyReason)
	}
	if decision.Allowed {
		providers := make([]string, 0, len(decision.AllowedProviders))
		for p := range decision.AllowedProviders {
			providers = append(providers, string(p))
		}
		sort.Strings(providers)
		resp.AllowedProviders = providers
		resp.ModelTier = string(decision.ModelTier)
		resp.MaxTokens = decision.MaxTokens
		resp.AutomationLevel = string(decision.AutomationLevel)
		resp.PolicyVersion = decision.PolicyVersion
	}

	return resp, nil
}

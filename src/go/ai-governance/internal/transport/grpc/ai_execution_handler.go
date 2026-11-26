package grpc

import (
	"context"
	"errors"
	"log/slog"
	"strings"

	gwdomain "aurora/ai-governance/internal/gateway/domain"
	"aurora/ai-governance/internal/orchestration"
	"aurora/ai-governance/internal/shared"
	"aurora/ai-governance/proto/aigovpb"
	sharedctx "aurora/shared/context"

	"github.com/google/uuid"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/status"
)

const (
	maxPromptLength        = 100_000
	maxContentLength       = 100_000
	maxEmbeddingDimensions = int32(4_096)
)

type AiExecutionGrpcHandler struct {
	aigovpb.UnimplementedAiExecutionServiceServer
	executeAiService *orchestration.ExecuteAiService
	logger           *slog.Logger
}

func NewAiExecutionGrpcHandler(executeAiService *orchestration.ExecuteAiService, logger *slog.Logger) *AiExecutionGrpcHandler {
	if logger == nil {
		logger = slog.Default()
	}
	return &AiExecutionGrpcHandler{
		executeAiService: executeAiService,
		logger:           logger,
	}
}

func (h *AiExecutionGrpcHandler) Generate(ctx context.Context, req *aigovpb.AiGenerateRequest) (*aigovpb.AiGenerateResponse, error) {
	serviceID, ok := sharedctx.GetServiceID(ctx)
	if !ok || strings.TrimSpace(serviceID) == "" {
		return nil, status.Error(codes.Unauthenticated, "Missing required x-service-id metadata header")
	}

	if err := validateGenerateRequest(req); err != nil {
		return nil, status.Error(codes.InvalidArgument, err.Error())
	}

	tenantPtr, userPtr := extractOptionalIDs(ctx)

	params := make(map[string]string, len(req.GetParameters()))
	for k, v := range req.GetParameters() {
		params[k] = v
	}

	inputParts := make([]gwdomain.MultimodalPart, 0, len(req.GetInputParts()))
	for _, part := range req.GetInputParts() {
		switch v := part.Value.(type) {
		case *aigovpb.AiInputPart_Text:
			inputParts = append(inputParts, gwdomain.TextPart(v.Text))
		case *aigovpb.AiInputPart_File:
			if v.File == nil {
				continue
			}
			ref := v.File.GetStorageReference()
			if isUnsafeStorageRef(ref) {
				return nil, status.Errorf(codes.InvalidArgument, "Invalid or unsafe storage reference: %s", ref)
			}
			inputParts = append(inputParts, gwdomain.FilePart(ref, v.File.GetMimeType(), v.File.GetFileName(), 0))
			params["storage_reference"] = ref
			params["mime_type"] = v.File.GetMimeType()
			params["file_name"] = v.File.GetFileName()
		}
	}

	cmd := orchestration.GenerateAiCommand{
		TenantID:        tenantPtr,
		UserID:          userPtr,
		CallerServiceID: serviceID,
		CapabilityCode:  req.GetCapabilityCode(),
		Prompt:          req.GetPrompt(),
		TokenBudget:     shared.NewTokenBudget(req.GetEstimatedInputTokens(), req.GetMaxOutputTokens()),
		Parameters:      params,
		InputParts:      inputParts,
	}

	res, err := h.executeAiService.Generate(ctx, cmd)
	if err != nil {
		h.logger.Error("Generation failed", slog.String("serviceId", serviceID), slog.String("capability", req.GetCapabilityCode()), slog.Any("error", err))
		return nil, mapExecutionFailure("AI generation", err)
	}

	if !res.Decision.Allowed {
		reason := ""
		if res.Decision.DenyReason != nil {
			reason = string(*res.Decision.DenyReason)
		}
		return nil, status.Errorf(codes.PermissionDenied, "Governance policy denied: %s", reason)
	}

	return &aigovpb.AiGenerateResponse{
		Content:          res.Result.Content,
		InputTokens:      res.Result.InputTokens,
		OutputTokens:     res.Result.OutputTokens,
		DecisionId:       res.Decision.DecisionID,
		AutomationLevel:  string(res.Decision.AutomationLevel),
		RequiresApproval: res.Decision.RequireApproval,
		Model:            res.Result.Model,
		Provider:         res.Result.Provider,
	}, nil
}

func (h *AiExecutionGrpcHandler) Embed(ctx context.Context, req *aigovpb.AiEmbedRequest) (*aigovpb.AiEmbedResponse, error) {
	serviceID, ok := sharedctx.GetServiceID(ctx)
	if !ok || strings.TrimSpace(serviceID) == "" {
		return nil, status.Error(codes.Unauthenticated, "Missing required x-service-id metadata header")
	}

	if err := validateEmbedRequest(req); err != nil {
		return nil, status.Error(codes.InvalidArgument, err.Error())
	}

	tenantPtr, userPtr := extractOptionalIDs(ctx)

	var dims *int32
	if req.GetDimensions() > 0 {
		d := req.GetDimensions()
		dims = &d
	}

	cmd := orchestration.EmbedAiCommand{
		TenantID:             tenantPtr,
		UserID:               userPtr,
		CallerServiceID:      serviceID,
		CapabilityCode:       req.GetCapabilityCode(),
		Content:              req.GetContent(),
		Dimensions:           dims,
		EstimatedInputTokens: req.GetEstimatedInputTokens(),
	}

	res, err := h.executeAiService.Embed(ctx, cmd)
	if err != nil {
		h.logger.Error("Embedding failed", slog.String("serviceId", serviceID), slog.String("capability", req.GetCapabilityCode()), slog.Any("error", err))
		return nil, mapExecutionFailure("AI embedding", err)
	}

	if !res.Decision.Allowed {
		reason := ""
		if res.Decision.DenyReason != nil {
			reason = string(*res.Decision.DenyReason)
		}
		return nil, status.Errorf(codes.PermissionDenied, "Governance policy denied: %s", reason)
	}

	return &aigovpb.AiEmbedResponse{
		Vector:      res.Result.Vector,
		InputTokens: res.Result.InputTokens,
		DecisionId:  res.Decision.DecisionID,
		Model:       res.Result.Model,
		Provider:    res.Result.Provider,
	}, nil
}

func extractOptionalIDs(ctx context.Context) (*uuid.UUID, *uuid.UUID) {
	var tenantPtr *uuid.UUID
	if tid, ok := sharedctx.GetTenantID(ctx); ok {
		tenantPtr = &tid
	}
	var userPtr *uuid.UUID
	if uidStr, ok := sharedctx.GetUserID(ctx); ok {
		if parsed, err := uuid.Parse(uidStr); err == nil && parsed != uuid.Nil {
			userPtr = &parsed
		}
	}
	return tenantPtr, userPtr
}

func validateGenerateRequest(req *aigovpb.AiGenerateRequest) error {
	if err := validateCapability(req.GetCapabilityCode()); err != nil {
		return err
	}
	if strings.TrimSpace(req.GetPrompt()) == "" {
		return errors.New("Prompt is required.")
	}
	if len(req.GetPrompt()) > maxPromptLength {
		return errors.New("Prompt exceeds the maximum supported length.")
	}
	return validateTokenBudget(req.GetEstimatedInputTokens(), req.GetMaxOutputTokens())
}

func validateEmbedRequest(req *aigovpb.AiEmbedRequest) error {
	if err := validateCapability(req.GetCapabilityCode()); err != nil {
		return err
	}
	if strings.TrimSpace(req.GetContent()) == "" {
		return errors.New("Content is required.")
	}
	if len(req.GetContent()) > maxContentLength {
		return errors.New("Content exceeds the maximum supported length.")
	}
	if req.GetEstimatedInputTokens() < 0 || req.GetEstimatedInputTokens() > maxTokenBudget {
		return errors.New("Estimated input tokens are outside the supported range.")
	}
	if req.GetDimensions() < 0 || req.GetDimensions() > maxEmbeddingDimensions {
		return errors.New("Embedding dimensions are outside the supported range.")
	}
	return nil
}

func validateCapability(code string) error {
	if strings.TrimSpace(code) == "" || len(code) > maxCapabilityCodeLength || !capabilityCodeRegex.MatchString(code) {
		return errors.New("Capability code is invalid.")
	}
	return nil
}

func validateTokenBudget(estInput, maxOut int64) error {
	if estInput < 0 || maxOut < 0 || estInput > maxTokenBudget || maxOut > maxTokenBudget || estInput+maxOut > maxTokenBudget {
		return errors.New("Token budget is outside the supported range.")
	}
	return nil
}

func isUnsafeStorageRef(ref string) bool {
	return strings.TrimSpace(ref) == "" ||
		strings.Contains(ref, "..") ||
		strings.HasPrefix(ref, "http://") ||
		strings.HasPrefix(ref, "https://") ||
		strings.HasPrefix(ref, "file://") ||
		strings.HasPrefix(ref, "ftp://")
}

func mapExecutionFailure(operation string, err error) error {
	msg := strings.ToUpper(err.Error())
	if strings.Contains(msg, "CAPACITY") || strings.Contains(msg, "QUOTA") {
		return status.Error(codes.ResourceExhausted, operation+" capacity is exhausted.")
	}
	if strings.Contains(msg, "PROVIDER") || strings.Contains(msg, "TIMEOUT") || strings.Contains(msg, "UNAVAILABLE") {
		return status.Error(codes.Unavailable, operation+" provider is unavailable.")
	}
	return status.Error(codes.Internal, operation+" failed.")
}

package aigov

import (
	"context"
	"encoding/json"
	"fmt"
	"log/slog"
	"strings"
	"time"

	"aurora/route-planning-agent/internal/application/dtos"
	domainrules "aurora/route-planning-agent/internal/domain/rules"
	"aurora/route-planning-agent/proto/aigovpb"

	"github.com/google/uuid"
	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
)

const (
	PromptVersion = "v2.0"

	SystemPrompt = `Bạn là chuyên gia phân tích rủi ro logistics cho hệ thống lập kế hoạch tuyến vận chuyển.
Nhiệm vụ: phân tích tuyến đường (JSON), các vi phạm rule engine và ngữ cảnh tuân thủ (nếu có),
sau đó trả về khuyến nghị.

BẮT BUỘC trả về DUY NHẤT một JSON object đúng schema sau, không thêm văn bản nào khác:
{
  "summary": "<tóm tắt phân tích rủi ro và khuyến nghị, tiếng Việt, tối đa 500 ký tự>",
  "confidence": <số 0.0-1.0>,
  "suggestions": ["<đề xuất cải thiện 1>", "<đề xuất 2>", ...]
}`
)

type RouteAiService interface {
	GetRecommendation(
		ctx context.Context,
		route *dtos.RouteDto,
		ruleResults []domainrules.RuleResult,
		complianceResult *dtos.ComplianceCheckResultDto,
	) *dtos.RouteAiResult
}

type Client struct {
	grpcClient     aigovpb.AiExecutionServiceClient
	capabilityCode string
	logger         *slog.Logger
}

func NewClient(target string, capabilityCode string, logger *slog.Logger) (*Client, error) {
	if logger == nil {
		logger = slog.Default()
	}
	if target == "" {
		target = "localhost:5005"
	}
	if capabilityCode == "" {
		capabilityCode = "route.plan"
	}

	conn, err := grpc.NewClient(target, grpc.WithTransportCredentials(insecure.NewCredentials()))
	if err != nil {
		return nil, fmt.Errorf("không thể kết nối tới AiGovernance (%s): %w", target, err)
	}

	return &Client{
		grpcClient:     aigovpb.NewAiExecutionServiceClient(conn),
		capabilityCode: capabilityCode,
		logger:         logger,
	}, nil
}

// NewClientWithGrpcClient cho phép truyền mock hoặc custom client
func NewClientWithGrpcClient(grpcClient aigovpb.AiExecutionServiceClient, capabilityCode string, logger *slog.Logger) *Client {
	if logger == nil {
		logger = slog.Default()
	}
	if capabilityCode == "" {
		capabilityCode = "route.plan"
	}
	return &Client{
		grpcClient:     grpcClient,
		capabilityCode: capabilityCode,
		logger:         logger,
	}
}

func (c *Client) GetRecommendation(
	ctx context.Context,
	route *dtos.RouteDto,
	ruleResults []domainrules.RuleResult,
	complianceResult *dtos.ComplianceCheckResultDto,
) *dtos.RouteAiResult {
	start := time.Now()

	routeJSON, _ := json.Marshal(route)
	rulesJSON, _ := json.Marshal(ruleResults)

	compContext := "(không có)"
	if complianceResult != nil && complianceResult.MergedContext != "" {
		compContext = complianceResult.MergedContext
	}

	prompt := fmt.Sprintf("%s\n\n[ROUTE]\n%s\n\n[RULE_VIOLATIONS]\n%s\n\n[COMPLIANCE_CONTEXT]\n%s",
		SystemPrompt,
		string(routeJSON),
		string(rulesJSON),
		compContext,
	)

	var llmContent string
	var inputTokens int64
	var outputTokens int64
	var model string
	var provider string
	success := true

	req := &aigovpb.AiGenerateRequest{
		CapabilityCode:  c.capabilityCode,
		Prompt:          prompt,
		MaxOutputTokens: 1000,
	}

	resp, err := c.grpcClient.Generate(ctx, req)
	if err != nil {
		success = false
		c.logger.Error("Gọi AI Governance Generate thất bại cho route",
			slog.String("route_id", route.ID.String()),
			slog.Any("error", err),
		)
	} else {
		llmContent = resp.GetContent()
		inputTokens = resp.GetInputTokens()
		outputTokens = resp.GetOutputTokens()
		model = resp.GetModel()
		provider = resp.GetProvider()
	}

	latency := time.Since(start).Milliseconds()

	var recommendation dtos.RouteRecommendationDto
	if success {
		recommendation = ParseLlmResponse(llmContent, route.ID, complianceResult)
	} else {
		recommendation = BuildFallback(route.ID, ruleResults, complianceResult)
	}

	return &dtos.RouteAiResult{
		Recommendation: recommendation,
		Provider:       provider,
		Model:          model,
		PromptVersion:  PromptVersion,
		InputTokens:    int32(inputTokens),
		OutputTokens:   int32(outputTokens),
		LatencyMs:      latency,
		Success:        success,
	}
}

func ParseLlmResponse(llmResponse string, routeID uuid.UUID, complianceResult *dtos.ComplianceCheckResultDto) dtos.RouteRecommendationDto {
	summary := llmResponse
	suggestions := []string{}
	confidence := 0.5

	cleanJSON := strings.TrimSpace(llmResponse)
	if strings.HasPrefix(cleanJSON, "```") {
		firstNewline := strings.Index(cleanJSON, "\n")
		lastFence := strings.LastIndex(cleanJSON, "```")
		if firstNewline >= 0 && lastFence > firstNewline {
			cleanJSON = strings.TrimSpace(cleanJSON[firstNewline+1 : lastFence])
		}
	}

	if strings.HasPrefix(cleanJSON, "{") {
		var parsed struct {
			Summary     string   `json:"summary"`
			Confidence  *float64 `json:"confidence"`
			Suggestions []string `json:"suggestions"`
		}
		if err := json.Unmarshal([]byte(cleanJSON), &parsed); err == nil {
			if parsed.Summary != "" {
				summary = parsed.Summary
			}
			if parsed.Confidence != nil {
				confidence = *parsed.Confidence
			}
			if len(parsed.Suggestions) > 0 {
				suggestions = parsed.Suggestions
			}
		}
	}

	riskLevel := "Medium"
	if complianceResult != nil && complianceResult.HasViolations {
		riskLevel = "High"
	}

	var regulations []string
	if complianceResult != nil {
		regulations = complianceResult.ApplicableRegulations
	}

	return dtos.RouteRecommendationDto{
		RouteID:               routeID,
		RiskLevel:             riskLevel,
		AutomationDecision:    "ExecutedByAi",
		RecommendationSource:  "AI",
		Summary:               summary,
		Suggestions:           suggestions,
		ConfidenceScore:       &confidence,
		ApplicableRegulations: regulations,
	}
}

func BuildFallback(routeID uuid.UUID, ruleResults []domainrules.RuleResult, complianceResult *dtos.ComplianceCheckResultDto) dtos.RouteRecommendationDto {
	var failed []string
	for _, r := range ruleResults {
		if !r.Passed && r.Message != "" {
			failed = append(failed, r.Message)
		}
	}

	summary := "AiGovernance không khả dụng — rule engine không phát hiện vi phạm."
	if len(failed) > 0 {
		summary = fmt.Sprintf("AiGovernance không khả dụng — kết quả rule engine: %s", strings.Join(failed, "; "))
	}

	riskLevel := "Medium"
	if complianceResult != nil && complianceResult.HasViolations {
		riskLevel = "High"
	}

	var regulations []string
	if complianceResult != nil {
		regulations = complianceResult.ApplicableRegulations
	}

	return dtos.RouteRecommendationDto{
		RouteID:               routeID,
		RiskLevel:             riskLevel,
		AutomationDecision:    "ExecutedByRules",
		RecommendationSource:  "Rules",
		Summary:               summary,
		Suggestions:           []string{},
		ConfidenceScore:       nil,
		ApplicableRegulations: regulations,
	}
}


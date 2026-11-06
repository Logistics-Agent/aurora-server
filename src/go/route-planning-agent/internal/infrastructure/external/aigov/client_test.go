package aigov_test

import (
	"context"
	"errors"
	"testing"

	"aurora/route-planning-agent/internal/application/dtos"
	domainrules "aurora/route-planning-agent/internal/domain/rules"
	"aurora/route-planning-agent/internal/infrastructure/external/aigov"
	"aurora/route-planning-agent/proto/aigovpb"

	"github.com/google/uuid"
	"google.golang.org/grpc"
)

type mockAiExecutionServiceClient struct {
	generateResp *aigovpb.AiGenerateResponse
	generateErr  error
}

func (m *mockAiExecutionServiceClient) Generate(ctx context.Context, in *aigovpb.AiGenerateRequest, opts ...grpc.CallOption) (*aigovpb.AiGenerateResponse, error) {
	if m.generateErr != nil {
		return nil, m.generateErr
	}
	return m.generateResp, nil
}

func (m *mockAiExecutionServiceClient) Embed(ctx context.Context, in *aigovpb.AiEmbedRequest, opts ...grpc.CallOption) (*aigovpb.AiEmbedResponse, error) {
	return nil, nil
}

func TestAiGovClientSuccessWithCodeFence(t *testing.T) {
	mockGrpc := &mockAiExecutionServiceClient{
		generateResp: &aigovpb.AiGenerateResponse{
			Content: "```json\n{\n  \"summary\": \"Tuyến đường an toàn\",\n  \"confidence\": 0.95,\n  \"suggestions\": [\"Khởi hành sớm\"]\n}\n```",
			InputTokens:  120,
			OutputTokens: 45,
			Model:        "gemini-1.5-pro",
			Provider:     "google",
		},
	}

	client := aigov.NewClientWithGrpcClient(mockGrpc, "route.plan", nil)

	routeID := uuid.New()
	route := &dtos.RouteDto{
		ID:   routeID,
		Name: "Test Route",
	}

	result := client.GetRecommendation(context.Background(), route, nil, nil)

	if !result.Success {
		t.Errorf("expected success")
	}
	if result.Model != "gemini-1.5-pro" {
		t.Errorf("expected model gemini-1.5-pro, got %s", result.Model)
	}
	if result.Recommendation.Summary != "Tuyến đường an toàn" {
		t.Errorf("expected parsed summary, got %s", result.Recommendation.Summary)
	}
	if result.Recommendation.ConfidenceScore == nil || *result.Recommendation.ConfidenceScore != 0.95 {
		t.Errorf("expected confidence 0.95")
	}
	if len(result.Recommendation.Suggestions) != 1 || result.Recommendation.Suggestions[0] != "Khởi hành sớm" {
		t.Errorf("expected suggestion 'Khởi hành sớm'")
	}
}

func TestAiGovClientFallbackOnError(t *testing.T) {
	mockGrpc := &mockAiExecutionServiceClient{
		generateErr: errors.New("connection refused"),
	}

	client := aigov.NewClientWithGrpcClient(mockGrpc, "route.plan", nil)

	routeID := uuid.New()
	route := &dtos.RouteDto{
		ID:   routeID,
		Name: "Test Route",
	}

	ruleResults := []domainrules.RuleResult{
		{
			RuleName: "HeavyWeightRule",
			Passed:   false,
			Message:  "Quá tải 6000kg",
		},
	}

	result := client.GetRecommendation(context.Background(), route, ruleResults, nil)

	if result.Success {
		t.Errorf("expected success to be false")
	}
	if result.Recommendation.AutomationDecision != "ExecutedByRules" {
		t.Errorf("expected fallback ExecutedByRules, got %s", result.Recommendation.AutomationDecision)
	}
	if result.Recommendation.RecommendationSource != "Rules" {
		t.Errorf("expected source Rules, got %s", result.Recommendation.RecommendationSource)
	}
}


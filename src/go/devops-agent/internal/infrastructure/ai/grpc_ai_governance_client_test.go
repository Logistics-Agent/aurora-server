package ai_test

import (
	"context"
	"errors"
	"io"
	"log/slog"
	"testing"

	"aurora/devops-agent/internal/application"
	"aurora/devops-agent/internal/infrastructure/ai"
	"aurora/devops-agent/proto/aigovpb"
	"aurora/shared/constants"

	"google.golang.org/grpc"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/metadata"
	"google.golang.org/grpc/status"
)

type stubExecutionStub struct {
	calls int
	errs  []error
	resp  *aigovpb.AiGenerateResponse
	lastServiceID string
}

func (s *stubExecutionStub) Generate(ctx context.Context, _ *aigovpb.AiGenerateRequest, _ ...grpc.CallOption) (*aigovpb.AiGenerateResponse, error) {
	if md, ok := metadata.FromOutgoingContext(ctx); ok {
		if vals := md.Get(constants.HeaderServiceID); len(vals) > 0 {
			s.lastServiceID = vals[0]
		}
	}
	idx := s.calls
	s.calls++
	if idx < len(s.errs) && s.errs[idx] != nil {
		return nil, s.errs[idx]
	}
	return s.resp, nil
}

func TestGrpcAiGovernanceClient_RetryOnlyUnavailable(t *testing.T) {
	logger := slog.New(slog.NewTextHandler(io.Discard, nil))

	// 1. Retries UNAVAILABLE once and succeeds
	stub1 := &stubExecutionStub{
		errs: []error{status.Error(codes.Unavailable, "transient")},
		resp: &aigovpb.AiGenerateResponse{Content: "done", Model: "gemini-1.5-flash", Provider: "GEMINI"},
	}
	client1 := ai.NewGrpcAiGovernanceClient(stub1, logger)
	res, err := client1.Generate(context.Background(), application.GenerateCommand{CapabilityCode: "devops.rca", Prompt: "p"})
	if err != nil || res.Content != "done" || stub1.calls != 2 {
		t.Fatalf("expected 1 retry then success, calls=%d err=%v", stub1.calls, err)
	}
	if stub1.lastServiceID != "devops-agent" {
		t.Fatalf("expected x-service-id=devops-agent, got %q", stub1.lastServiceID)
	}

	// 2. Does NOT retry PERMISSION_DENIED
	stub2 := &stubExecutionStub{
		errs: []error{status.Error(codes.PermissionDenied, "quota denied")},
	}
	client2 := ai.NewGrpcAiGovernanceClient(stub2, logger)
	_, err = client2.Generate(context.Background(), application.GenerateCommand{CapabilityCode: "devops.rca", Prompt: "p"})
	if !errors.Is(err, ai.ErrPolicyDenied) || stub2.calls != 1 {
		t.Fatalf("expected immediate ErrPolicyDenied with 1 call, calls=%d err=%v", stub2.calls, err)
	}
}

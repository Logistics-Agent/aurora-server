package ai

import (
	"context"
	"errors"
	"fmt"
	"log/slog"
	"sync"
	"time"

	"aurora/devops-agent/internal/application"
	"aurora/devops-agent/proto/aigovpb"
	"aurora/shared/constants"
	sharedctx "aurora/shared/context"

	"google.golang.org/grpc"
	"google.golang.org/grpc/codes"
	"google.golang.org/grpc/metadata"
	"google.golang.org/grpc/status"
)

const (
	serviceID          = "devops-agent"
	defaultCallTimeout = 30 * time.Second
	maxAttempts        = 2
	retryWait          = 300 * time.Millisecond
)

var (
	ErrPolicyDenied    = errors.New("AI Governance policy denial")
	ErrQuotaExhausted  = errors.New("AI Governance quota exhausted")
	ErrInvalidArgument = errors.New("invalid argument sent to AiGovernance")
	ErrCircuitOpen     = errors.New("AI Governance circuit breaker is open")
)

type cbState int

const (
	cbClosed cbState = iota
	cbOpen
	cbHalfOpen
)

type circuitBreaker struct {
	mu           sync.Mutex
	state        cbState
	windowSize   int
	results      []bool // true = failure
	failRatePct  float64
	openWait     time.Duration
	openedAt     time.Time
}

func newCircuitBreaker(windowSize int, failRatePct float64, openWait time.Duration) *circuitBreaker {
	return &circuitBreaker{
		state:       cbClosed,
		windowSize:  windowSize,
		results:     make([]bool, 0, windowSize),
		failRatePct: failRatePct,
		openWait:    openWait,
	}
}

func (cb *circuitBreaker) allow() bool {
	cb.mu.Lock()
	defer cb.mu.Unlock()
	if cb.state == cbOpen {
		if time.Since(cb.openedAt) >= cb.openWait {
			cb.state = cbHalfOpen
			return true
		}
		return false
	}
	return true
}

func (cb *circuitBreaker) record(isTransportFault bool) {
	cb.mu.Lock()
	defer cb.mu.Unlock()

	if cb.state == cbHalfOpen {
		if isTransportFault {
			cb.state = cbOpen
			cb.openedAt = time.Now()
		} else {
			cb.state = cbClosed
			cb.results = cb.results[:0]
		}
		return
	}

	if len(cb.results) >= cb.windowSize {
		cb.results = cb.results[1:]
	}
	cb.results = append(cb.results, isTransportFault)

	if len(cb.results) >= cb.windowSize {
		failures := 0
		for _, f := range cb.results {
			if f {
				failures++
			}
		}
		rate := (float64(failures) / float64(len(cb.results))) * 100.0
		if rate >= cb.failRatePct {
			cb.state = cbOpen
			cb.openedAt = time.Now()
		}
	}
}

type AiExecutionStub interface {
	Generate(ctx context.Context, in *aigovpb.AiGenerateRequest, opts ...grpc.CallOption) (*aigovpb.AiGenerateResponse, error)
}

type GrpcAiGovernanceClient struct {
	stub   AiExecutionStub
	cb     *circuitBreaker
	logger *slog.Logger
}

func NewGrpcAiGovernanceClient(stub AiExecutionStub, logger *slog.Logger) *GrpcAiGovernanceClient {
	if logger == nil {
		logger = slog.Default()
	}
	return &GrpcAiGovernanceClient{
		stub:   stub,
		cb:     newCircuitBreaker(20, 50.0, 15*time.Second),
		logger: logger,
	}
}

func (c *GrpcAiGovernanceClient) Generate(ctx context.Context, cmd application.GenerateCommand) (application.GenerateResult, error) {
	if c.stub == nil {
		return application.GenerateResult{}, errors.New("AiExecutionService gRPC stub is not initialized")
	}

	if !c.cb.allow() {
		return application.GenerateResult{}, ErrCircuitOpen
	}

	req := &aigovpb.AiGenerateRequest{
		CapabilityCode:       cmd.CapabilityCode,
		Prompt:               cmd.Prompt,
		MaxOutputTokens:      cmd.MaxOutputTokens,
		EstimatedInputTokens: cmd.EstimatedInputTokens,
		Parameters:           cmd.Parameters,
	}

	md := metadata.Pairs(constants.HeaderServiceID, serviceID)
	if tid, ok := sharedctx.GetTenantID(ctx); ok {
		md.Append(constants.HeaderTenantID, tid.String())
	}
	if uid, ok := sharedctx.GetUserID(ctx); ok {
		md.Append(constants.HeaderUserID, uid)
	}
	if traceID, ok := sharedctx.GetTraceID(ctx); ok {
		md.Append(constants.HeaderTraceID, traceID)
	}
	outCtx := metadata.NewOutgoingContext(ctx, md)

	var resp *aigovpb.AiGenerateResponse
	var lastErr error

	for attempt := 1; attempt <= maxAttempts; attempt++ {
		callCtx, cancel := context.WithTimeout(outCtx, defaultCallTimeout)
		c.logger.Debug("Calling AiGovernance.Generate", slog.String("capability", cmd.CapabilityCode), slog.Int("attempt", attempt))
		resp, lastErr = c.stub.Generate(callCtx, req)
		cancel()

		if lastErr == nil {
			c.cb.record(false)
			return application.GenerateResult{
				Content:          resp.GetContent(),
				InputTokens:      resp.GetInputTokens(),
				OutputTokens:     resp.GetOutputTokens(),
				DecisionID:       resp.GetDecisionId(),
				AutomationLevel:  resp.GetAutomationLevel(),
				RequiresApproval: resp.GetRequiresApproval(),
				Model:            resp.GetModel(),
				Provider:         resp.GetProvider(),
			}, nil
		}

		if isTransportFault(lastErr) {
			c.cb.record(true)
		} else {
			// Policy denials, invalid arguments, quota exhaustion are not transport faults
			c.cb.record(false)
		}

		if isTransientUnavailable(lastErr) && attempt < maxAttempts {
			select {
			case <-ctx.Done():
				return application.GenerateResult{}, ctx.Err()
			case <-time.After(retryWait):
			}
			continue
		}
		break
	}

	if st, ok := status.FromError(lastErr); ok {
		c.logger.Error("AiGovernance gRPC call failed",
			slog.String("status", st.Code().String()),
			slog.String("description", st.Message()))
		switch st.Code() {
		case codes.PermissionDenied:
			return application.GenerateResult{}, fmt.Errorf("%w: %s", ErrPolicyDenied, st.Message())
		case codes.InvalidArgument:
			return application.GenerateResult{}, fmt.Errorf("%w: %s", ErrInvalidArgument, st.Message())
		case codes.ResourceExhausted:
			return application.GenerateResult{}, fmt.Errorf("%w: %s", ErrQuotaExhausted, st.Message())
		default:
			return application.GenerateResult{}, fmt.Errorf("AI Governance execution failed (%s): %s", st.Code().String(), st.Message())
		}
	}
	return application.GenerateResult{}, fmt.Errorf("AI Governance execution failed: %w", lastErr)
}

func isTransientUnavailable(err error) bool {
	if st, ok := status.FromError(err); ok {
		return st.Code() == codes.Unavailable
	}
	return false
}

func isTransportFault(err error) bool {
	if st, ok := status.FromError(err); ok {
		code := st.Code()
		return code == codes.Unavailable || code == codes.Internal || code == codes.Unknown
	}
	return true
}

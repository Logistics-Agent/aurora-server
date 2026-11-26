package application

import (
	"context"
	"fmt"
	"log/slog"

	"aurora/devops-agent/internal/domain"
)

type ActionRequest struct {
	CorrelationID string
	ActionType    domain.ActionType
	TargetService string
	Params        map[string]string
}

type ExecutionStatus string

const (
	ExecutionStatusSuccess ExecutionStatus = "SUCCESS"
	ExecutionStatusFailed  ExecutionStatus = "FAILED"
)

type ExecutionResult struct {
	Status  ExecutionStatus
	Message string
}

type ActionExecutor interface {
	SupportedAction() domain.ActionType
	Execute(ctx context.Context, req ActionRequest) (ExecutionResult, error)
}

type ActionExecutorRegistry struct {
	executors map[domain.ActionType]ActionExecutor
}

func NewActionExecutorRegistry(executors ...ActionExecutor) *ActionExecutorRegistry {
	m := make(map[domain.ActionType]ActionExecutor, len(executors))
	for _, ex := range executors {
		if ex != nil {
			m[ex.SupportedAction()] = ex
		}
	}
	return &ActionExecutorRegistry{executors: m}
}

func (r *ActionExecutorRegistry) Get(actionType domain.ActionType) (ActionExecutor, error) {
	ex, ok := r.executors[actionType]
	if !ok {
		return nil, fmt.Errorf("unsupported action type: %s", actionType)
	}
	return ex, nil
}

type RestartPodActionExecutor struct{ logger *slog.Logger }

func NewRestartPodActionExecutor(logger *slog.Logger) *RestartPodActionExecutor {
	return &RestartPodActionExecutor{logger: logger}
}
func (e *RestartPodActionExecutor) SupportedAction() domain.ActionType { return domain.ActionTypeRestartPod }
func (e *RestartPodActionExecutor) Execute(_ context.Context, req ActionRequest) (ExecutionResult, error) {
	if e.logger != nil {
		e.logger.Info("Executing RESTART_POD action", slog.String("service", req.TargetService), slog.String("correlationId", req.CorrelationID))
	}
	return ExecutionResult{Status: ExecutionStatusSuccess, Message: "Pod restart triggered for " + req.TargetService}, nil
}

type ClearCacheActionExecutor struct{ logger *slog.Logger }

func NewClearCacheActionExecutor(logger *slog.Logger) *ClearCacheActionExecutor {
	return &ClearCacheActionExecutor{logger: logger}
}
func (e *ClearCacheActionExecutor) SupportedAction() domain.ActionType { return domain.ActionTypeClearCache }
func (e *ClearCacheActionExecutor) Execute(_ context.Context, req ActionRequest) (ExecutionResult, error) {
	if e.logger != nil {
		e.logger.Info("Executing CLEAR_CACHE action", slog.String("service", req.TargetService), slog.String("correlationId", req.CorrelationID))
	}
	return ExecutionResult{Status: ExecutionStatusSuccess, Message: "Cache cleared for " + req.TargetService}, nil
}

type RollbackActionExecutor struct{ logger *slog.Logger }

func NewRollbackActionExecutor(logger *slog.Logger) *RollbackActionExecutor {
	return &RollbackActionExecutor{logger: logger}
}
func (e *RollbackActionExecutor) SupportedAction() domain.ActionType { return domain.ActionTypeRollback }
func (e *RollbackActionExecutor) Execute(_ context.Context, req ActionRequest) (ExecutionResult, error) {
	if e.logger != nil {
		e.logger.Info("Executing ROLLBACK_RELEASE action", slog.String("service", req.TargetService), slog.String("correlationId", req.CorrelationID))
	}
	return ExecutionResult{Status: ExecutionStatusSuccess, Message: "Release rollback triggered for " + req.TargetService}, nil
}

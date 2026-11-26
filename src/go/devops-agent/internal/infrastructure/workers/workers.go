package workers

import (
	"context"
	"fmt"
	"log/slog"
	"strings"
	"time"

	"aurora/devops-agent/internal/application"
	"aurora/devops-agent/internal/domain"
	"aurora/devops-agent/internal/infrastructure/metrics"
	"aurora/shared/rabbitmq"

	amqp "github.com/rabbitmq/amqp091-go"
)

const (
	AuditExchange           = "devops.audit.events"
	AuditRoutingKeyPrefix   = "devops.audit."
	NotificationExchange    = "devops.events.exchange"
	NotificationRoutingKey  = "devops.alert.incident.detected"
)

type RcaAnalysisWorker struct {
	incidentRepo    application.IncidentRepository
	outboxRepo      application.OutboxRepository
	uow             application.UnitOfWork
	rcaOrchestrator *application.RcaOrchestratorService
	metrics         *metrics.DevOpsMetrics
	interval        time.Duration
	batchSize       int
	logger          *slog.Logger
}

func NewRcaAnalysisWorker(
	incidentRepo application.IncidentRepository,
	outboxRepo application.OutboxRepository,
	uow application.UnitOfWork,
	rcaOrchestrator *application.RcaOrchestratorService,
	m *metrics.DevOpsMetrics,
	interval time.Duration,
	batchSize int,
	logger *slog.Logger,
) *RcaAnalysisWorker {
	if batchSize < 1 {
		batchSize = 1
	}
	if interval <= 0 {
		interval = 5 * time.Second
	}
	if logger == nil {
		logger = slog.Default()
	}
	return &RcaAnalysisWorker{
		incidentRepo:    incidentRepo,
		outboxRepo:      outboxRepo,
		uow:             uow,
		rcaOrchestrator: rcaOrchestrator,
		metrics:         m,
		interval:        interval,
		batchSize:       batchSize,
		logger:          logger,
	}
}

func (w *RcaAnalysisWorker) Start(ctx context.Context) {
	ticker := time.NewTicker(w.interval)
	defer ticker.Stop()

	for {
		select {
		case <-ctx.Done():
			return
		case <-ticker.C:
			if err := w.ProcessPendingIncidents(ctx); err != nil {
				w.logger.Error("RcaAnalysisWorker cycle failed", slog.Any("error", err))
			}
		}
	}
}

func (w *RcaAnalysisWorker) ProcessPendingIncidents(ctx context.Context) error {
	var failedIncidents []*domain.Incident
	err := w.uow.WithTransaction(ctx, func(txCtx context.Context) error {
		pending, err := w.incidentRepo.FindAndLockByStatus(txCtx, domain.IncidentStatusNew, w.batchSize)
		if err != nil {
			return err
		}
		for _, incident := range pending {
			if _, rcaErr := w.rcaOrchestrator.ExecuteRca(txCtx, incident, incident.EvidenceJson); rcaErr != nil {
				w.metrics.IncrementRcaFailures()
				w.logger.Error("RCA failed for incident", slog.String("correlationId", incident.CorrelationID), slog.Any("error", rcaErr))
				failedIncidents = append(failedIncidents, incident)
				return rcaErr
			}
		}
		return nil
	})
	if err != nil && len(failedIncidents) > 0 {
		// Persist failure state in a fresh transaction so an aborted tx never leaves the incident stuck in NEW
		_ = w.uow.WithTransaction(ctx, func(recCtx context.Context) error {
			for _, inc := range failedIncidents {
				fresh, findErr := w.incidentRepo.FindByCorrelationID(recCtx, inc.CorrelationID)
				if findErr != nil || fresh == nil {
					fresh = inc
				}
				if fresh.Status == domain.IncidentStatusNew {
					_ = fresh.TransitionTo(domain.IncidentStatusCollectingContext)
				}
				if fresh.Status == domain.IncidentStatusCollectingContext {
					_ = fresh.TransitionTo(domain.IncidentStatusContextReady)
				}
				if fresh.Status == domain.IncidentStatusContextReady {
					_ = fresh.TransitionTo(domain.IncidentStatusAiAnalysis)
				}
				if fresh.Status == domain.IncidentStatusAiAnalysis {
					_ = fresh.TransitionTo(domain.IncidentStatusAiAnalysisFailed)
				}
				fresh.UpdatedAt = time.Now().UTC()
				if saveErr := w.incidentRepo.Save(recCtx, fresh); saveErr != nil {
					w.logger.Error("Could not persist RCA failure state", slog.String("correlationId", fresh.CorrelationID), slog.Any("error", saveErr))
					continue
				}
				if outErr := w.outboxRepo.Enqueue(recCtx, fresh.CorrelationID, &fresh.ID, domain.AuditActionRcaAnalysisFailed,
					"SYSTEM_DEVOPS_AGENT", `{"reason":"RCA execution failed; inspect sanitized service logs"}`); outErr != nil {
					w.logger.Error("Could not enqueue RCA_ANALYSIS_FAILED outbox", slog.String("correlationId", fresh.CorrelationID), slog.Any("error", outErr))
				}
			}
			return nil
		})
	}
	return err
}

type OutboxPollerWorker struct {
	outboxRepo   application.OutboxRepository
	uow          application.UnitOfWork
	rabbitClient *rabbitmq.Client
	metrics      *metrics.DevOpsMetrics
	interval     time.Duration
	logger       *slog.Logger
}

func NewOutboxPollerWorker(
	outboxRepo application.OutboxRepository,
	uow application.UnitOfWork,
	rabbitClient *rabbitmq.Client,
	m *metrics.DevOpsMetrics,
	interval time.Duration,
	logger *slog.Logger,
) *OutboxPollerWorker {
	if interval <= 0 {
		interval = 5 * time.Second
	}
	if logger == nil {
		logger = slog.Default()
	}
	return &OutboxPollerWorker{
		outboxRepo:   outboxRepo,
		uow:          uow,
		rabbitClient: rabbitClient,
		metrics:      m,
		interval:     interval,
		logger:       logger,
	}
}

func (w *OutboxPollerWorker) Start(ctx context.Context) {
	ticker := time.NewTicker(w.interval)
	defer ticker.Stop()

	for {
		select {
		case <-ctx.Done():
			return
		case <-ticker.C:
			if err := w.ProcessOutboxEvents(ctx); err != nil {
				w.logger.Error("OutboxPollerWorker cycle failed", slog.Any("error", err))
			}
		}
	}
}

func (w *OutboxPollerWorker) ProcessOutboxEvents(ctx context.Context) error {
	if w.rabbitClient == nil {
		return nil
	}
	return w.uow.WithTransaction(ctx, func(txCtx context.Context) error {
		pending, err := w.outboxRepo.FindAndLockPendingBatch(txCtx, 50)
		if err != nil || len(pending) == 0 {
			return err
		}

		for _, event := range pending {
			isNotif := event.ActionType == domain.AuditActionIncidentDetected
			exchange := AuditExchange
			routingKey := AuditRoutingKeyPrefix + strings.ToLower(string(event.ActionType))
			if isNotif {
				exchange = NotificationExchange
				routingKey = NotificationRoutingKey
			}

			headers := amqp.Table{
				"action_type": string(event.ActionType),
				"actor":       event.Actor,
			}
			pubErr := w.rabbitClient.PublishTopic(txCtx, exchange, routingKey, []byte(event.PayloadJson), "application/json", event.CorrelationID, headers)
			now := time.Now().UTC()
			if pubErr != nil {
				w.metrics.IncrementOutboxPublishFailures()
				w.logger.Error("Failed to publish outbox event", slog.String("id", event.ID.String()), slog.Any("error", pubErr))
				event.RetryCount++
				msg := pubErr.Error()
				event.LastError = &msg
				event.UpdatedAt = now
				if saveErr := w.outboxRepo.Save(txCtx, event); saveErr != nil {
					return saveErr
				}
				continue
			}

			event.Processed = true
			event.ProcessedAt = &now
			event.UpdatedAt = now
			if saveErr := w.outboxRepo.Save(txCtx, event); saveErr != nil {
				return saveErr
			}
		}
		return nil
	})
}

type ApprovalTimeoutWorker struct {
	approvalRepo application.PrApprovalRecordRepository
	outboxRepo   application.OutboxRepository
	uow          application.UnitOfWork
	metrics      *metrics.DevOpsMetrics
	interval     time.Duration
	logger       *slog.Logger
}

func NewApprovalTimeoutWorker(
	approvalRepo application.PrApprovalRecordRepository,
	outboxRepo application.OutboxRepository,
	uow application.UnitOfWork,
	m *metrics.DevOpsMetrics,
	interval time.Duration,
	logger *slog.Logger,
) *ApprovalTimeoutWorker {
	if interval <= 0 {
		interval = 15 * time.Second
	}
	if logger == nil {
		logger = slog.Default()
	}
	return &ApprovalTimeoutWorker{
		approvalRepo: approvalRepo,
		outboxRepo:   outboxRepo,
		uow:          uow,
		metrics:      m,
		interval:     interval,
		logger:       logger,
	}
}

func (w *ApprovalTimeoutWorker) Start(ctx context.Context) {
	ticker := time.NewTicker(w.interval)
	defer ticker.Stop()

	for {
		select {
		case <-ctx.Done():
			return
		case <-ticker.C:
			if err := w.ProcessExpiredApprovals(ctx); err != nil {
				w.logger.Error("ApprovalTimeoutWorker cycle failed", slog.Any("error", err))
			}
		}
	}
}

func (w *ApprovalTimeoutWorker) ProcessExpiredApprovals(ctx context.Context) error {
	return w.uow.WithTransaction(ctx, func(txCtx context.Context) error {
		now := time.Now().UTC()
		expired, err := w.approvalRepo.FindByStatusAndExpiresAtBefore(txCtx, domain.ApprovalStatusPending, now)
		if err != nil || len(expired) == 0 {
			return err
		}

		for _, rec := range expired {
			rec.Status = domain.ApprovalStatusExpired
			rec.UpdatedAt = now
			if err := w.approvalRepo.Save(txCtx, rec); err != nil {
				return err
			}
			w.metrics.IncrementApprovalTimeouts()

			var corrID string
			if rec.Incident != nil {
				corrID = rec.Incident.CorrelationID
			}
			incID := rec.IncidentID
			payload := fmt.Sprintf(`{"approvalId":"%s","reason":"Approval expired due to timeout (%d minutes)"}`,
				rec.ID.String(), rec.TimeoutMinutes)
			if err := w.outboxRepo.Enqueue(txCtx, corrID, &incID, domain.AuditActionApprovalRejected, "SYSTEM_DEVOPS_AGENT", payload); err != nil {
				return err
			}
		}
		return nil
	})
}

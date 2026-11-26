package application

import (
	"context"
	"encoding/json"
	"fmt"
	"log/slog"
	"time"

	"aurora/devops-agent/internal/domain"
	"aurora/devops-agent/internal/infrastructure/metrics"
	"aurora/devops-agent/internal/infrastructure/security"
	sharedctx "aurora/shared/context"

	"github.com/google/uuid"
)

const maxEvidenceLength = 131072

type IngestAlertCommand struct {
	Source          string
	ErrorSignature  string
	PayloadJson     string
	AffectedService string
	Environment     string
}

type IngestAlertResult struct {
	Duplicated    bool   `json:"duplicated"`
	CorrelationID string `json:"correlationId"`
	IncidentID    string `json:"incidentId"`
	Status        string `json:"status"`
}

type IngestAlertHandler struct {
	incidentRepo       IncidentRepository
	outboxRepo         OutboxRepository
	uow                UnitOfWork
	dedupService       DedupStore
	antiFlapping       AntiFlappingStore
	severityClassifier *SeverityClassifier
	redaction          *security.RedactionService
	metrics            *metrics.DevOpsMetrics
	logger             *slog.Logger
}

func NewIngestAlertHandler(
	incidentRepo IncidentRepository,
	outboxRepo OutboxRepository,
	uow UnitOfWork,
	dedupService DedupStore,
	antiFlapping AntiFlappingStore,
	severityClassifier *SeverityClassifier,
	redaction *security.RedactionService,
	m *metrics.DevOpsMetrics,
	logger *slog.Logger,
) *IngestAlertHandler {
	if logger == nil {
		logger = slog.Default()
	}
	return &IngestAlertHandler{
		incidentRepo:       incidentRepo,
		outboxRepo:         outboxRepo,
		uow:                uow,
		dedupService:       dedupService,
		antiFlapping:       antiFlapping,
		severityClassifier: severityClassifier,
		redaction:          redaction,
		metrics:            m,
		logger:             logger,
	}
}

func (h *IngestAlertHandler) Handle(ctx context.Context, cmd IngestAlertCommand) (IngestAlertResult, error) {
	h.metrics.IncrementAlertsIngested()

	now := time.Now().UTC()
	dedupKey := h.dedupService.ComputeDedupKey(cmd.Source, cmd.ErrorSignature, now.Unix())
	proposedCorrID := uuid.NewString()

	existingCorrID, isDup, err := h.dedupService.CheckAndStore(ctx, dedupKey, proposedCorrID)
	if err != nil {
		h.logger.Warn("Dedup check failed, proceeding as non-duplicate", slog.Any("error", err))
	} else if isDup {
		h.metrics.IncrementAlertsDeduplicated()
		h.logger.Info("Alert is duplicate", slog.String("correlationId", existingCorrID))
		return IngestAlertResult{
			Duplicated:    true,
			CorrelationID: existingCorrID,
			IncidentID:    "",
			Status:        string(domain.IncidentStatusNew),
		}, nil
	}

	classification := h.severityClassifier.Classify(cmd.Source, cmd.ErrorSignature, cmd.AffectedService, cmd.Environment)
	finalSeverity := classification.Severity
	originalSeverity := finalSeverity

	if finalSeverity == domain.SeverityLow && h.antiFlapping != nil {
		isFlapping, flapErr := h.antiFlapping.RecordEventAndCheckFlapping(ctx, dedupKey)
		if flapErr != nil {
			h.logger.Warn("Anti-flapping check failed", slog.Any("error", flapErr))
		} else if isFlapping {
			finalSeverity = domain.SeverityMedium
			h.metrics.IncrementAntiflappingTriggered()
		}
	}

	sanitizedSig := h.redaction.Redact(cmd.ErrorSignature)
	sanitizedEvidence := h.redaction.Redact(cmd.PayloadJson)
	if len(sanitizedEvidence) > maxEvidenceLength {
		sanitizedEvidence = sanitizedEvidence[:maxEvidenceLength]
	}
	if sanitizedEvidence == "" {
		sanitizedEvidence = "{}"
	}
	sanitizedService := h.redaction.Redact(cmd.AffectedService)
	safeEnv := h.redaction.Redact(cmd.Environment)

	incident := &domain.Incident{
		BaseAuditable: domain.BaseAuditable{
			ID:        uuid.New(),
			CreatedAt: now,
			UpdatedAt: now,
			Version:   0,
		},
		CorrelationID:    proposedCorrID,
		DedupKey:         dedupKey,
		Source:           cmd.Source,
		ErrorSignature:   sanitizedSig,
		EvidenceJson:     sanitizedEvidence,
		Severity:         finalSeverity,
		OriginalSeverity: originalSeverity,
		Status:           domain.IncidentStatusNew,
		AffectedService:  sanitizedService,
		ImpactScore:      classification.ImpactScore,
	}

	serviceLabel := sanitizedService
	if serviceLabel == "" {
		serviceLabel = "unknown"
	}
	envLabel := safeEnv
	if envLabel == "" {
		envLabel = "unknown"
	}

	auditBytes, err := json.Marshal(map[string]any{
		"severity":        string(incident.Severity),
		"affectedService": serviceLabel,
	})
	if err != nil {
		return IngestAlertResult{}, fmt.Errorf("serialize incident audit event: %w", err)
	}

	notifBytes, err := json.Marshal(map[string]any{
		"eventId":     incident.CorrelationID,
		"serviceName": serviceLabel,
		"alertTitle":  incident.ErrorSignature,
		"severity":    notificationSeverity(incident.Severity),
		"details":     "Incident correlationId=" + incident.CorrelationID + "; source=" + incident.Source,
		"environment": envLabel,
		"timestamp":   now.Format(time.RFC3339Nano),
	})
	if err != nil {
		return IngestAlertResult{}, fmt.Errorf("serialize incident notification event: %w", err)
	}

	actor := resolveActor(ctx)
	err = h.uow.WithTransaction(ctx, func(txCtx context.Context) error {
		if err := h.incidentRepo.Save(txCtx, incident); err != nil {
			return err
		}
		if err := h.outboxRepo.Enqueue(txCtx, incident.CorrelationID, &incident.ID, domain.AuditActionIncidentCreated, actor, string(auditBytes)); err != nil {
			return err
		}
		return h.outboxRepo.Enqueue(txCtx, incident.CorrelationID, &incident.ID, domain.AuditActionIncidentDetected, actor, string(notifBytes))
	})
	if err != nil {
		return IngestAlertResult{}, err
	}

	h.logger.Info("Created new Incident",
		slog.String("id", incident.ID.String()),
		slog.String("correlationId", incident.CorrelationID),
		slog.String("severity", string(incident.Severity)),
		slog.Float64("impactScore", incident.ImpactScore))

	return IngestAlertResult{
		Duplicated:    false,
		CorrelationID: incident.CorrelationID,
		IncidentID:    incident.ID.String(),
		Status:        string(incident.Status),
	}, nil
}

func notificationSeverity(s domain.Severity) string {
	switch s {
	case domain.SeverityCritical:
		return "CRITICAL"
	case domain.SeverityHigh, domain.SeverityMedium:
		return "WARNING"
	default:
		return "INFO"
	}
}

func resolveActor(ctx context.Context) string {
	if uid, ok := sharedctx.GetUserID(ctx); ok && uid != "" {
		return uid
	}
	return "SYSTEM_DEVOPS_AGENT"
}

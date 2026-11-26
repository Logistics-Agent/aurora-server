package http_test

import (
	"bytes"
	"context"
	"encoding/json"
	"io"
	"log/slog"
	"net/http"
	"net/http/httptest"
	"testing"

	"aurora/devops-agent/internal/application"
	"aurora/devops-agent/internal/domain"
	"aurora/devops-agent/internal/infrastructure/metrics"
	"aurora/devops-agent/internal/infrastructure/security"
	httphandler "aurora/devops-agent/internal/transport/http"

	"github.com/google/uuid"
	"github.com/prometheus/client_golang/prometheus"
)

type httpStubIncidentRepo struct{}

func (httpStubIncidentRepo) Save(_ context.Context, _ *domain.Incident) error { return nil }
func (httpStubIncidentRepo) FindByCorrelationID(_ context.Context, _ string) (*domain.Incident, error) {
	return nil, nil
}
func (httpStubIncidentRepo) List(_ context.Context, _ string, _, _ int32) ([]*domain.Incident, int64, error) {
	return nil, 0, nil
}
func (httpStubIncidentRepo) FindAndLockByStatus(_ context.Context, _ domain.IncidentStatus, _ int) ([]*domain.Incident, error) {
	return nil, nil
}

type httpStubOutbox struct{}

func (httpStubOutbox) Enqueue(_ context.Context, _ string, _ *uuid.UUID, _ domain.AuditActionType, _, _ string) error {
	return nil
}
func (httpStubOutbox) FindAndLockPendingBatch(_ context.Context, _ int) ([]*domain.AuditEventOutbox, error) {
	return nil, nil
}
func (httpStubOutbox) Save(_ context.Context, _ *domain.AuditEventOutbox) error { return nil }

type httpStubUoW struct{}

func (httpStubUoW) WithTransaction(ctx context.Context, fn func(txCtx context.Context) error) error {
	return fn(ctx)
}

type httpStubDedup struct{}

func (httpStubDedup) ComputeDedupKey(_, sig string, _ int64) string { return sig }
func (httpStubDedup) CheckAndStore(_ context.Context, _, _ string) (string, bool, error) {
	return "", false, nil
}

func TestPrometheusWebhookHandler(t *testing.T) {
	reg := prometheus.NewRegistry()
	m := metrics.NewDevOpsMetrics(reg)
	logger := slog.New(slog.NewTextHandler(io.Discard, nil))
	ingest := application.NewIngestAlertHandler(
		httpStubIncidentRepo{}, httpStubOutbox{}, httpStubUoW{}, httpStubDedup{}, nil,
		application.NewSeverityClassifier(), security.NewRedactionService(), m, logger,
	)

	h := httphandler.NewHTTPHandler(ingest, "secret-token", func() bool { return true })

	// 1. Unauthorized without token
	req1 := httptest.NewRequest(http.MethodPost, "/api/v1/alerts/prometheus", bytes.NewBufferString(`{"alerts":[]}`))
	rec1 := httptest.NewRecorder()
	h.ServeHTTP(rec1, req1)
	if rec1.Code != http.StatusUnauthorized {
		t.Fatalf("expected 401, got %d", rec1.Code)
	}

	// 2. Empty alerts -> 400
	req2 := httptest.NewRequest(http.MethodPost, "/api/v1/alerts/prometheus", bytes.NewBufferString(`{"alerts":[]}`))
	req2.Header.Set("Authorization", "Bearer secret-token")
	rec2 := httptest.NewRecorder()
	h.ServeHTTP(rec2, req2)
	if rec2.Code != http.StatusBadRequest {
		t.Fatalf("expected 400, got %d", rec2.Code)
	}

	// 3. Valid firing alert + resolved alert filtered -> 202 with 1 result
	body := `{"alerts":[
		{"status":"firing","labels":{"alertname":"CrashLoopBackOff","service":"orders","environment":"prod"}},
		{"status":"resolved","labels":{"alertname":"OldAlert","service":"orders","environment":"prod"}}
	]}`
	req3 := httptest.NewRequest(http.MethodPost, "/api/v1/alerts/prometheus", bytes.NewBufferString(body))
	req3.Header.Set("Authorization", "Bearer secret-token")
	rec3 := httptest.NewRecorder()
	h.ServeHTTP(rec3, req3)
	if rec3.Code != http.StatusAccepted {
		t.Fatalf("expected 202, got %d: %s", rec3.Code, rec3.Body.String())
	}

	var results []application.IngestAlertResult
	if err := json.Unmarshal(rec3.Body.Bytes(), &results); err != nil || len(results) != 1 || results[0].Duplicated {
		t.Fatalf("unexpected webhook body: err=%v results=%+v", err, results)
	}
}

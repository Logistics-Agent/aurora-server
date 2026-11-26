package http

import (
	"crypto/subtle"
	"encoding/json"
	"net/http"
	"strings"

	"aurora/devops-agent/internal/application"

	"github.com/prometheus/client_golang/prometheus/promhttp"
)

type AlertmanagerWebhook struct {
	Alerts []AlertmanagerAlert `json:"alerts"`
}

type AlertmanagerAlert struct {
	Status      string            `json:"status"`
	Labels      map[string]string `json:"labels"`
	Annotations map[string]string `json:"annotations"`
	StartsAt    string            `json:"startsAt"`
	Fingerprint string            `json:"fingerprint"`
}

func NewHTTPHandler(ingestHandler *application.IngestAlertHandler, webhookToken string, readyCheck func() bool) http.Handler {
	mux := http.NewServeMux()
	mux.Handle("/actuator/prometheus", promhttp.Handler())

	healthFn := func(w http.ResponseWriter, _ *http.Request) {
		st := "UP"
		code := http.StatusOK
		if readyCheck != nil && !readyCheck() {
			st = "DOWN"
			code = http.StatusServiceUnavailable
		}
		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(code)
		_ = json.NewEncoder(w).Encode(map[string]string{"status": st})
	}
	mux.HandleFunc("/actuator/health", healthFn)
	mux.HandleFunc("/healthz", healthFn)

	mux.HandleFunc("/api/v1/alerts/prometheus", func(w http.ResponseWriter, r *http.Request) {
		if r.Method != http.MethodPost {
			http.Error(w, "Method not allowed", http.StatusMethodNotAllowed)
			return
		}
		if webhookToken == "" || !matchesToken(r.Header.Get("Authorization"), webhookToken) {
			http.Error(w, "Unauthorized", http.StatusUnauthorized)
			return
		}

		var payload AlertmanagerWebhook
		if err := json.NewDecoder(r.Body).Decode(&payload); err != nil {
			http.Error(w, "Invalid JSON payload", http.StatusBadRequest)
			return
		}
		if len(payload.Alerts) == 0 {
			http.Error(w, "Alertmanager payload must contain alerts", http.StatusBadRequest)
			return
		}

		results := make([]application.IngestAlertResult, 0, len(payload.Alerts))
		for _, alert := range payload.Alerts {
			if alert.Status != "" && !strings.EqualFold(alert.Status, "firing") {
				continue
			}
			cmd, err := toIngestCommand(alert)
			if err != nil {
				http.Error(w, "Invalid alert payload", http.StatusBadRequest)
				return
			}
			res, err := ingestHandler.Handle(r.Context(), cmd)
			if err != nil {
				http.Error(w, "Failed to ingest alert", http.StatusInternalServerError)
				return
			}
			results = append(results, res)
		}

		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(http.StatusAccepted)
		_ = json.NewEncoder(w).Encode(results)
	})

	return mux
}

func toIngestCommand(alert AlertmanagerAlert) (application.IngestAlertCommand, error) {
	labels := alert.Labels
	if labels == nil {
		labels = map[string]string{}
	}
	alertName := firstOrDefault(labels["alertname"], "PrometheusAlert")
	service := firstOrDefault(labels["service"], firstOrDefault(labels["service_name"], "unknown"))
	env := firstOrDefault(labels["environment"], firstOrDefault(labels["env"], "unknown"))
	signature := alertName + ":" + service + ":" + env

	rawBytes, err := json.Marshal(alert)
	if err != nil {
		return application.IngestAlertCommand{}, err
	}
	return application.IngestAlertCommand{
		Source:          "prometheus",
		ErrorSignature:  signature,
		PayloadJson:     string(rawBytes),
		AffectedService: service,
		Environment:     env,
	}, nil
}

func firstOrDefault(val, def string) string {
	if val != "" {
		return val
	}
	return def
}

func matchesToken(authHeader, expectedToken string) bool {
	if !strings.HasPrefix(authHeader, "Bearer ") {
		return false
	}
	supplied := strings.TrimPrefix(authHeader, "Bearer ")
	return subtle.ConstantTimeCompare([]byte(supplied), []byte(expectedToken)) == 1
}

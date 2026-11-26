package http

import (
	"encoding/json"
	"net/http"

	"github.com/prometheus/client_golang/prometheus/promhttp"
)

func NewHTTPHandler(readyCheck func() bool) http.Handler {
	mux := http.NewServeMux()
	mux.Handle("/actuator/prometheus", promhttp.Handler())

	healthFn := func(w http.ResponseWriter, _ *http.Request) {
		status := "UP"
		code := http.StatusOK
		if readyCheck != nil && !readyCheck() {
			status = "DOWN"
			code = http.StatusServiceUnavailable
		}
		w.Header().Set("Content-Type", "application/json")
		w.WriteHeader(code)
		_ = json.NewEncoder(w).Encode(map[string]string{"status": status})
	}

	mux.HandleFunc("/actuator/health", healthFn)
	mux.HandleFunc("/healthz", healthFn)
	return mux
}

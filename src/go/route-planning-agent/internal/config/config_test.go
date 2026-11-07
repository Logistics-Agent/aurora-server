package config_test

import (
	"strings"
	"testing"

	"aurora/route-planning-agent/internal/config"
)

func TestNormalizePostgresDSN(t *testing.T) {
	// Dạng .NET Connection String
	dotnetConn := "Host=postgres;Port=5432;Database=route_planning_db;Username=aurora_admin;Password=secret123;"
	normalized := config.NormalizePostgresDSN(dotnetConn)

	if !strings.Contains(normalized, "host=postgres") {
		t.Errorf("missing host=postgres, got %s", normalized)
	}
	if !strings.Contains(normalized, "port=5432") {
		t.Errorf("missing port=5432, got %s", normalized)
	}
	if !strings.Contains(normalized, "dbname=route_planning_db") {
		t.Errorf("missing dbname=route_planning_db, got %s", normalized)
	}
	if !strings.Contains(normalized, "user=aurora_admin") {
		t.Errorf("missing user=aurora_admin, got %s", normalized)
	}
	if !strings.Contains(normalized, "password=secret123") {
		t.Errorf("missing password=secret123, got %s", normalized)
	}
	if !strings.Contains(normalized, "sslmode=disable") {
		t.Errorf("missing sslmode=disable, got %s", normalized)
	}

	// Dạng URL postgresql://
	urlConn := "postgresql://user:pass@localhost:5432/dbname?sslmode=disable"
	if config.NormalizePostgresDSN(urlConn) != urlConn {
		t.Errorf("URL format should remain unchanged")
	}
}


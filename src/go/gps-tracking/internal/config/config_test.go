package config

import (
	"os"
	"testing"
)

func TestLoadConfig_Defaults(t *testing.T) {
	os.Unsetenv("PORT")
	os.Unsetenv("DATABASE_URL")

	cfg := LoadConfig()
	if cfg.Port != 6002 {
		t.Errorf("expected default port 6002, got %d", cfg.Port)
	}
	if cfg.StationarySpeedKph != 1.0 {
		t.Errorf("expected default stationary speed 1.0, got %f", cfg.StationarySpeedKph)
	}
	if cfg.MaxDBOpenConns != 50 {
		t.Errorf("expected default max open conns 50, got %d", cfg.MaxDBOpenConns)
	}
}


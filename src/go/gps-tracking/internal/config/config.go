package config

import (
	"os"
	"strconv"
	"time"
)

type Config struct {
	Port                  int
	DatabaseURL           string
	StationarySpeedKph    float64
	MaxDBOpenConns        int
	MaxDBIdleConns        int
	RabbitMQURL           string
	SignalLossThreshold   time.Duration
	SignalLossInterval    time.Duration
	SignalLossBatchSize   int
	OutboxPollingInterval time.Duration
	OutboxBatchSize       int
	OutboxMaxRetries      int
}

func getEnvOrDefault(key, fallback string) string {
	if val := os.Getenv(key); val != "" {
		return val
	}
	return fallback
}

func LoadConfig() Config {
	port, _ := strconv.Atoi(getEnvOrDefault("PORT", "6002"))
	maxOpen, _ := strconv.Atoi(getEnvOrDefault("DB_MAX_OPEN_CONNS", "50"))
	maxIdle, _ := strconv.Atoi(getEnvOrDefault("DB_MAX_IDLE_CONNS", "10"))
	stationarySpeed, _ := strconv.ParseFloat(getEnvOrDefault("STATIONARY_SPEED_KPH", "1.0"), 64)

	rabbitURL := getEnvOrDefault("RABBITMQ_URL", "")
	if rabbitURL == "" {
		host := getEnvOrDefault("RABBITMQ_HOST", "localhost")
		port := getEnvOrDefault("RABBITMQ_PORT", "5672")
		user := getEnvOrDefault("RABBITMQ_USERNAME", "guest")
		pass := getEnvOrDefault("RABBITMQ_PASSWORD", "guest")
		rabbitURL = "amqp://" + user + ":" + pass + "@" + host + ":" + port + "/"
	}

	sigLossSec, _ := strconv.Atoi(getEnvOrDefault("SIGNAL_LOSS_THRESHOLD_SECONDS", "300"))
	sigIntervalSec, _ := strconv.Atoi(getEnvOrDefault("SIGNAL_LOSS_INTERVAL_SECONDS", "30"))
	sigBatch, _ := strconv.Atoi(getEnvOrDefault("SIGNAL_LOSS_BATCH_SIZE", "100"))

	outboxIntervalMs, _ := strconv.Atoi(getEnvOrDefault("OUTBOX_POLLING_INTERVAL_MS", "1000"))
	outboxBatch, _ := strconv.Atoi(getEnvOrDefault("OUTBOX_BATCH_SIZE", "100"))
	outboxRetries, _ := strconv.Atoi(getEnvOrDefault("OUTBOX_MAX_RETRIES", "5"))

	dbURL := getEnvOrDefault("DATABASE_URL", "")
	if dbURL == "" {
		host := getEnvOrDefault("POSTGRES_HOST", "localhost")
		portStr := getEnvOrDefault("POSTGRES_PORT", "5435")
		user := getEnvOrDefault("POSTGRES_USER", "aurora_admin")
		pass := getEnvOrDefault("POSTGRES_PASSWORD", "aurora_secure_password_2026!")
		dbName := getEnvOrDefault("POSTGRES_DB", "aurora_gps_tracking")
		dbURL = "host=" + host + " user=" + user + " password=" + pass + " dbname=" + dbName + " port=" + portStr + " sslmode=disable TimeZone=UTC"
	}

	return Config{
		Port:                  port,
		DatabaseURL:           dbURL,
		StationarySpeedKph:    stationarySpeed,
		MaxDBOpenConns:        maxOpen,
		MaxDBIdleConns:        maxIdle,
		RabbitMQURL:           rabbitURL,
		SignalLossThreshold:   time.Duration(sigLossSec) * time.Second,
		SignalLossInterval:    time.Duration(sigIntervalSec) * time.Second,
		SignalLossBatchSize:   sigBatch,
		OutboxPollingInterval: time.Duration(outboxIntervalMs) * time.Millisecond,
		OutboxBatchSize:       outboxBatch,
		OutboxMaxRetries:      outboxRetries,
	}
}


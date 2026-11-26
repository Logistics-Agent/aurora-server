package config

import (
	"fmt"
	"net/url"
	"os"
	"strconv"
	"strings"
	"time"
)

type Config struct {
	GRPCPort                     string
	HTTPPort                     string
	PostgresDSN                  string
	RedisAddr                    string
	RedisPassword                string
	RabbitMQURI                  string
	AiGovernanceGRPCAddr         string
	RagServiceGRPCAddr           string
	AlertmanagerWebhookToken     string
	RcaPollInterval              time.Duration
	RcaBatchSize                 int
	OutboxPollInterval           time.Duration
	ApprovalTimeoutCheckInterval time.Duration
}

func LoadConfig() Config {
	grpcPort := getEnvAny([]string{"GRPC_PORT", "GRPC_SERVER_PORT"}, "9090")
	httpPort := getEnvAny([]string{"HTTP_PORT", "SERVER_PORT"}, "8080")

	aiGovAddr := getEnvAny([]string{"AI_GOVERNANCE_GRPC_ADDRESS"}, "localhost:9092")
	aiGovAddr = strings.TrimPrefix(aiGovAddr, "static://")

	ragAddr := getEnvAny([]string{"RAG_SERVICE_GRPC_ADDRESS"}, "localhost:9091")
	ragAddr = strings.TrimPrefix(ragAddr, "static://")

	rcaPollMs := parseIntEnv("DEVOPS_RCA_POLL_INTERVAL_MS", 5000)
	rcaBatch := parseIntEnv("DEVOPS_RCA_BATCH_SIZE", 1)
	if rcaBatch < 1 {
		rcaBatch = 1
	}
	outboxPollMs := parseIntEnv("DEVOPS_OUTBOX_POLL_INTERVAL_MS", 5000)
	approvalCheckMs := parseIntEnv("DEVOPS_APPROVAL_TIMEOUT_CHECK_INTERVAL_MS", 15000)

	return Config{
		GRPCPort:                     grpcPort,
		HTTPPort:                     httpPort,
		PostgresDSN:                  buildPostgresDSN(),
		RedisAddr:                    buildRedisAddr(),
		RedisPassword:                getEnvAny([]string{"REDIS_PASSWORD", "SPRING_REDIS_PASSWORD"}, ""),
		RabbitMQURI:                  buildRabbitMQURI(),
		AiGovernanceGRPCAddr:         aiGovAddr,
		RagServiceGRPCAddr:           ragAddr,
		AlertmanagerWebhookToken:     getEnvAny([]string{"DEVOPS_ALERT_WEBHOOK_TOKEN"}, ""),
		RcaPollInterval:              time.Duration(rcaPollMs) * time.Millisecond,
		RcaBatchSize:                 rcaBatch,
		OutboxPollInterval:           time.Duration(outboxPollMs) * time.Millisecond,
		ApprovalTimeoutCheckInterval: time.Duration(approvalCheckMs) * time.Millisecond,
	}
}

func buildPostgresDSN() string {
	if dsn := os.Getenv("POSTGRES_DSN"); dsn != "" {
		return dsn
	}
	host := getEnvAny([]string{"POSTGRES_HOST"}, "localhost")
	port := getEnvAny([]string{"POSTGRES_PORT"}, "5432")
	db := getEnvAny([]string{"POSTGRES_DB"}, "aurora_devops_db")
	user := getEnvAny([]string{"POSTGRES_USER", "SPRING_DATASOURCE_USERNAME"}, "postgres")
	pass := getEnvAny([]string{"POSTGRES_PASSWORD", "SPRING_DATASOURCE_PASSWORD"}, "postgres")

	if jdbc := os.Getenv("SPRING_DATASOURCE_URL"); jdbc != "" {
		trimmed := strings.TrimPrefix(jdbc, "jdbc:postgresql://")
		if u, err := url.Parse("postgres://" + trimmed); err == nil {
			if h := u.Hostname(); h != "" {
				host = h
			}
			if p := u.Port(); p != "" {
				port = p
			}
			if pathDB := strings.TrimPrefix(u.Path, "/"); pathDB != "" {
				db = pathDB
			}
		}
	}

	return fmt.Sprintf("host=%s user=%s password=%s dbname=%s port=%s sslmode=disable TimeZone=UTC",
		host, user, pass, db, port)
}

func buildRedisAddr() string {
	if addr := os.Getenv("REDIS_ADDR"); addr != "" {
		return addr
	}
	host := getEnvAny([]string{"REDIS_HOST", "SPRING_REDIS_HOST"}, "localhost")
	port := getEnvAny([]string{"REDIS_PORT", "SPRING_REDIS_PORT"}, "6379")
	return fmt.Sprintf("%s:%s", host, port)
}

func buildRabbitMQURI() string {
	if uri := os.Getenv("RABBITMQ_URI"); uri != "" {
		return uri
	}
	host := getEnvAny([]string{"RABBITMQ_HOST", "SPRING_RABBITMQ_HOST"}, "localhost")
	port := getEnvAny([]string{"RABBITMQ_PORT", "SPRING_RABBITMQ_PORT"}, "5672")
	user := getEnvAny([]string{"RABBITMQ_USER", "SPRING_RABBITMQ_USERNAME"}, "guest")
	pass := getEnvAny([]string{"RABBITMQ_PASSWORD", "SPRING_RABBITMQ_PASSWORD"}, "guest")
	return fmt.Sprintf("amqp://%s:%s@%s:%s/", url.QueryEscape(user), url.QueryEscape(pass), host, port)
}

func getEnvAny(keys []string, fallback string) string {
	for _, k := range keys {
		if v := strings.TrimSpace(os.Getenv(k)); v != "" {
			return v
		}
	}
	return fallback
}

func parseIntEnv(key string, fallback int) int {
	raw := strings.TrimSpace(os.Getenv(key))
	if raw == "" {
		return fallback
	}
	if v, err := strconv.Atoi(raw); err == nil {
		return v
	}
	return fallback
}

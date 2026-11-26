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
	GRPCPort                string
	HTTPPort                string
	PostgresDSN             string
	RedisAddr               string
	RedisPassword           string
	RabbitMQURI             string
	IamTenantGRPCAddr       string
	TenantPlanCacheTTL      time.Duration
	CapacityHeadroomRatio   float64
	CredentialCacheTTL      time.Duration
	AzureKeyVaultURL        string
	AzureOpenAIEndpoint     string
	GeminiBaseURL           string
}

func LoadConfig() Config {
	grpcPort := getEnvAny([]string{"GRPC_PORT", "GRPC_SERVER_PORT"}, "9090")
	httpPort := getEnvAny([]string{"HTTP_PORT", "SERVER_PORT"}, "8080")

	postgresDSN := buildPostgresDSN()
	redisAddr := buildRedisAddr()
	redisPass := getEnvAny([]string{"REDIS_PASSWORD", "SPRING_REDIS_PASSWORD"}, "")
	rabbitURI := buildRabbitMQURI()

	iamAddr := getEnvAny([]string{"IAM_TENANT_GRPC_ADDRESS"}, "localhost:5001")
	iamAddr = strings.TrimPrefix(iamAddr, "static://")

	cacheTTL := parseDurationEnv("AI_TENANT_PLAN_CACHE_TTL", time.Hour)
	headroom := parseFloatEnv("AI_CAPACITY_HEADROOM_RATIO", 0.20)
	credTTLSecs := parseIntEnv("CREDENTIAL_CACHE_TTL_SECONDS", 300)

	return Config{
		GRPCPort:              grpcPort,
		HTTPPort:              httpPort,
		PostgresDSN:           postgresDSN,
		RedisAddr:             redisAddr,
		RedisPassword:         redisPass,
		RabbitMQURI:           rabbitURI,
		IamTenantGRPCAddr:     iamAddr,
		TenantPlanCacheTTL:    cacheTTL,
		CapacityHeadroomRatio: headroom,
		CredentialCacheTTL:    time.Duration(credTTLSecs) * time.Second,
		AzureKeyVaultURL:      getEnvAny([]string{"AZURE_KEYVAULT_URL"}, "https://placeholder.vault.azure.net/"),
		AzureOpenAIEndpoint:   getEnvAny([]string{"AZURE_OPENAI_ENDPOINT"}, "https://aurora-openai.openai.azure.com"),
		GeminiBaseURL:         getEnvAny([]string{"GEMINI_BASE_URL"}, "https://generativelanguage.googleapis.com"),
	}
}

func buildPostgresDSN() string {
	if dsn := os.Getenv("POSTGRES_DSN"); dsn != "" {
		return dsn
	}
	host := getEnvAny([]string{"POSTGRES_HOST"}, "localhost")
	port := getEnvAny([]string{"POSTGRES_PORT"}, "5432")
	db := getEnvAny([]string{"POSTGRES_DB"}, "ai_governance")
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

func parseDurationEnv(key string, fallback time.Duration) time.Duration {
	raw := strings.TrimSpace(os.Getenv(key))
	if raw == "" {
		return fallback
	}
	if strings.HasPrefix(raw, "PT") || strings.HasPrefix(raw, "pt") {
		raw = strings.ToLower(strings.TrimPrefix(strings.ToLower(raw), "pt"))
	}
	if d, err := time.ParseDuration(raw); err == nil {
		return d
	}
	return fallback
}

func parseFloatEnv(key string, fallback float64) float64 {
	raw := strings.TrimSpace(os.Getenv(key))
	if raw == "" {
		return fallback
	}
	if v, err := strconv.ParseFloat(raw, 64); err == nil {
		return v
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

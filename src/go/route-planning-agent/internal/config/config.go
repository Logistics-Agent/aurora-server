package config

import (
	"fmt"
	"os"
	"strconv"
	"strings"
	"time"
)

// AppConfig chứa toàn bộ cấu hình hoạt động của RoutePlanningAgent Go
type AppConfig struct {
	Port              string
	PostgresDSN       string
	RabbitMQURI       string
	VroomURL          string
	AiGovernanceURL   string
	ComplianceRagURL  string
	OutboxPollingSecs time.Duration
	OutboxBatchSize   int32
}

// LoadConfig nạp cấu hình từ môi trường, hỗ trợ cả định dạng .NET và Golang chuẩn
func LoadConfig() *AppConfig {
	port := getFirstEnv("PORT", "ROUTEPLANNING_GRPC_PORT")
	if port == "" {
		port = "5005"
	}

	// 1. Phân giải PostgreSQL Connection String
	rawDB := getFirstEnv(
		"POSTGRES_DSN",
		"ROUTEPLANNING_DB",
		"ConnectionStrings__DefaultConnection",
	)
	dbDSN := NormalizePostgresDSN(rawDB)
	if dbDSN == "" {
		dbDSN = "host=localhost port=5432 user=postgres password=postgres dbname=route_planning_db sslmode=disable"
	}

	// 2. Phân giải RabbitMQ URI
	rabbitURI := getFirstEnv("RABBITMQ_URI")
	if rabbitURI == "" {
		rabbitHost := getFirstEnv("RabbitMQ__Host", "RABBITMQ_HOST")
		if rabbitHost == "" {
			rabbitHost = "localhost:5672"
		}
		rabbitUser := getFirstEnv("RabbitMQ__Username", "RABBITMQ_USER", "RABBITMQ_DEFAULT_USER")
		if rabbitUser == "" {
			rabbitUser = "aurora"
		}
		rabbitPass := getFirstEnv("RabbitMQ__Password", "RABBITMQ_PASSWORD", "RABBITMQ_DEFAULT_PASS")
		if rabbitPass == "" {
			rabbitPass = "aurora_dev"
		}
		rabbitURI = fmt.Sprintf("amqp://%s:%s@%s/", rabbitUser, rabbitPass, rabbitHost)
	}

	// 3. Phân giải VROOM URL
	vroomURL := getFirstEnv("VROOM_URL", "Optimization__VroomUrl", "Vroom__BaseUrl")
	if vroomURL == "" {
		vroomURL = "http://localhost:3000"
	}

	// 4. Phân giải AI Governance URL
	aiGovURL := getFirstEnv("AI_GOVERNANCE_URL", "AiGovernance__ServiceUrl")
	if aiGovURL == "" {
		aiGovURL = "localhost:5005"
	}

	// 5. Phân giải Compliance RAG URL
	complianceURL := getFirstEnv("COMPLIANCE_RAG_URL", "ComplianceRag__ServiceUrl")
	if complianceURL == "" {
		complianceURL = "localhost:5003"
	}

	// 6. Outbox settings
	pollingSecs := 5
	if s := getFirstEnv("Outbox__PollingSeconds", "OUTBOX_POLLING_SECONDS"); s != "" {
		if val, err := strconv.Atoi(s); err == nil && val > 0 {
			pollingSecs = val
		}
	}

	batchSize := int32(50)
	if s := getFirstEnv("Outbox__BatchSize", "OUTBOX_BATCH_SIZE"); s != "" {
		if val, err := strconv.Atoi(s); err == nil && val > 0 {
			batchSize = int32(val)
		}
	}

	return &AppConfig{
		Port:              port,
		PostgresDSN:       dbDSN,
		RabbitMQURI:       rabbitURI,
		VroomURL:          vroomURL,
		AiGovernanceURL:   aiGovURL,
		ComplianceRagURL:  complianceURL,
		OutboxPollingSecs: time.Duration(pollingSecs) * time.Second,
		OutboxBatchSize:   batchSize,
	}
}

// NormalizePostgresDSN chuyển đổi chuỗi kết nối .NET (Host=...;Port=...;Database=...) sang DSN chuẩn của PostgreSQL
func NormalizePostgresDSN(raw string) string {
	raw = strings.TrimSpace(raw)
	if raw == "" {
		return ""
	}

	// Nếu đã là dạng URL hoặc libpq key=val chuẩn
	if strings.HasPrefix(raw, "postgres://") || strings.HasPrefix(raw, "postgresql://") {
		return raw
	}
	if strings.Contains(raw, "=") && !strings.Contains(raw, ";") {
		return raw
	}

	// Parse chuỗi dạng .NET (Host=...;Port=...;Database=...;Username=...;Password=...)
	parts := strings.Split(raw, ";")
	kv := make(map[string]string)
	for _, part := range parts {
		part = strings.TrimSpace(part)
		if part == "" {
			continue
		}
		eqIdx := strings.Index(part, "=")
		if eqIdx > 0 {
			k := strings.ToLower(strings.TrimSpace(part[:eqIdx]))
			v := strings.TrimSpace(part[eqIdx+1:])
			kv[k] = v
		}
	}

	var dsnParts []string
	if host, ok := kv["host"]; ok {
		dsnParts = append(dsnParts, fmt.Sprintf("host=%s", host))
	}
	if port, ok := kv["port"]; ok {
		dsnParts = append(dsnParts, fmt.Sprintf("port=%s", port))
	}
	if db, ok := kv["database"]; ok {
		dsnParts = append(dsnParts, fmt.Sprintf("dbname=%s", db))
	}
	if user, ok := kv["username"]; ok {
		dsnParts = append(dsnParts, fmt.Sprintf("user=%s", user))
	} else if user, ok := kv["user id"]; ok {
		dsnParts = append(dsnParts, fmt.Sprintf("user=%s", user))
	}
	if pass, ok := kv["password"]; ok {
		dsnParts = append(dsnParts, fmt.Sprintf("password=%s", pass))
	}
	if ssl, ok := kv["sslmode"]; ok {
		dsnParts = append(dsnParts, fmt.Sprintf("sslmode=%s", ssl))
	} else {
		dsnParts = append(dsnParts, "sslmode=disable")
	}

	if len(dsnParts) > 0 {
		return strings.Join(dsnParts, " ")
	}

	return raw
}

func getFirstEnv(keys ...string) string {
	for _, k := range keys {
		if val := os.Getenv(k); val != "" {
			return val
		}
	}
	return ""
}


package security_test

import (
	"strings"
	"testing"

	"aurora/devops-agent/internal/infrastructure/security"
)

func TestRedactionService_RedactsSecretsAndPII(t *testing.T) {
	svc := security.NewRedactionService()

	input := "Bearer eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjMifQ.signature_abc123 " +
		"postgres://user:pass@db.internal:5432/prod " +
		"Server=db.prod.internal;Password=SuperSecretPass123; " +
		"AIzaSyD3x9kL1mN0pQ2rS4tU6vW8xY0z1a2b3c4 " +
		"AKIAIOSFODNN7EXAMPLE " +
		`api_key="supersecretapikey123456" password="mySecretPassword" ` +
		"contact admin@aurora.io or +1-555-234-5678\n" +
		"-----BEGIN RSA PRIVATE KEY-----\nMIIEpAIBAAKCAQEA\n-----END RSA PRIVATE KEY-----"

	out := svc.Redact(input)

	forbidden := []string{
		"eyJhbGciOiJIUzI1NiJ9",
		"postgres://",
		"SuperSecretPass123",
		"AIzaSyD3x9kL1mN0pQ2rS4tU6vW8xY0z1a2b3c4",
		"AKIAIOSFODNN7EXAMPLE",
		"supersecretapikey123456",
		"mySecretPassword",
		"admin@aurora.io",
		"555-234-5678",
		"MIIEpAIBAAKCAQEA",
	}
	for _, s := range forbidden {
		if strings.Contains(out, s) {
			t.Fatalf("expected %q to be redacted in output: %s", s, out)
		}
	}
	if !strings.Contains(out, "[REDACTED_PRIVATE_KEY]") || !strings.Contains(out, "[REDACTED_EMAIL]") {
		t.Fatalf("expected redaction markers in output: %s", out)
	}
}

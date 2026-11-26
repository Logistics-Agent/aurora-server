package security

import (
	"regexp"
	"strings"
)

var (
	pemPrivateKeyPattern = regexp.MustCompile(`-----BEGIN (?:[A-Z ]*)?PRIVATE KEY-----[\s\S]*?-----END (?:[A-Z ]*)?PRIVATE KEY-----`)
	jwtPattern           = regexp.MustCompile(`eyJ[A-Za-z0-9-_]+\.[A-Za-z0-9-_]+\.[A-Za-z0-9-_]+`)
	bearerPattern        = regexp.MustCompile(`(?i)Bearer\s+[A-Za-z0-9-_\.]+`)
	connStringPattern    = regexp.MustCompile(`(?i)(?:(?:Server|Host|Data Source|User Id|Password|Database|Uid|Pwd)[:=]|(?:mongodb(?:\+srv)?|postgres(?:ql)?|mysql)://|jdbc:[a-z:]+:)[^;\r\n\s]+`)
	geminiKeyPattern     = regexp.MustCompile(`AIzaSy[A-Za-z0-9_-]{33}`)
	awsKeyPattern        = regexp.MustCompile(`AKIA[0-9A-Z]{16}`)
	genericKeyPattern    = regexp.MustCompile(`(?i)(?:api[_-]?key|secret[_-]?key|access[_-]?token|auth[_-]?token)\s*[:=]\s*['"]?[A-Za-z0-9_\-]{16,}['"]?`)
	passwordPattern      = regexp.MustCompile(`(?i)(?:password|passwd|pwd|secret)\s*[:=]\s*['"]([^'"]+)['"]`)
	emailPattern         = regexp.MustCompile(`[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}`)
	phonePattern         = regexp.MustCompile(`(?:\+?\d{1,3}[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}`)
)

type RedactionService struct{}

func NewRedactionService() *RedactionService {
	return &RedactionService{}
}

func (s *RedactionService) Redact(input string) string {
	if strings.TrimSpace(input) == "" {
		return input
	}

	result := input
	result = pemPrivateKeyPattern.ReplaceAllString(result, "[REDACTED_PRIVATE_KEY]")
	result = jwtPattern.ReplaceAllString(result, "[REDACTED_JWT]")
	result = bearerPattern.ReplaceAllString(result, "Bearer [REDACTED_TOKEN]")
	result = connStringPattern.ReplaceAllString(result, "[REDACTED_CONNECTION_STRING]")
	result = geminiKeyPattern.ReplaceAllString(result, "[REDACTED_GEMINI_KEY]")
	result = awsKeyPattern.ReplaceAllString(result, "[REDACTED_AWS_KEY]")
	result = genericKeyPattern.ReplaceAllString(result, "[REDACTED_API_KEY]")
	result = passwordPattern.ReplaceAllString(result, `password="[REDACTED_PASSWORD]"`)
	result = emailPattern.ReplaceAllString(result, "[REDACTED_EMAIL]")
	result = phonePattern.ReplaceAllString(result, "[REDACTED_PHONE]")
	return result
}

package application

import (
	"math"
	"strings"

	"aurora/devops-agent/internal/domain"
)

type ClassificationResult struct {
	Severity    domain.Severity
	ImpactScore float64
}

type SeverityClassifier struct{}

func NewSeverityClassifier() *SeverityClassifier {
	return &SeverityClassifier{}
}

func (c *SeverityClassifier) Classify(source, errorSignature, affectedService, environment string) ClassificationResult {
	severity := determineBaseSeverity(errorSignature)
	score := calculateImpactScore(severity, environment)
	return ClassificationResult{
		Severity:    severity,
		ImpactScore: math.Round(score*100) / 100,
	}
}

func determineBaseSeverity(errorSignature string) domain.Severity {
	if errorSignature == "" {
		return domain.SeverityLow
	}
	lower := strings.ToLower(errorSignature)
	switch {
	case strings.Contains(lower, "out-of-memory") ||
		strings.Contains(lower, "oom") ||
		strings.Contains(lower, "database-connection-failed") ||
		strings.Contains(lower, "critical"):
		return domain.SeverityCritical
	case strings.Contains(lower, "timeout") ||
		strings.Contains(lower, "500") ||
		strings.Contains(lower, "service-unavailable") ||
		strings.Contains(lower, "high"):
		return domain.SeverityHigh
	case strings.Contains(lower, "error") ||
		strings.Contains(lower, "exception") ||
		strings.Contains(lower, "warn"):
		return domain.SeverityMedium
	default:
		return domain.SeverityLow
	}
}

func calculateImpactScore(severity domain.Severity, environment string) float64 {
	var base float64
	switch severity {
	case domain.SeverityCritical:
		base = 85.0
	case domain.SeverityHigh:
		base = 65.0
	case domain.SeverityMedium:
		base = 45.0
	default:
		base = 20.0
	}

	if strings.EqualFold(environment, "production") || strings.EqualFold(environment, "prod") {
		base += 10.0
	}
	return math.Min(100.0, math.Max(0.0, base))
}

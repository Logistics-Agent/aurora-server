package com.aurora.devopsagent.Application.Services;

import com.aurora.devopsagent.Domain.Enums.Severity;
import org.springframework.stereotype.Service;

import java.math.BigDecimal;
import java.math.RoundingMode;

/**
 * SeverityClassifier: Phân loại severity & tính ImpactScore composite (0-100).
 */
@Service
public class SeverityClassifier {

    public record ClassificationResult(Severity severity, BigDecimal impactScore) {}

    public ClassificationResult classify(String source, String errorSignature, String affectedService, String environment) {
        // Base severity classification based on error keywords
        Severity severity = determineBaseSeverity(errorSignature);

        // Calculate ImpactScore (0 - 100)
        double score = calculateImpactScore(severity, affectedService, environment);
        BigDecimal impactScore = BigDecimal.valueOf(score).setScale(2, RoundingMode.HALF_UP);

        return new ClassificationResult(severity, impactScore);
    }

    private Severity determineBaseSeverity(String errorSignature) {
        if (errorSignature == null) return Severity.Low;
        String lower = errorSignature.toLowerCase();
        if (lower.contains("out-of-memory") || lower.contains("oom") || lower.contains("database-connection-failed") || lower.contains("critical")) {
            return Severity.Critical;
        } else if (lower.contains("timeout") || lower.contains("500") || lower.contains("service-unavailable") || lower.contains("high")) {
            return Severity.High;
        } else if (lower.contains("error") || lower.contains("exception") || lower.contains("warn")) {
            return Severity.Medium;
        }
        return Severity.Low;
    }

    private double calculateImpactScore(Severity severity, String affectedService, String environment) {
        double base = switch (severity) {
            case Critical -> 85.0;
            case High -> 65.0;
            case Medium -> 45.0;
            case Low -> 20.0;
        };

        if ("production".equalsIgnoreCase(environment) || "prod".equalsIgnoreCase(environment)) {
            base += 10.0;
        }

        return Math.min(100.0, Math.max(0.0, base));
    }
}

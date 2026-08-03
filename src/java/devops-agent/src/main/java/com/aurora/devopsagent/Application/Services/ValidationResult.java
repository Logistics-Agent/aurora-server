package com.aurora.devopsagent.Application.Services;

public record ValidationResult(
    boolean valid,
    String reason
) {
    public static ValidationResult ok() {
        return new ValidationResult(true, "Validation successful");
    }

    public static ValidationResult fail(String reason) {
        return new ValidationResult(false, reason);
    }
}

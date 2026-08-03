package com.aurora.devopsagent.Domain.ValueObject;

import java.util.List;

public record VerificationPlan(
    List<VerificationCheck> checks,
    int stabilizationWaitSeconds,       // Wait before starting verification
    int checkIntervalSeconds,           // How often to re-check
    int maxRetries                      // How many check cycles before failure
) {}

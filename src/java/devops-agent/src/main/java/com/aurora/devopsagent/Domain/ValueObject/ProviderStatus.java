package com.aurora.devopsagent.Domain.ValueObject;

import java.time.Duration;

public record ProviderStatus(
    String providerName,
    boolean succeeded,
    Duration responseTime,
    String failureReason
) {}

package com.aurora.devopsagent.Application.Services;

import java.time.Duration;
import java.util.Map;

public record ExecutionResult(
    ExecutionStatus status,
    String message,
    Duration duration,
    Map<String, Object> outputDetails
) {}

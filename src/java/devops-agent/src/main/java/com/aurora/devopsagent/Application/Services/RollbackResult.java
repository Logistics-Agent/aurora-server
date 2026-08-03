package com.aurora.devopsagent.Application.Services;

import java.time.Duration;

public record RollbackResult(
    boolean success,
    String detail,
    Duration duration
) {}

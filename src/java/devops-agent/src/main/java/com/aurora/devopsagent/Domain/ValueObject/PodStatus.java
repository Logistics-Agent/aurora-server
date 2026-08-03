package com.aurora.devopsagent.Domain.ValueObject;

import java.time.Instant;
import java.util.Map;

public record PodStatus(
    String phase,                        // Running, Pending, Failed
    int restartCount,
    Instant lastRestartAt,
    String lastTerminationReason,        // OOMKilled, Error
    Map<String, String> resourceLimits,
    Map<String, String> resourceRequests
) {}

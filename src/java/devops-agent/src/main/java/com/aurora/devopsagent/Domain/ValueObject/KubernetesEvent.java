package com.aurora.devopsagent.Domain.ValueObject;

import java.time.Instant;

public record KubernetesEvent(
    Instant timestamp,
    String type,                         // Normal, Warning
    String reason,                       // CrashLoopBackOff, OOMKilled, ImagePullBackOff
    String involvedObject,
    String message,
    int count
) {}

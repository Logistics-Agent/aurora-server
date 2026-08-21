package com.aurora.devopsagent.Domain.ValueObject;

import java.time.Instant;

public record RecentDeployment(
    Instant timestamp,
    String service,
    String previousVersion,
    String currentVersion,
    String commitSha,
    String deployer,                     // ArgoCD, manual
    String syncStatus,                   // Synced, OutOfSync, Degraded
    String healthStatus                  // Healthy, Progressing, Degraded
) {}

package com.aurora.devopsagent.Domain.ValueObject;

public enum RecommendationStatus {
    DRAFT,          // AI generated, not yet proposed
    PROPOSED,       // Sent to approval workflow
    APPROVED,       // Human approved
    REJECTED,       // Human rejected
    EXECUTING,      // ActionExecutor is running
    VERIFIED,       // Post-execution verification passed
    FAILED,         // Execution or verification failed
    SUPERSEDED      // Replaced by a newer version
}

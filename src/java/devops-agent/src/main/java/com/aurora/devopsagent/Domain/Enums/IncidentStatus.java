package com.aurora.devopsagent.Domain.Enums;

public enum IncidentStatus {

    // ── Lifecycle States ──────────────────────────────

    NEW,                        // Created after dedup, before any processing
    COLLECTING_CONTEXT,         // IncidentContextBuilder gathering evidence
    CONTEXT_READY,              // Context built, awaiting routing decision
    RULE_ANALYSIS,              // Rule Engine evaluating
    AI_ANALYSIS,                // RCA Pipeline running (one or more parallel analyses)
    RECOMMENDATION_READY,       // AI/Rule produced recommendation, awaiting proposal
    WAITING_APPROVAL,           // Recommendation proposed, human decision pending
    APPROVED,                   // Human approved, awaiting ActionExecutor dispatch
    EXECUTING,                  // ActionExecutor running remediation
    VERIFYING,                  // Post-execution verification in progress
    RESOLVED,                   // Verification passed, incident resolved
    CLOSED,                     // Administratively closed (after resolved or manually)

    // ── Error States ──────────────────────────────────

    CONTEXT_BUILD_FAILED,       // IncidentContextBuilder critical failure
    ROUTING_FAILED,             // Decision engine cannot determine route
    AI_ANALYSIS_FAILED,         // All RCA analyses failed
    EXECUTION_FAILED,           // ActionExecutor or verification failed

    // ── Terminal Passive States ───────────────────────

    IGNORED,                    // ImpactScore below threshold, logged only
    CANCELLED                   // Manually cancelled by operator
}


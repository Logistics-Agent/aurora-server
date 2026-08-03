package com.aurora.devopsagent.Domain.ValueObject;

import com.aurora.devopsagent.Domain.Enums.ActionType;

import java.time.Instant;
import java.util.List;
import java.util.Map;
import java.util.UUID;

/**
 * Stable contract for AI Recommendations.
 * Consumers: Dashboard, Approval, ActionExecutor, RabbitMQ, Database (JSONB).
 *
 * IMPORTANT: Recommendation is ADVISORY. It does not execute anything.
 * It is produced by AI or Rule Engine and consumed by Approval Workflow.
 * Only after Approval does ActionExecutor receive the actionType + actionParams.
 */
public record Recommendation(

    // ── Identity ─────────────────────────────────────

    UUID recommendationId,
    int version,                        // starts at 1, incremented on revision
    UUID parentVersion,                 // null for v1, points to previous recommendationId for revisions
    RecommendationStatus status,        // DRAFT → PROPOSED → APPROVED → EXECUTING → VERIFIED → REJECTED → SUPERSEDED
    RecommendationSource source,        // RULE_ENGINE, AI_RCA, HUMAN_OVERRIDE

    // ── Primary Action ───────────────────────────────

    ActionType actionType,              // RESTART_POD, ROLLBACK_RELEASE, etc.
    String actionDescription,           // Human-readable: "Rollback PaymentService from v2.4.0 to v2.3.0"
    Map<String, Object> actionParams,   // Structured params for ActionExecutor

    // ── Alternatives ─────────────────────────────────

    List<AlternativeAction> alternativeActions,  // Ranked by confidence DESC

    // ── Assessment ───────────────────────────────────

    double confidence,                  // 0.0 – 1.0
    String confidenceReason,            // "High confidence: 3/3 evidence sources corroborate deployment correlation"
    RiskLevel risk,                     // LOW, MEDIUM, HIGH, CRITICAL
    String riskJustification,           // "Rollback is non-destructive and reversible"
    BlastRadius blastRadius,            // ISOLATED, SERVICE, TENANT, PLATFORM
    String estimatedImpact,             // "30s downtime during rollback, affects ~200 active users"
    int estimatedRecoveryMinutes,       // Expected time to resolve

    // ── Root Cause ───────────────────────────────────

    String rootCause,                   // Structured root cause statement
    List<String> alternativeCauses,     // Other possible causes considered

    // ── Evidence ─────────────────────────────────────

    List<EvidenceRef> evidence,         // Ranked evidence supporting this recommendation

    // ── Rollback ─────────────────────────────────────

    boolean rollbackAvailable,
    RollbackStrategy rollbackStrategy,  // null if rollbackAvailable = false

    // ── Verification ─────────────────────────────────

    VerificationPlan verificationPlan,  // What to check after execution

    // ── Context ──────────────────────────────────────

    String relatedRunbookId,            // Runbook ID if a documented procedure exists
    List<String> relatedIncidentIds,    // Past similar incidents from RAG
    String analysisType,                // "INFRASTRUCTURE", "APPLICATION", "DATABASE", "NETWORK"

    // ── Metadata ─────────────────────────────────────

    Instant createdAt,
    Instant updatedAt

) {
    /**
     * Whether this recommendation requires human approval
     * based on risk, blast radius, and action type.
     */
    public boolean requiresApproval() {
        if (actionType != null && actionType.isDestructive()) return true;
        if (risk == RiskLevel.HIGH || risk == RiskLevel.CRITICAL) return true;
        if (blastRadius == BlastRadius.TENANT || blastRadius == BlastRadius.PLATFORM) return true;
        return false;
    }
}

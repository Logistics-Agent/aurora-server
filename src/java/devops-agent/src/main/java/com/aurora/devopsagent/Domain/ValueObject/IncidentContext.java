package com.aurora.devopsagent.Domain.ValueObject;

import java.time.Duration;
import java.time.Instant;
import java.util.List;
import java.util.Map;

/**
 * Immutable context assembled by IncidentContextBuilder.
 * Consumed by Rule Engine, AI RCA Pipeline, and Dashboard.
 *
 * AI MUST consume this record — never raw logs.
 */
public record IncidentContext(

    // ── Incident Summary ────────────────────────────

    IncidentSummary summary,

    // ── Investigation Window ────────────────────────

    Instant windowStart,
    Instant windowEnd,
    Duration windowDuration,

    // ── Timeline ────────────────────────────────────

    List<TimelineEvent> timeline,       // All evidence merged, sorted by timestamp

    // ── Evidence Package ────────────────────────────

    EvidencePackage evidence,

    // ── Business Context ────────────────────────────

    List<BusinessEvent> businessEvents, // Recent business events from RabbitMQ cache

    // ── Deployment Context ──────────────────────────

    List<RecentDeployment> recentDeployments,
    List<ConfigChange> recentConfigChanges,

    // ── Historical Context ──────────────────────────

    List<MatchingRule> matchingRules,    // From Rule Engine cache
    List<KnowledgeEntry> relatedKnowledge, // From RAG
    List<PastIncidentRef> similarPastIncidents, // Past incidents with same error_signature

    // ── Operational Context ─────────────────────────

    List<RunbookRef> relatedRunbooks,    // Matching runbook references
    TopologySnapshot topology,           // Service dependency graph (affected service and neighbors)
    ConfigSnapshot configSnapshot,       // Current config of affected service (redacted)

    // ── Scores ──────────────────────────────────────

    double riskScore,                    // Pre-computed risk assessment 0.0–1.0
    double correlationScore,             // How well evidence correlates 0.0–1.0
    double contextQualityScore,          // How complete the context is 0.0–1.0

    // ── Telemetry Summary ───────────────────────────

    TelemetrySummary telemetry,          // Aggregated metrics snapshot

    // ── Context Metadata ────────────────────────────

    List<String> warningFlags,           // "LOG_PROVIDER_TIMEOUT", "RAG_UNAVAILABLE", etc.
    Map<String, ProviderStatus> providerStatuses, // Status per provider
    Duration contextBuildDuration,
    int totalEvidenceItemsCollected,     // Before filtering
    int totalEvidenceItemsSelected       // After filtering (sent to AI)

) {}

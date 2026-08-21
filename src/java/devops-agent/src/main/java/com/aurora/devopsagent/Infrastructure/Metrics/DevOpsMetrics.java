package com.aurora.devopsagent.Infrastructure.Metrics;

import io.micrometer.core.instrument.Counter;
import io.micrometer.core.instrument.MeterRegistry;
import org.springframework.stereotype.Component;

/**
 * DevOpsMetrics: Domain-level metrics collection for DevOps-Agent.
 * AiGovernance metrics (RPM, TPM, token budgets) are strictly owned by AiGovernance service.
 */
@Component
public class DevOpsMetrics {

    private final Counter alertsIngested;
    private final Counter alertsDeduplicated;
    private final Counter antiflappingTriggered;
    private final Counter rcaRequests;
    private final Counter rcaFailures;
    private final Counter ragFallback;
    private final Counter actionsExecuted;
    private final Counter actionsBlocked;
    private final Counter approvalTimeouts;
    private final Counter outboxPublishFailures;

    public DevOpsMetrics(MeterRegistry registry) {
        this.alertsIngested = Counter.builder("devops_alerts_ingested_total")
                .description("Total number of ingested alerts")
                .register(registry);

        this.alertsDeduplicated = Counter.builder("devops_alerts_deduplicated_total")
                .description("Total number of alerts deduplicated")
                .register(registry);

        this.antiflappingTriggered = Counter.builder("devops_antiflapping_triggered_total")
                .description("Total number of anti-flapping escalations triggered")
                .register(registry);

        this.rcaRequests = Counter.builder("devops_rca_requests_total")
                .description("Total number of RCA requests initiated")
                .register(registry);

        this.rcaFailures = Counter.builder("devops_rca_failures_total")
                .description("Total number of RCA analysis failures")
                .register(registry);

        this.ragFallback = Counter.builder("devops_rag_fallback_total")
                .description("Total number of DevOps-RAG fallbacks due to unavailability")
                .register(registry);

        this.actionsExecuted = Counter.builder("devops_actions_executed_total")
                .description("Total number of remediation actions executed")
                .register(registry);

        this.actionsBlocked = Counter.builder("devops_actions_blocked_total")
                .description("Total number of remediation actions blocked by policy or validation")
                .register(registry);

        this.approvalTimeouts = Counter.builder("devops_approval_timeouts_total")
                .description("Total number of approval timeouts expired")
                .register(registry);

        this.outboxPublishFailures = Counter.builder("devops_outbox_publish_failures_total")
                .description("Total number of outbox message publish failures")
                .register(registry);
    }

    public void incrementAlertsIngested() { alertsIngested.increment(); }
    public void incrementAlertsDeduplicated() { alertsDeduplicated.increment(); }
    public void incrementAntiflappingTriggered() { antiflappingTriggered.increment(); }
    public void incrementRcaRequests() { rcaRequests.increment(); }
    public void incrementRcaFailures() { rcaFailures.increment(); }
    public void incrementRagFallback() { ragFallback.increment(); }
    public void incrementActionsExecuted() { actionsExecuted.increment(); }
    public void incrementActionsBlocked() { actionsBlocked.increment(); }
    public void incrementApprovalTimeouts() { approvalTimeouts.increment(); }
    public void incrementOutboxPublishFailures() { outboxPublishFailures.increment(); }
}

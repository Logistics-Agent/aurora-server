package com.aurora.aigovernance.governance.application.port;

/**
 * Port for publishing governance audit events (async, best-effort in V1).
 */
public interface PolicyAuditPort {

    void publishPolicyDecision(PolicyAuditEvent event);

    record PolicyAuditEvent(
            String decisionId,
            String tenantId,
            String callerServiceId,
            String capabilityCode,
            String operation,
            boolean allowed,
            String denyReason,
            String policyVersion,
            long timestamp
    ) {}
}

package com.aurora.aigovernance.governance.domain.enums;

/**
 * Reasons for governance policy denial.
 */
public enum DenyReason {
    TENANT_NOT_FOUND,
    TENANT_SUSPENDED,
    TENANT_CANCELLED,
    CAPABILITY_NOT_ALLOWED,
    CLOUD_AI_DISABLED,
    QUOTA_EXCEEDED,
    POLICY_ERROR
}

package com.aurora.shared.entity;

import com.aurora.shared.security.CurrentServiceContext;
import com.aurora.shared.security.CurrentUserContext;
import jakarta.persistence.PrePersist;
import jakarta.persistence.PreUpdate;

import java.time.OffsetDateTime;
import java.time.ZoneOffset;

/**
 * JPA EntityListener tự động điền createdAt/createdBy khi Add, updatedAt/updatedBy khi Modify.
 * Parallel với AuditSaveChangesInterceptor.cs trong .NET.
 * <p>
 * Actor resolution: {@code user:{userId}} → {@code service:{serviceId}} → {@code "system"}.
 */
public class AuditEntityListener {

    @PrePersist
    public void touchForCreate(AuditableEntity entity) {
        OffsetDateTime now = OffsetDateTime.now(ZoneOffset.UTC);
        if (entity.getCreatedAt() == null) {
            entity.setCreatedAt(now);
        }
        if (entity.getCreatedBy() == null) {
            entity.setCreatedBy(getActor());
        }
        entity.setUpdatedAt(now);
        entity.setUpdatedBy(getActor());
    }

    @PreUpdate
    public void touchForUpdate(AuditableEntity entity) {
        OffsetDateTime now = OffsetDateTime.now(ZoneOffset.UTC);
        entity.setUpdatedAt(now);
        entity.setUpdatedBy(getActor());
    }

    /**
     * Resolves the audit actor using priority: user identity → service identity → system.
     * <p>
     * Format: {@code "user:{userId}"}, {@code "service:{serviceId}"}, or {@code "system"}.
     */
    private String getActor() {
        // Priority 1: user identity
        CurrentUserContext userContext = CurrentUserContext.getCurrent();
        if (userContext != null && userContext.getUserId() != null) {
            return "user:" + userContext.getUserId();
        }

        // Priority 2: service/workload identity
        CurrentServiceContext serviceContext = CurrentServiceContext.getCurrent();
        if (serviceContext != null && serviceContext.getServiceId() != null) {
            return "service:" + serviceContext.getServiceId();
        }

        // Priority 3: system fallback
        return "system";
    }
}

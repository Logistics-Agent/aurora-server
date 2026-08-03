package com.aurora.shared.entity;

import com.aurora.shared.security.CurrentUserContext;
import jakarta.persistence.PrePersist;
import jakarta.persistence.PreUpdate;

import java.time.OffsetDateTime;
import java.time.ZoneOffset;

/**
 * JPA EntityListener tự động điền createdAt/createdBy khi Add, updatedAt/updatedBy khi Modify.
 * Parallel với AuditSaveChangesInterceptor.cs trong .NET.
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

    private String getActor() {
        CurrentUserContext context = CurrentUserContext.getCurrent();
        if (context != null && context.getUserId() != null) {
            return context.getUserId().toString();
        }
        return "system";
    }
}

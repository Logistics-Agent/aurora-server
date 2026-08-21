package com.aurora.aigovernance.governance.domain.entity;

import com.aurora.shared.entity.BaseEntity;
import jakarta.persistence.*;

import java.time.OffsetDateTime;

/**
 * Idempotency guard for RabbitMQ event processing.
 */
@Entity
@Table(name = "processed_events")
public class ProcessedEvent extends BaseEntity {

    @Column(name = "event_id", nullable = false, unique = true, length = 100)
    private String eventId;

    @Column(name = "processed_at", nullable = false)
    private OffsetDateTime processedAt;

    public String getEventId() { return eventId; }
    public void setEventId(String eventId) { this.eventId = eventId; }

    public OffsetDateTime getProcessedAt() { return processedAt; }
    public void setProcessedAt(OffsetDateTime processedAt) { this.processedAt = processedAt; }
}

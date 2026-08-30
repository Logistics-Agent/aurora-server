package com.aurora.audit.infrastructure.persistence.entity;

import jakarta.persistence.*;
import java.time.Instant;

@Entity
@Table(name = "audit_logs")
public class AuditLogEntity {

    @Id
    @Column(length = 36)
    private String id;

    @Column(name = "service_name", nullable = false, length = 64)
    private String serviceName;

    @Column(name = "event_type", nullable = false, length = 64)
    private String eventType;

    @Column(name = "tenant_id", length = 64)
    private String tenantId;

    @Column(name = "user_id", length = 64)
    private String userId;

    @Column(name = "user_role", length = 32)
    private String userRole;

    @Column(name = "resource_id", length = 128)
    private String resourceId;

    @Column(name = "payload_json", columnDefinition = "TEXT")
    private String payloadJson;

    @Column(name = "ip_address", length = 45)
    private String ipAddress;

    @Column(name = "created_at", nullable = false)
    private Instant createdAt;

    public AuditLogEntity() {}

    public AuditLogEntity(String id, String serviceName, String eventType, String tenantId, String userId, String userRole, String resourceId, String payloadJson, String ipAddress, Instant createdAt) {
        this.id = id;
        this.serviceName = serviceName;
        this.eventType = eventType;
        this.tenantId = tenantId;
        this.userId = userId;
        this.userRole = userRole;
        this.resourceId = resourceId;
        this.payloadJson = payloadJson;
        this.ipAddress = ipAddress;
        this.createdAt = createdAt;
    }

    public String getId() { return id; }
    public void setId(String id) { this.id = id; }

    public String getServiceName() { return serviceName; }
    public void setServiceName(String serviceName) { this.serviceName = serviceName; }

    public String getEventType() { return eventType; }
    public void setEventType(String eventType) { this.eventType = eventType; }

    public String getTenantId() { return tenantId; }
    public void setTenantId(String tenantId) { this.tenantId = tenantId; }

    public String getUserId() { return userId; }
    public void setUserId(String userId) { this.userId = userId; }

    public String getUserRole() { return userRole; }
    public void setUserRole(String userRole) { this.userRole = userRole; }

    public String getResourceId() { return resourceId; }
    public void setResourceId(String resourceId) { this.resourceId = resourceId; }

    public String getPayloadJson() { return payloadJson; }
    public void setPayloadJson(String payloadJson) { this.payloadJson = payloadJson; }

    public String getIpAddress() { return ipAddress; }
    public void setIpAddress(String ipAddress) { this.ipAddress = ipAddress; }

    public Instant getCreatedAt() { return createdAt; }
    public void setCreatedAt(Instant createdAt) { this.createdAt = createdAt; }
}

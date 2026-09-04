package com.aurora.audit.domain.model;

import java.time.Instant;

public class AuditLog {
    private String id;
    private String serviceName;
    private String eventType;
    private String tenantId;
    private String userId;
    private String userRole;
    private String resourceId;
    private String payloadJson;
    private String ipAddress;
    private Instant createdAt;

    public AuditLog() {}

    public AuditLog(String id, String serviceName, String eventType, String tenantId, String userId, String userRole, String resourceId, String payloadJson, String ipAddress, Instant createdAt) {
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

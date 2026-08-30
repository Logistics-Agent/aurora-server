package com.aurora.audit.infrastructure.messaging.dto;

public class AuditEventDto {
    private String eventId;
    private String serviceName;
    private String eventType;
    private String tenantId;
    private String userId;
    private String userRole;
    private String resourceId;
    private String payloadJson;
    private String ipAddress;
    private String timestamp;

    public AuditEventDto() {}

    public AuditEventDto(String eventId, String serviceName, String eventType, String tenantId, String userId, String userRole, String resourceId, String payloadJson, String ipAddress, String timestamp) {
        this.eventId = eventId;
        this.serviceName = serviceName;
        this.eventType = eventType;
        this.tenantId = tenantId;
        this.userId = userId;
        this.userRole = userRole;
        this.resourceId = resourceId;
        this.payloadJson = payloadJson;
        this.ipAddress = ipAddress;
        this.timestamp = timestamp;
    }

    public String getEventId() { return eventId; }
    public void setEventId(String eventId) { this.eventId = eventId; }

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

    public String getTimestamp() { return timestamp; }
    public void setTimestamp(String timestamp) { this.timestamp = timestamp; }
}

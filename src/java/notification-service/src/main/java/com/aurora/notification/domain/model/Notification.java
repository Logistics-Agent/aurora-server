package com.aurora.notification.domain.model;

import java.time.Instant;

public class Notification {
    private String id;
    private String tenantId;
    private String userId;
    private NotificationType type;
    private NotificationPriority priority;
    private NotificationStatus status;
    private String title;
    private String body;
    private String actionUrl;
    private Instant createdAt;
    private Instant readAt;

    public Notification() {}

    public Notification(String id, String tenantId, String userId, NotificationType type, NotificationPriority priority, NotificationStatus status, String title, String body, String actionUrl, Instant createdAt) {
        this.id = id;
        this.tenantId = tenantId;
        this.userId = userId;
        this.type = type;
        this.priority = priority;
        this.status = status;
        this.title = title;
        this.body = body;
        this.actionUrl = actionUrl;
        this.createdAt = createdAt;
    }

    public String getId() { return id; }
    public void setId(String id) { this.id = id; }

    public String getTenantId() { return tenantId; }
    public void setTenantId(String tenantId) { this.tenantId = tenantId; }

    public String getUserId() { return userId; }
    public void setUserId(String userId) { this.userId = userId; }

    public NotificationType getType() { return type; }
    public void setType(NotificationType type) { this.type = type; }

    public NotificationPriority getPriority() { return priority; }
    public void setPriority(NotificationPriority priority) { this.priority = priority; }

    public NotificationStatus getStatus() { return status; }
    public void setStatus(NotificationStatus status) { this.status = status; }

    public String getTitle() { return title; }
    public void setTitle(String title) { this.title = title; }

    public String getBody() { return body; }
    public void setBody(String body) { this.body = body; }

    public String getActionUrl() { return actionUrl; }
    public void setActionUrl(String actionUrl) { this.actionUrl = actionUrl; }

    public Instant getCreatedAt() { return createdAt; }
    public void setCreatedAt(Instant createdAt) { this.createdAt = createdAt; }

    public Instant getReadAt() { return readAt; }
    public void setReadAt(Instant readAt) { this.readAt = readAt; }
}

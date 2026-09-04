package com.aurora.notification.infrastructure.persistence;

import com.aurora.notification.domain.model.NotificationStatus;
import com.aurora.notification.infrastructure.persistence.entity.NotificationEntity;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.stereotype.Repository;

import java.util.List;

@Repository
public interface SpringDataNotificationRepository extends JpaRepository<NotificationEntity, String> {
    List<NotificationEntity> findByTenantIdAndUserIdOrderByCreatedAtDesc(String tenantId, String userId);
    List<NotificationEntity> findByTenantIdOrderByCreatedAtDesc(String tenantId);
    
    long countByTenantIdAndUserIdAndStatus(String tenantId, String userId, NotificationStatus status);
    long countByTenantIdAndStatus(String tenantId, NotificationStatus status);
    long countByStatus(NotificationStatus status);
}

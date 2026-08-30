package com.aurora.notification.application.usecase;

import com.aurora.notification.domain.model.NotificationStatus;
import com.aurora.notification.infrastructure.persistence.SpringDataNotificationRepository;
import com.aurora.notification.infrastructure.persistence.entity.NotificationEntity;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;

import java.time.Instant;
import java.util.List;

@Service
@RequiredArgsConstructor
public class MarkAllAsReadUseCase {

    private final SpringDataNotificationRepository notificationRepository;

    public int execute(String tenantId, String userId) {
        List<NotificationEntity> notifications;
        if (userId != null && !userId.isBlank()) {
            notifications = notificationRepository.findByTenantIdAndUserIdOrderByCreatedAtDesc(tenantId, userId);
        } else {
            notifications = notificationRepository.findByTenantIdOrderByCreatedAtDesc(tenantId);
        }

        int updatedCount = 0;
        Instant now = Instant.now();
        for (NotificationEntity entity : notifications) {
            if (entity.getStatus() != NotificationStatus.READ) {
                entity.setStatus(NotificationStatus.READ);
                entity.setReadAt(now);
                notificationRepository.save(entity);
                updatedCount++;
            }
        }
        return updatedCount;
    }
}

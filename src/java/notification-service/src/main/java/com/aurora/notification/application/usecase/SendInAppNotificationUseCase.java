package com.aurora.notification.application.usecase;

import com.aurora.notification.domain.model.Notification;
import com.aurora.notification.domain.model.NotificationPriority;
import com.aurora.notification.domain.model.NotificationStatus;
import com.aurora.notification.domain.model.NotificationType;
import com.aurora.notification.infrastructure.persistence.SpringDataNotificationRepository;
import com.aurora.notification.infrastructure.persistence.entity.NotificationEntity;
import com.aurora.notification.infrastructure.realtime.RealtimeHubPublisher;
import lombok.RequiredArgsConstructor;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;

import java.time.Instant;
import java.util.UUID;

@Service
@RequiredArgsConstructor
public class SendInAppNotificationUseCase {

    private static final Logger log = LoggerFactory.getLogger(SendInAppNotificationUseCase.class);

    private final SpringDataNotificationRepository notificationRepository;
    private final RealtimeHubPublisher realtimeHubPublisher;

    public Notification execute(String tenantId, String userId, String title, String body, String actionUrl, NotificationPriority priority) {
        String notificationId = UUID.randomUUID().toString();
        Instant now = Instant.now();

        NotificationEntity entity = new NotificationEntity(
                notificationId,
                tenantId,
                userId,
                NotificationType.IN_APP,
                priority != null ? priority : NotificationPriority.INFO,
                NotificationStatus.PENDING,
                title,
                body,
                actionUrl,
                now
        );

        notificationRepository.save(entity);

        Notification notification = new Notification(
                entity.getId(),
                entity.getTenantId(),
                entity.getUserId(),
                entity.getType(),
                entity.getPriority(),
                entity.getStatus(),
                entity.getTitle(),
                entity.getBody(),
                entity.getActionUrl(),
                entity.getCreatedAt()
        );

        // Broadcast to Realtime Hub for FB/IG style Toast Popup
        realtimeHubPublisher.publishPopupToast(notification);

        return notification;
    }
}

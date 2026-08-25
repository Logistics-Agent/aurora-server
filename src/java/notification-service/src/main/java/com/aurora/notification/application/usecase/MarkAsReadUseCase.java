package com.aurora.notification.application.usecase;

import com.aurora.notification.domain.model.NotificationStatus;
import com.aurora.notification.infrastructure.persistence.SpringDataNotificationRepository;
import com.aurora.notification.infrastructure.persistence.entity.NotificationEntity;
import lombok.RequiredArgsConstructor;
import org.springframework.stereotype.Service;

import java.time.Instant;
import java.util.Optional;

@Service
@RequiredArgsConstructor
public class MarkAsReadUseCase {

    private final SpringDataNotificationRepository notificationRepository;

    public Optional<NotificationEntity> execute(String id) {
        return notificationRepository.findById(id).map(entity -> {
            entity.setStatus(NotificationStatus.READ);
            entity.setReadAt(Instant.now());
            return notificationRepository.save(entity);
        });
    }
}

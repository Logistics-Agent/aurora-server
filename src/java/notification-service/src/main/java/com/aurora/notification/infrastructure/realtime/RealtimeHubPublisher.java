package com.aurora.notification.infrastructure.realtime;

import com.aurora.notification.domain.model.Notification;
import com.fasterxml.jackson.databind.ObjectMapper;
import lombok.RequiredArgsConstructor;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.scheduling.annotation.Async;
import org.springframework.stereotype.Component;

@Component
@RequiredArgsConstructor
public class RealtimeHubPublisher {

    private static final Logger log = LoggerFactory.getLogger(RealtimeHubPublisher.class);

    private final StringRedisTemplate redisTemplate;
    private final ObjectMapper objectMapper;

    @Value("${app.realtime.channel:aurora.realtime.notifications}")
    private String realtimeChannel;

    @Async
    public void publishPopupToast(Notification notification) {
        try {
            String payloadJson = objectMapper.writeValueAsString(notification);
            redisTemplate.convertAndSend(realtimeChannel, payloadJson);
            log.info("Published Realtime Popup Toast to Redis channel '{}' for user='{}'",
                    realtimeChannel, notification.getUserId());
        } catch (Exception e) {
            log.error("Failed to publish Realtime Popup Toast for notificationId={}: {}",
                    notification.getId(), e.getMessage(), e);
        }
    }
}

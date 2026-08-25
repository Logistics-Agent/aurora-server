package com.aurora.notification;

import com.aurora.notification.application.usecase.ProcessDevOpsAlertUseCase;
import com.aurora.notification.application.usecase.SendInAppNotificationUseCase;
import com.aurora.notification.domain.model.Notification;
import com.aurora.notification.domain.model.NotificationPriority;
import com.aurora.notification.domain.model.NotificationStatus;
import com.aurora.notification.domain.model.NotificationType;
import com.aurora.notification.infrastructure.messaging.dto.DevOpsAlertEventDto;
import com.aurora.notification.infrastructure.persistence.SpringDataNotificationRepository;
import com.aurora.notification.infrastructure.persistence.entity.NotificationEntity;
import com.aurora.notification.infrastructure.realtime.RealtimeHubPublisher;
import com.aurora.notification.infrastructure.telegram.TelegramBotClient;
import com.aurora.notification.infrastructure.telegram.TelegramMessageFormatter;
import com.aurora.notification.infrastructure.telegram.dto.TelegramSendMessageRequest;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import org.mockito.Mockito;
import org.springframework.data.redis.core.StringRedisTemplate;

import java.util.Optional;

import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.*;

class NotificationServiceApplicationTests {

    private TelegramBotClient telegramBotClient;
    private TelegramMessageFormatter telegramMessageFormatter;
    private ProcessDevOpsAlertUseCase processDevOpsAlertUseCase;

    private SpringDataNotificationRepository notificationRepository;
    private RealtimeHubPublisher realtimeHubPublisher;
    private SendInAppNotificationUseCase sendInAppNotificationUseCase;

    @BeforeEach
    void setUp() {
        telegramBotClient = mock(TelegramBotClient.class);
        telegramMessageFormatter = new TelegramMessageFormatter();
        processDevOpsAlertUseCase = new ProcessDevOpsAlertUseCase(telegramBotClient, telegramMessageFormatter);

        notificationRepository = mock(SpringDataNotificationRepository.class);
        realtimeHubPublisher = mock(RealtimeHubPublisher.class);
        sendInAppNotificationUseCase = new SendInAppNotificationUseCase(notificationRepository, realtimeHubPublisher);
    }

    @Test
    void testTelegramAlertFormattingAndDispatch() {
        DevOpsAlertEventDto alert = new DevOpsAlertEventDto(
                "evt-101",
                "DevOpsAgent",
                "High CPU Usage Alert",
                "CRITICAL",
                "CPU usage exceeded 95% threshold on node-01",
                "production",
                "2026-08-25T12:00:00Z"
        );

        processDevOpsAlertUseCase.execute(alert);

        ArgumentCaptor<TelegramSendMessageRequest> requestCaptor = ArgumentCaptor.forClass(TelegramSendMessageRequest.class);
        verify(telegramBotClient, times(1)).sendMessage(any(), requestCaptor.capture());

        TelegramSendMessageRequest capturedRequest = requestCaptor.getValue();
        assertNotNull(capturedRequest);
        assertTrue(capturedRequest.getText().contains("High CPU Usage Alert"));
        assertTrue(capturedRequest.getText().contains("DevOpsAgent"));
        assertTrue(capturedRequest.getText().contains("CRITICAL"));
    }

    @Test
    void testSendInAppNotificationUseCase() {
        when(notificationRepository.save(any(NotificationEntity.class))).thenAnswer(invocation -> invocation.getArgument(0));

        Notification result = sendInAppNotificationUseCase.execute(
                "tenant-alpha",
                "user-99",
                "New Order Received",
                "Shipment #888 has been assigned to your fleet.",
                "https://aurora.logistics.com/shipments/888",
                NotificationPriority.INFO
        );

        assertNotNull(result);
        assertEquals("tenant-alpha", result.getTenantId());
        assertEquals("user-99", result.getUserId());
        assertEquals(NotificationType.IN_APP, result.getType());
        assertEquals(NotificationStatus.PENDING, result.getStatus());
        assertEquals("New Order Received", result.getTitle());

        verify(notificationRepository, times(1)).save(any(NotificationEntity.class));
        verify(realtimeHubPublisher, times(1)).publishPopupToast(any(Notification.class));
    }
}

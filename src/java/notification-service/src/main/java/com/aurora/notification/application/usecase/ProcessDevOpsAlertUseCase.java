package com.aurora.notification.application.usecase;

import com.aurora.notification.infrastructure.messaging.dto.DevOpsAlertEventDto;
import com.aurora.notification.infrastructure.telegram.TelegramBotClient;
import com.aurora.notification.infrastructure.telegram.TelegramMessageFormatter;
import com.aurora.notification.infrastructure.telegram.dto.TelegramSendMessageRequest;
import lombok.RequiredArgsConstructor;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.scheduling.annotation.Async;
import org.springframework.stereotype.Service;

@Service
@RequiredArgsConstructor
public class ProcessDevOpsAlertUseCase {

    private static final Logger log = LoggerFactory.getLogger(ProcessDevOpsAlertUseCase.class);

    private final TelegramBotClient telegramBotClient;
    private final TelegramMessageFormatter telegramMessageFormatter;

    @Value("${telegram.bot.token}")
    private String botToken;

    @Value("${telegram.bot.chat-id}")
    private String chatId;

    @Async
    public void execute(DevOpsAlertEventDto alert) {
        log.info("Processing DevOps Alert: eventId={}, service={}, severity={}",
                alert.getEventId(), alert.getServiceName(), alert.getSeverity());

        try {
            String htmlMessage = telegramMessageFormatter.formatDevOpsAlert(alert);

            TelegramSendMessageRequest request = new TelegramSendMessageRequest(chatId, htmlMessage, "HTML");

            telegramBotClient.sendMessage(botToken, request);
            log.info("Successfully dispatched Telegram alert to chatId={}", chatId);

        } catch (Exception e) {
            log.error("Failed to send Telegram alert for eventId={}: {}", alert.getEventId(), e.getMessage(), e);
        }
    }
}

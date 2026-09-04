package com.aurora.notification.infrastructure.telegram;

import com.aurora.notification.infrastructure.telegram.dto.TelegramSendMessageRequest;
import org.springframework.cloud.openfeign.FeignClient;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;

@FeignClient(name = "telegramBotClient", url = "https://api.telegram.org")
public interface TelegramBotClient {

    @PostMapping("/bot{botToken}/sendMessage")
    void sendMessage(
            @PathVariable("botToken") String botToken,
            @RequestBody TelegramSendMessageRequest request
    );
}

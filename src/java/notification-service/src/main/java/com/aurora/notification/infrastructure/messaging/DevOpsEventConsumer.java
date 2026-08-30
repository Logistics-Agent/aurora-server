package com.aurora.notification.infrastructure.messaging;

import com.aurora.notification.application.usecase.ProcessDevOpsAlertUseCase;
import com.aurora.notification.infrastructure.messaging.dto.DevOpsAlertEventDto;
import lombok.RequiredArgsConstructor;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.amqp.rabbit.annotation.RabbitListener;
import org.springframework.stereotype.Component;

@Component
@RequiredArgsConstructor
public class DevOpsEventConsumer {

    private static final Logger log = LoggerFactory.getLogger(DevOpsEventConsumer.class);

    private final ProcessDevOpsAlertUseCase processDevOpsAlertUseCase;

    @RabbitListener(queues = "${app.rabbitmq.queue.devops-alerts:devops.alerts.queue}")
    public void consumeDevOpsAlert(DevOpsAlertEventDto alertEvent) {
        log.info("Received RabbitMQ DevOps event: {}", alertEvent);
        processDevOpsAlertUseCase.execute(alertEvent);
    }
}

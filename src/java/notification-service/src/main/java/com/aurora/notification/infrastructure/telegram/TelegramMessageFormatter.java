package com.aurora.notification.infrastructure.telegram;

import com.aurora.notification.infrastructure.messaging.dto.DevOpsAlertEventDto;
import org.springframework.stereotype.Component;

import java.time.Instant;

@Component
public class TelegramMessageFormatter {

    public String formatDevOpsAlert(DevOpsAlertEventDto alert) {
        String icon = switch (alert.getSeverity() != null ? alert.getSeverity().toUpperCase() : "INFO") {
            case "CRITICAL", "FATAL", "HIGH" -> "🚨";
            case "WARNING", "MEDIUM" -> "⚠️";
            default -> "ℹ️";
        };

        return String.format("""
            <b>%s [DevOps Alert] %s</b>
            
            <b>Component:</b> <code>%s</code>
            <b>Severity:</b> %s
            <b>Message:</b> %s
            <b>Environment:</b> <code>%s</code>
            <b>Timestamp:</b> <code>%s</code>
            """,
                icon,
                alert.getAlertTitle() != null ? alert.getAlertTitle() : "System Event",
                alert.getServiceName() != null ? alert.getServiceName() : "Aurora Core",
                alert.getSeverity() != null ? alert.getSeverity() : "INFO",
                alert.getDetails() != null ? alert.getDetails() : "No details provided",
                alert.getEnvironment() != null ? alert.getEnvironment() : "production",
                alert.getTimestamp() != null ? alert.getTimestamp() : Instant.now().toString()
        );
    }
}

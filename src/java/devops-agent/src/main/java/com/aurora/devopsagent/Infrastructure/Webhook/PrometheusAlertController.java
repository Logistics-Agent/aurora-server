package com.aurora.devopsagent.Infrastructure.Webhook;

import com.aurora.devopsagent.Application.Commands.IngestAlertCommand;
import com.fasterxml.jackson.core.JsonProcessingException;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestHeader;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;
import org.springframework.web.server.ResponseStatusException;

import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.List;
import java.util.Map;

/** Receives Alertmanager webhook notifications and converts firing alerts to incidents. */
@RestController
@RequestMapping("/api/v1/alerts/prometheus")
public class PrometheusAlertController {

    private final IngestAlertCommand.Handler ingestAlertHandler;
    private final ObjectMapper objectMapper;
    private final String webhookToken;

    public PrometheusAlertController(
            IngestAlertCommand.Handler ingestAlertHandler,
            ObjectMapper objectMapper,
            @Value("${devops.alertmanager.webhook-token:}") String webhookToken) {
        this.ingestAlertHandler = ingestAlertHandler;
        this.objectMapper = objectMapper;
        this.webhookToken = webhookToken;
    }

    @PostMapping
    public ResponseEntity<List<IngestAlertCommand.Result>> receive(
            @RequestHeader(name = "Authorization", required = false) String authorization,
            @RequestBody AlertmanagerWebhook webhook) {
        if (webhookToken.isBlank() || !matchesToken(authorization)) {
            throw new ResponseStatusException(HttpStatus.UNAUTHORIZED);
        }
        if (webhook == null || webhook.alerts() == null || webhook.alerts().isEmpty()) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Alertmanager payload must contain alerts");
        }

        List<IngestAlertCommand.Result> results = webhook.alerts().stream()
                .filter(alert -> alert.status() == null || "firing".equalsIgnoreCase(alert.status()))
                .map(alert -> ingestAlertHandler.handle(toCommand(alert)))
                .toList();
        return ResponseEntity.accepted().body(results);
    }

    private IngestAlertCommand toCommand(Alert alert) {
        Map<String, String> labels = alert.labels() == null ? Map.of() : alert.labels();
        String alertName = labels.getOrDefault("alertname", "PrometheusAlert");
        String service = labels.getOrDefault("service", labels.getOrDefault("service_name", "unknown"));
        String environment = labels.getOrDefault("environment", labels.getOrDefault("env", "unknown"));
        String signature = alertName + ":" + service + ":" + environment;
        String payload;
        try {
            payload = objectMapper.writeValueAsString(alert);
        } catch (JsonProcessingException exception) {
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST, "Invalid alert payload");
        }
        return new IngestAlertCommand("prometheus", signature, payload, service, environment);
    }

    private boolean matchesToken(String authorization) {
        if (authorization == null || !authorization.startsWith("Bearer ")) return false;
        byte[] expected = webhookToken.getBytes(StandardCharsets.UTF_8);
        byte[] supplied = authorization.substring("Bearer ".length()).getBytes(StandardCharsets.UTF_8);
        return MessageDigest.isEqual(expected, supplied);
    }

    public record AlertmanagerWebhook(List<Alert> alerts) {}
    public record Alert(String status, Map<String, String> labels, Map<String, String> annotations,
                        String startsAt, String fingerprint) {}
}

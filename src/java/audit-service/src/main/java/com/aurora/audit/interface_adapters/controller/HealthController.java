package com.aurora.audit.interface_adapters.controller;

import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RestController;

import java.util.Map;

@RestController
public class HealthController {

    @GetMapping("/")
    public ResponseEntity<Map<String, String>> healthCheck() {
        return ResponseEntity.ok(Map.of(
                "service", "Aurora Java Audit Log Service",
                "status", "UP",
                "version", "1.0.0",
                "targetAudience", "SYSTEM / DEV / PLATFORM OWNER"
        ));
    }
}

package com.aurora.audit.interface_adapters.controller;

import com.aurora.audit.application.usecase.IngestAuditEventUseCase;
import com.aurora.audit.infrastructure.messaging.dto.AuditEventDto;
import com.aurora.audit.infrastructure.persistence.SpringDataAuditLogRepository;
import com.aurora.audit.infrastructure.persistence.entity.AuditLogEntity;
import lombok.RequiredArgsConstructor;
import org.springframework.data.domain.Page;
import org.springframework.data.domain.PageRequest;

import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

import java.time.Instant;
import java.util.*;

@RestController
@RequestMapping("/api/v1/audit-logs")
@RequiredArgsConstructor
public class AuditLogController {

    private final IngestAuditEventUseCase ingestAuditEventUseCase;
    private final SpringDataAuditLogRepository auditLogRepository;

    @PostMapping("/ingest")
    public ResponseEntity<AuditLogEntity> ingestAuditEvent(@RequestBody AuditEventDto request) {
        AuditLogEntity saved = ingestAuditEventUseCase.execute(request);
        return ResponseEntity.ok(saved);
    }

    @GetMapping
    public ResponseEntity<List<AuditLogEntity>> getAuditLogs(
            @RequestParam(value = "serviceName", required = false) String serviceName,
            @RequestParam(value = "tenantId", required = false) String tenantId,
            @RequestParam(value = "page", defaultValue = "0") int page,
            @RequestParam(value = "size", defaultValue = "20") int size) {

        if (serviceName != null && !serviceName.isBlank()) {
            return ResponseEntity.ok(auditLogRepository.findByServiceNameOrderByCreatedAtDesc(serviceName));
        }
        if (tenantId != null && !tenantId.isBlank()) {
            return ResponseEntity.ok(auditLogRepository.findByTenantIdOrderByCreatedAtDesc(tenantId));
        }

        Page<AuditLogEntity> pageResult = auditLogRepository.findAllByOrderByCreatedAtDesc(PageRequest.of(page, size));
        return ResponseEntity.ok(pageResult.getContent());
    }

    @GetMapping("/{id}")
    public ResponseEntity<AuditLogEntity> getAuditLogById(@PathVariable("id") String id) {
        return auditLogRepository.findById(id)
                .map(ResponseEntity::ok)
                .orElseGet(() -> ResponseEntity.notFound().build());
    }

    @PostMapping("/seed-random")
    public ResponseEntity<List<AuditLogEntity>> seedRandomAuditLogs(@RequestParam(value = "count", defaultValue = "5") int count) {
        Random random = new Random();
        List<AuditLogEntity> seededList = new ArrayList<>();
        String[] services = {"ShipmentWorkflow", "IamTenant", "BillingService", "RoutePlanningAgent", "DocumentOCR"};
        String[] eventTypes = {"SHIPMENT_CREATED", "USER_LOGIN", "INVOICE_GENERATED", "ROUTE_OPTIMIZED", "DOCUMENT_PARSED"};
        String[] roles = {"SYSTEM", "DEV", "PLATFORM_OWNER"};
        String[] tenants = {"tenant-aurora-01", "tenant-logistics-beta", "tenant-express-global"};

        for (int i = 0; i < count; i++) {
            String serviceName = services[random.nextInt(services.length)];
            String eventType = eventTypes[random.nextInt(eventTypes.length)];
            String tenantId = tenants[random.nextInt(tenants.length)];
            String userId = "user-" + (random.nextInt(10) + 100);
            String role = roles[random.nextInt(roles.length)];

            AuditEventDto event = new AuditEventDto(
                    UUID.randomUUID().toString(),
                    serviceName,
                    eventType,
                    tenantId,
                    userId,
                    role,
                    "RES-" + (random.nextInt(9000) + 1000),
                    String.format("{\"action\": \"%s\", \"details\": \"Auto-seeded audit payload #%d\"}", eventType, i + 1),
                    "192.168.1." + (random.nextInt(200) + 1),
                    Instant.now().minusSeconds(random.nextInt(86400 * 3)).toString()
            );

            seededList.add(ingestAuditEventUseCase.execute(event));
        }

        return ResponseEntity.ok(seededList);
    }
}

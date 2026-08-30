package com.aurora.notification.interface_adapters.controller;

import com.aurora.notification.application.usecase.MarkAllAsReadUseCase;
import com.aurora.notification.application.usecase.MarkAsReadUseCase;
import com.aurora.notification.application.usecase.ProcessDevOpsAlertUseCase;
import com.aurora.notification.application.usecase.SendInAppNotificationUseCase;
import com.aurora.notification.domain.model.Notification;
import com.aurora.notification.domain.model.NotificationPriority;
import com.aurora.notification.domain.model.NotificationStatus;
import com.aurora.notification.domain.model.NotificationType;
import com.aurora.notification.infrastructure.messaging.dto.DevOpsAlertEventDto;
import com.aurora.notification.infrastructure.persistence.SpringDataNotificationRepository;
import com.aurora.notification.infrastructure.persistence.entity.NotificationEntity;
import lombok.Data;
import lombok.RequiredArgsConstructor;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

import java.time.Instant;
import java.util.*;

@RestController
@RequestMapping("/api/v1/notifications")
@RequiredArgsConstructor
public class NotificationController {

    private final ProcessDevOpsAlertUseCase processDevOpsAlertUseCase;
    private final SendInAppNotificationUseCase sendInAppNotificationUseCase;
    private final MarkAsReadUseCase markAsReadUseCase;
    private final MarkAllAsReadUseCase markAllAsReadUseCase;
    private final SpringDataNotificationRepository notificationRepository;

    @PostMapping("/telegram-test")
    public ResponseEntity<String> sendTelegramTest(@RequestBody DevOpsAlertEventDto request) {
        processDevOpsAlertUseCase.execute(request);
        return ResponseEntity.ok("DevOps Telegram Alert dispatched successfully!");
    }

    @PostMapping("/in-app")
    public ResponseEntity<Notification> createInAppNotification(@RequestBody CreateInAppNotificationRequest request) {
        Notification notification = sendInAppNotificationUseCase.execute(
                request.getTenantId(),
                request.getUserId(),
                request.getTitle(),
                request.getBody(),
                request.getActionUrl(),
                request.getPriority()
        );
        return ResponseEntity.ok(notification);
    }

    @GetMapping
    public ResponseEntity<List<NotificationEntity>> getNotifications(
            @RequestHeader(value = "X-Tenant-Id", required = false) String tenantId,
            @RequestParam(value = "userId", required = false) String userId) {
        if (tenantId != null && !tenantId.isBlank()) {
            if (userId != null && !userId.isBlank()) {
                return ResponseEntity.ok(notificationRepository.findByTenantIdAndUserIdOrderByCreatedAtDesc(tenantId, userId));
            }
            return ResponseEntity.ok(notificationRepository.findByTenantIdOrderByCreatedAtDesc(tenantId));
        }
        return ResponseEntity.ok(notificationRepository.findAll());
    }

    @GetMapping("/{id}")
    public ResponseEntity<NotificationEntity> getNotificationById(@PathVariable("id") String id) {
        return notificationRepository.findById(id)
                .map(ResponseEntity::ok)
                .orElseGet(() -> ResponseEntity.notFound().build());
    }

    @GetMapping("/unread-count")
    public ResponseEntity<Map<String, Object>> getUnreadCount(
            @RequestHeader(value = "X-Tenant-Id", required = false) String tenantId,
            @RequestParam(value = "userId", required = false) String userId) {
        long count;
        if (tenantId != null && !tenantId.isBlank()) {
            if (userId != null && !userId.isBlank()) {
                count = notificationRepository.countByTenantIdAndUserIdAndStatus(tenantId, userId, NotificationStatus.PENDING);
            } else {
                count = notificationRepository.countByTenantIdAndStatus(tenantId, NotificationStatus.PENDING);
            }
        } else {
            count = notificationRepository.countByStatus(NotificationStatus.PENDING);
        }
        return ResponseEntity.ok(Map.of("unreadCount", count));
    }

    @PutMapping("/{id}/read")
    public ResponseEntity<NotificationEntity> markAsRead(@PathVariable("id") String id) {
        return markAsReadUseCase.execute(id)
                .map(ResponseEntity::ok)
                .orElseGet(() -> ResponseEntity.notFound().build());
    }

    @PutMapping("/read-all")
    public ResponseEntity<Map<String, Object>> markAllAsRead(
            @RequestHeader(value = "X-Tenant-Id", defaultValue = "tenant-aurora-01") String tenantId,
            @RequestParam(value = "userId", required = false) String userId) {
        int updatedCount = markAllAsReadUseCase.execute(tenantId, userId);
        return ResponseEntity.ok(Map.of("updatedCount", updatedCount, "message", "All notifications marked as read."));
    }

    @DeleteMapping("/{id}")
    public ResponseEntity<Void> deleteNotification(@PathVariable("id") String id) {
        if (notificationRepository.existsById(id)) {
            notificationRepository.deleteById(id);
            return ResponseEntity.noContent().build();
        }
        return ResponseEntity.notFound().build();
    }

    @PostMapping("/shipment-event-test")
    public ResponseEntity<Map<String, Object>> testShipmentEventNotification(@RequestBody ShipmentEventTestRequest request) {
        String title = String.format("Lô hàng #%s: %s", request.getShipmentCode(), request.getStatusName());
        String body = String.format("Lô hàng của %s đã chuyển trạng thái sang '%s'. Vị trí: %s",
                request.getCustomerName(), request.getStatusName(), request.getLocation());

        Notification inApp = sendInAppNotificationUseCase.execute(
                request.getTenantId(),
                request.getUserId(),
                title,
                body,
                "https://aurora.logistics.com/shipments/" + request.getShipmentCode(),
                NotificationPriority.INFO
        );

        // Dispatches to Telegram as well
        DevOpsAlertEventDto telegramAlert = new DevOpsAlertEventDto(
                UUID.randomUUID().toString(),
                "ShipmentWorkflow",
                "Cập nhật trạng thái lô hàng #" + request.getShipmentCode(),
                "INFO",
                body,
                "production",
                Instant.now().toString()
        );
        processDevOpsAlertUseCase.execute(telegramAlert);

        return ResponseEntity.ok(Map.of(
                "status", "SUCCESS",
                "notificationId", inApp.getId(),
                "channelsDispatched", List.of("IN_APP_POPUP", "REDIS_REALTIME", "TELEGRAM_BOT")
        ));
    }

    @PostMapping("/seed-random")
    public ResponseEntity<List<NotificationEntity>> seedRandomNotifications(@RequestParam(value = "count", defaultValue = "5") int count) {
        Random random = new Random();
        List<NotificationEntity> seededList = new ArrayList<>();
        String[] tenants = {"tenant-aurora-01", "tenant-logistics-beta", "tenant-express-global"};
        String[] titles = {
                "Lô hàng #SHIP-%d đã cập bến kho",
                "Cảnh báo nhiệt độ container #C-%d",
                "Tài xế %s đã nhận đơn hàng",
                "Yêu cầu duyệt chứng từ cho lô hàng #SHIP-%d",
                "Thanh toán hóa đơn #INV-%d hoàn tất"
        };
        String[] names = {"Nguyễn Văn A", "Trần Thị B", "Lê Văn C", "Phạm Hoàng D"};

        for (int i = 0; i < count; i++) {
            String tenantId = tenants[random.nextInt(tenants.length)];
            String userId = "user-" + (random.nextInt(10) + 100);
            int code = random.nextInt(9000) + 1000;
            String name = names[random.nextInt(names.length)];
            String title = String.format(titles[random.nextInt(titles.length)], code, name);
            NotificationPriority priority = NotificationPriority.values()[random.nextInt(NotificationPriority.values().length)];

            NotificationEntity entity = new NotificationEntity(
                    UUID.randomUUID().toString(),
                    tenantId,
                    userId,
                    NotificationType.IN_APP,
                    priority,
                    NotificationStatus.PENDING,
                    title,
                    "Chi tiết tự động sinh cho thông báo ngẫu nhiên #" + code,
                    "https://aurora.logistics.com/shipments/" + code,
                    Instant.now().minusSeconds(random.nextInt(86400 * 5))
            );
            seededList.add(notificationRepository.save(entity));
        }

        return ResponseEntity.ok(seededList);
    }

    @Data
    public static class CreateInAppNotificationRequest {
        private String tenantId;
        private String userId;
        private String title;
        private String body;
        private String actionUrl;
        private NotificationPriority priority;

        public String getTenantId() { return tenantId; }
        public void setTenantId(String tenantId) { this.tenantId = tenantId; }

        public String getUserId() { return userId; }
        public void setUserId(String userId) { this.userId = userId; }

        public String getTitle() { return title; }
        public void setTitle(String title) { this.title = title; }

        public String getBody() { return body; }
        public void setBody(String body) { this.body = body; }

        public String getActionUrl() { return actionUrl; }
        public void setActionUrl(String actionUrl) { this.actionUrl = actionUrl; }

        public NotificationPriority getPriority() { return priority; }
        public void setPriority(NotificationPriority priority) { this.priority = priority; }
    }

    @Data
    public static class ShipmentEventTestRequest {
        private String tenantId;
        private String userId;
        private String shipmentCode;
        private String customerName;
        private String statusName;
        private String location;

        public String getTenantId() { return tenantId; }
        public void setTenantId(String tenantId) { this.tenantId = tenantId; }

        public String getUserId() { return userId; }
        public void setUserId(String userId) { this.userId = userId; }

        public String getShipmentCode() { return shipmentCode; }
        public void setShipmentCode(String shipmentCode) { this.shipmentCode = shipmentCode; }

        public String getCustomerName() { return customerName; }
        public void setCustomerName(String customerName) { this.customerName = customerName; }

        public String getStatusName() { return statusName; }
        public void setStatusName(String statusName) { this.statusName = statusName; }

        public String getLocation() { return location; }
        public void setLocation(String location) { this.location = location; }
    }
}

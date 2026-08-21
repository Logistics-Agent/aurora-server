# Notification Event Consumption

Notification hiện không publish integration event. Service subscribe event của các service khác, áp dụng tenant-scoped preferences và tạo Email/InApp notification trong database riêng.

## Consumer endpoints

| Endpoint exchange | Consumer | Contracts |
| --- | --- | --- |
| `ShipmentNotification` | `ShipmentNotificationConsumer` | 8 Shipment contracts |
| `GpsNotification` | `GpsNotificationConsumer` | `GpsMonitoringAlertRaisedEvent` |
| `DocumentOcrNotification` | `DocumentOcrNotificationConsumer` | OCR completed/failed |
| `ComplianceNotification` | `ComplianceNotificationConsumer` | Compliance completed/failed |

## Consumed events

| Source event | Notification event type | Title | Shipment reference |
| --- | --- | --- | --- |
| `ShipmentCreatedEvent` | `ShipmentCreated` | Shipment created | Required |
| `ShipmentSubmittedEvent` | `ShipmentSubmitted` | Shipment submitted | Required |
| `ShipmentStatusChangedEvent` | `ShipmentStatusChanged` | Shipment status updated | Required |
| `ShipmentCancelledEvent` | `ShipmentCancelled` | Shipment cancelled | Required |
| `ShipmentPickedUpEvent` | `ShipmentPickedUp` | Shipment picked up | Required |
| `ShipmentDeliveredEvent` | `ShipmentDelivered` | Shipment delivered | Required |
| `ShipmentCompletedEvent` | `ShipmentCompleted` | Shipment completed | Required |
| `DocumentAttachedEvent` | `DocumentAttached` | Shipment document attached | Required |
| `GpsMonitoringAlertRaisedEvent` | `GpsMonitoringAlertRaised` | GPS monitoring alert | Optional |
| `DocumentOcrCompletedEvent` | `DocumentOcrCompleted` | Document OCR completed | Optional external shipment ID |
| `DocumentOcrFailedEvent` | `DocumentOcrFailed` | Document OCR failed | Optional external shipment ID |
| `ComplianceEvaluationCompletedEvent` | `ComplianceEvaluationCompleted` | Compliance evaluation completed | Required external shipment ID |
| `ComplianceEvaluationFailedEvent` | `ComplianceEvaluationFailed` | Compliance evaluation failed | Required external shipment ID |

## Processing rules

1. Consumer map trusted event metadata sang bounded notification envelope.
2. Lookup enabled preference theo `(TenantId, EventType)`; không dùng tenant của request hiện tại.
3. Tạo một notification cho mỗi enabled recipient/channel.
4. Ghi `ConsumedIntegrationEvent` kể cả khi không có preference để event retry không tạo kết quả khác về sau.
5. Notification và consumed receipt được lưu trong một `SaveChangesAsync`.
6. Unique indexes chống duplicate receipt và duplicate recipient/channel notification.
7. OCR `NormalizedJson` không được đưa vào title/body; body tối đa 2.000 ký tự.
8. GPS position update không được subscribe để tránh notification volume cao.

## Example projected notification

```json
{
  "eventType": "GpsMonitoringAlertRaised",
  "channel": "InApp",
  "title": "GPS monitoring alert",
  "body": "GeofenceExited alert for vehicle vehicle-001: Vehicle exited geofence Port.",
  "shipmentId": null
}
```

## Delivery behavior

* InApp delivery được hoàn tất nội bộ và hỗ trợ read/unread.
* Email đi qua provider interface; SMTP credentials chỉ đến từ deployment configuration.
* Delivery attempt, provider message ID, lỗi, retry count và next-attempt time được persist.
* Notification không gọi ngược database của Shipment, GPS, OCR hoặc Compliance.


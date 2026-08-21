# GPS Tracking Integration Events

GPS Tracking publish position và business monitoring alert qua transactional outbox.

## Events

### GpsPositionUpdatedEvent

* Exchange: `GpsTracking.Contracts.Events:GpsPositionUpdatedEvent`
* Trigger: một device reading hợp lệ được persist.
* Consumer hiện tại trong 5 service: chưa có. Realtime/GPS analytics có thể subscribe sau.
* Payload:

```json
{
  "eventId": "UUID v7",
  "contractVersion": 1,
  "tenantId": "UUID",
  "positionId": "UUID",
  "deviceId": "string",
  "vehicleId": "string",
  "shipmentId": "UUID or null",
  "latitude": 10.7769,
  "longitude": 106.7009,
  "speedKph": 30.0,
  "headingDegrees": 90.0,
  "recordedAt": "RFC3339 UTC"
}
```

### GpsMonitoringAlertRaisedEvent

* Exchange: `GpsTracking.Contracts.Events:GpsMonitoringAlertRaisedEvent`
* Trigger: geofence entry/exit, abnormal stop hoặc signal loss rule tạo alert mới.
* Consumer: Notification.
* Payload:

```json
{
  "eventId": "UUID v7",
  "contractVersion": 1,
  "tenantId": "UUID",
  "alertId": "UUID",
  "alertType": "GeofenceExited",
  "vehicleId": "vehicle-001",
  "shipmentId": "UUID or null",
  "geofenceId": "UUID or null",
  "positionId": "UUID or null",
  "message": "Vehicle exited geofence Port.",
  "occurredAt": "RFC3339 UTC"
}
```

## Reliability and volume

* Position, current-location update, alerts và outbox commit atomically.
* Device retries dedupe theo `(TenantId, DeviceId, ExternalReadingId)`.
* Active monitoring alerts được dedupe đến khi state thay đổi hoặc alert được resolve.
* Outbox uses explicit allowlist, bounded retries và PostgreSQL skip-locked processing.
* Notification chỉ consume business alert, không consume mọi position update.

## Consumed Shipment events

GPS cũng consume:

| Event | Local effect |
| --- | --- |
| `RouteAssignedEvent` | Project vehicle-shipment assignment và tracking state |
| `ShipmentCancelledEvent` | Deactivate assignment/tracking state |
| `ShipmentCompletedEvent` | Complete/deactivate shipment tracking state |

Consumer lưu local projection và inbox receipt; không truy cập Shipment database.


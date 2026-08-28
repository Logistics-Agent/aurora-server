# Đặc tả GPS Tracking & Monitoring Service

Tài liệu mô tả `GpsTrackingService` trong `protos/gps_tracking.proto`.

## 1. Tổng quan

GPS Tracking trả lời vị trí hiện tại của vehicle/shipment, lưu immutable position history, duy trì current-location snapshot, quản lý circular geofence và phát hiện operational alerts.

Service không sở hữu shipment lifecycle, route geometry/optimization, ETA, notification delivery hoặc billing.

## 2. Dữ liệu sở hữu

* `GpsPosition`: immutable device reading.
* `CurrentLocation`: latest accepted reading per tenant/vehicle.
* `VehicleShipmentAssignment` và `ShipmentTrackingState`: local projections từ Shipment events.
* `Geofence` và `GeofencePresence`: circular boundary và state.
* `MonitoringAlert`: signal loss, abnormal stop, geofence entered/exited.
* Inbox receipt và outbox message.

Shipment/route IDs là external references, không cross-service foreign keys.

## 3. gRPC API

| RPC | Chức năng |
| --- | --- |
| `IngestPosition` | Persist một idempotent trusted device reading |
| `GetCurrentLocation` | Query theo vehicle hoặc shipment |
| `ListPositionHistory` | Paged bounded history theo time range |
| `CreateGeofence` | Tạo circular geofence cho vehicle/shipment reference |
| `ListGeofences` | List active/all tenant geofences |
| `SetGeofenceActive` | Activate/deactivate geofence |
| `ListMonitoringAlerts` | Filter/paginate alerts |
| `ResolveMonitoringAlert` | Resolve alert thuộc tenant |

## 4. Functional Requirements

### FR-01: Position ingestion

* Require external reading ID, device ID, vehicle ID và recorded time.
* Deduplicate device retry theo `(TenantId, DeviceId, ExternalReadingId)`.
* Validate latitude `[-90,90]`, longitude `[-180,180]`, non-negative speed/accuracy và heading `[0,360)`.
* Reject reading quá 5 phút trong tương lai hoặc quá 30 ngày cũ.
* Late valid reading được giữ trong history nhưng không lùi current-location snapshot.
* Shipment association được derive từ active local assignment, không lấy từ client.

### FR-02: Queries

* Current/history selector yêu cầu đúng một trong vehicle ID hoặc shipment ID.
* History range tối đa 7 ngày và page size tối đa 500.
* Stable chronological ordering và tenant isolation.

### FR-03: Monitoring

* Circular geofence radius dương, tối đa 100 km.
* Detect entry/exit statefully, không phát duplicate active alert.
* Detect abnormal stop và signal loss theo configuration thresholds.
* Resolve alert khi underlying state hồi phục hoặc user gọi resolve.

### FR-04: Shipment projection

* Consume `RouteAssignedEvent`, `ShipmentCancelledEvent`, `ShipmentCompletedEvent` idempotently.
* Chỉ lưu assignment/tracking references cần thiết.
* Không query Shipment database.
* Lưu inbox receipt với local projection transaction.

### FR-05: Events

Publish `GpsPositionUpdatedEvent` và `GpsMonitoringAlertRaisedEvent` qua outbox. Chi tiết tại [GPS events](documents/events/gps-tracking-events.md).

## 5. Non-functional Requirements

* High-throughput position path phải bounded và idempotent.
* Immutable history; current snapshot update concurrency-safe.
* Outbox/inbox dùng explicit type allowlist và bounded errors/retries.
* Monitoring thresholds cấu hình được, không hard-code theo tenant trong source.
* Missing tenant fail closed; no client-controlled TenantId/ShipmentId.
* Không yêu cầu Realtime Hub chạy để GPS API/test hoạt động.

Local development: gRPC `6002`, PostgreSQL `localhost:5435/aurora_gps_tracking`.

## 6. Test Cases đại diện

| ID | Scenario | Expected result |
| --- | --- | --- |
| GPS-TC-01 | Ingest valid reading | Position/history/current snapshot và outbox đúng |
| GPS-TC-02 | Duplicate external reading | Return existing/no duplicate row |
| GPS-TC-03 | Invalid coordinates/time | `InvalidArgument` |
| GPS-TC-04 | Late reading | History lưu, current snapshot không lùi |
| GPS-TC-05 | Geofence enter rồi exit | Hai stateful alerts, entry resolved/exit active |
| GPS-TC-06 | Repeated same state | Không duplicate active alert |
| GPS-TC-07 | Route assignment event duplicate | Một local assignment/inbox receipt |
| GPS-TC-08 | Cross-tenant current/history | Không leakage |
| GPS-TC-09 | RabbitMQ outbox publish | Contract đúng và message marked processed |

## 7. Trạng thái triển khai

Position ingestion/query, monitoring, Shipment projections, PostgreSQL migration và two-event outbox publisher đã implemented. Route assignment projection chờ Shipment production emitter để chạy end-to-end trong runtime thật.


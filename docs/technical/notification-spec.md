# Đặc tả Notification Service

Tài liệu mô tả `NotificationService` trong `protos/notification.proto` và event-driven delivery behavior.

## 1. Tổng quan

Notification tạo Email/InApp notification từ integration events, quản lý recipient preferences, delivery attempts, retries và read state. Service không sở hữu shipment/GPS/OCR/compliance data và không truy cập database của các producer.

## 2. Dữ liệu sở hữu

* `NotificationMessage`: recipient, source event, channel, title/body, delivery/read status.
* `NotificationPreference`: tenant/user/event/channel preference.
* `NotificationDeliveryAttempt`: attempt number, status, provider ID và bounded error.
* `ConsumedIntegrationEvent`: inbox receipt để event processing idempotent.

`ShipmentId` chỉ là optional external reference, không phải foreign key.

## 3. gRPC API

| RPC | Chức năng |
| --- | --- |
| `ListNotifications` | Paged tenant/user notification list, optional unread filter |
| `MarkNotificationRead` | Mark một InApp notification thuộc current user là read |
| `ListNotificationPreferences` | List preferences của current tenant/user |
| `UpsertNotificationPreference` | Enable/disable event + channel và recipient address |

Supported channels hiện tại: `Email`, `InApp`.

## 4. Functional Requirements

### FR-01: Event consumption

* Consume 8 Shipment, 1 GPS alert, 2 OCR và 2 Compliance event contracts.
* Validate trusted tenant/event metadata.
* Lookup enabled preferences theo tenant + event type.
* Tạo một notification cho mỗi enabled recipient/channel.
* Ghi consumed receipt kể cả khi không có preference.
* Không consume `GpsPositionUpdatedEvent`.
* Không đưa OCR raw `NormalizedJson` vào notification content.

Chi tiết: [Notification event consumption](documents/events/notification-events.md).

### FR-02: Preferences

* Preference unique theo tenant, recipient user, event type và channel.
* Email preference yêu cầu recipient address hợp lệ khi enabled.
* Client không được set TenantId/RecipientUserId thay current identity.
* Invalid event/channel trả `InvalidArgument`.

### FR-03: Delivery

* InApp provider hoàn tất local delivery và hỗ trợ read state.
* Email provider được abstract qua interface; SMTP là deployment adapter hiện tại.
* Mỗi attempt được persist trước/sau provider result.
* Transient failure schedule exponential/bounded retry; permanent failure không retry vô hạn.
* Sent notification không được gửi lại bởi normal worker flow.

### FR-04: Query và read state

* List chỉ trả notification của current tenant/current user.
* Pagination bounded và deterministic.
* Chỉ InApp notification được mark read.
* Cross-tenant hoặc wrong-recipient ID không lộ notification.

## 5. Non-functional Requirements

* At-least-once broker delivery, inbox dedupe và unique notification constraints.
* Event projection + consumed receipt commit atomically.
* Body tối đa 2.000 ký tự; title tối đa 200; provider error được bound.
* Không log SMTP credentials, OCR JSON hoặc sensitive payload không cần thiết.
* Provider integration phải replaceable và test được bằng fake.
* Missing tenant/user context fail closed.
* Real email phụ thuộc SMTP host/from/credentials từ secret configuration.

Local development: gRPC `6001`, PostgreSQL `localhost:5434/aurora_notification`, RabbitMQ `5672`, Redis `6379`.

## 6. Test Cases đại diện

| ID | Scenario | Expected result |
| --- | --- | --- |
| NOT-TC-01 | Upsert InApp preference | Preference created/updated idempotently |
| NOT-TC-02 | Invalid event type | `InvalidArgument` |
| NOT-TC-03 | Event có enabled preference | Notification và inbox receipt được tạo |
| NOT-TC-04 | Duplicate event | Không tạo duplicate notification/receipt |
| NOT-TC-05 | Event tenant khác | Chỉ preference cùng tenant được áp dụng |
| NOT-TC-06 | Event không có preference | Không notification nhưng receipt vẫn được lưu |
| NOT-TC-07 | OCR completed | Content không chứa raw normalized JSON |
| NOT-TC-08 | Provider transient failure | Attempt/error/next retry persisted |
| NOT-TC-09 | Mark read sai user/channel | Reject hoặc NotFound không leakage |
| NOT-TC-10 | RabbitMQ producer flows | GPS/OCR/Compliance/Shipment events tạo đúng notification |

## 7. Trạng thái triển khai

Tenant-safe APIs, 13 event consumers, Email/InApp providers, inbox dedupe, delivery retry và PostgreSQL persistence đã implemented. Notification hiện không publish service-owned integration event.


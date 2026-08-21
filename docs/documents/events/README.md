# Aurora Integration Event Catalog

Thư mục này mô tả các integration event được sở hữu bởi các service Aurora. Tài liệu được đối chiếu với contract, outbox publisher và consumer trong source code; không phải danh sách event dự kiến.

## Quy ước chung

* Transport: MassTransit trên RabbitMQ.
* Serialization: raw JSON để hỗ trợ consumer ngoài .NET.
* Exchange: durable `fanout`, mặc định theo tên đầy đủ của contract, ví dụ `Shipment.Contracts.Events:ShipmentCreatedEvent`.
* Delivery: at-least-once. Consumer phải chấp nhận message trùng và dùng `EventId` để khử trùng.
* Reliability: producer ghi business data và outbox trong cùng transaction; background worker publish và retry có giới hạn.
* Tenant: event tenant-owned phải có `TenantId`; consumer không được lấy tenant từ request client để thay thế metadata đáng tin cậy trong event.
* Versioning: event hiện dùng `ContractVersion = 1`. Chỉ thêm field optional/default-compatible trong cùng version; thay đổi breaking phải tạo contract/version mới.
* Time: timestamp dùng UTC `DateTimeOffset`, serialized theo RFC 3339.

## Danh mục

| Producer | Published contracts | Current consumers |
| --- | ---: | --- |
| [Shipment Workflow](shipment-workflow-events.md) | 11 | Notification, GPS Tracking |
| [Notification](notification-events.md) | 0 | Consume 13 contracts từ các service khác |
| [GPS Tracking](gps-tracking-events.md) | 2 | Notification consume monitoring alert; position update dành cho realtime/integration consumer |
| [Document OCR](document-ocr-events.md) | 2 | Notification |
| [Regulatory Compliance](regulatory-compliance-events.md) | 2 | Notification |
| [IAM & Tenant](iam-events.md) | Theo service IAM | Email Agent, Gateway/cache consumers |

## Ma trận kết nối hiện tại

| Event | Producer | Consumer trong repository |
| --- | --- | --- |
| `ShipmentCreatedEvent` | Shipment | Notification |
| `ShipmentSubmittedEvent` | Shipment | Notification |
| `ShipmentStatusChangedEvent` | Shipment | Notification |
| `ShipmentCancelledEvent` | Shipment | Notification, GPS Tracking |
| `ShipmentPickedUpEvent` | Shipment | Notification |
| `ShipmentDeliveredEvent` | Shipment | Notification |
| `ShipmentCompletedEvent` | Shipment | Notification, GPS Tracking |
| `DocumentAttachedEvent` | Shipment | Notification |
| `RouteAssignedEvent` | Shipment contract | GPS Tracking; production emit chưa được triển khai |
| `GpsMonitoringAlertRaisedEvent` | GPS Tracking | Notification |
| `GpsPositionUpdatedEvent` | GPS Tracking | Chưa có consumer thuộc 5 service này |
| `DocumentOcrCompletedEvent` | Document OCR | Notification |
| `DocumentOcrFailedEvent` | Document OCR | Notification |
| `ComplianceEvaluationCompletedEvent` | Regulatory Compliance | Notification |
| `ComplianceEvaluationFailedEvent` | Regulatory Compliance | Notification |

## Consumer requirements

Consumer phải:

1. Validate `EventId`, `ContractVersion`, `TenantId` và aggregate identifiers.
2. Không truy cập database của producer.
3. Lưu inbox/consumed receipt cùng transaction với local projection khi cần.
4. Không giả định thứ tự tuyệt đối giữa nhiều exchange.
5. Không log raw document JSON, credentials hoặc dữ liệu nhạy cảm không cần thiết.
6. Retry lỗi transient và đưa lỗi poison message vào error queue để điều tra.


# Đặc tả Shipment Workflow Service

Tài liệu mô tả chức năng, ranh giới, yêu cầu phi chức năng và test cases của `ShipmentWorkflowService` trong `protos/shipment_workflow.proto`.

## 1. Tổng quan

Shipment Workflow là owner của shipment aggregate và vòng đời logistics. Service quản lý shipment, cargo, ordered locations, document metadata, business milestones, status history và integration outbox.

Service không sở hữu route optimization, GPS telemetry chi tiết, OCR execution, regulatory decision, notification delivery, billing hoặc object storage.

## 2. Dữ liệu sở hữu

* `Shipment`: business identifier, customer, priority, transport mode, route/vehicle references, planned/actual timestamps và lifecycle status.
* `CargoItem`: quantity, weight, HS code và cargo metadata.
* `ShipmentLocation`: pickup/delivery/stop, sequence, address, coordinates và contact.
* `ShipmentDocument`: metadata, storage reference và controlled OCR metadata.
* `ShipmentMilestone`: business milestone, không phải GPS position history.
* `ShipmentStatusHistory`: immutable lifecycle history.
* `OutboxMessage`: reliable integration-event publication.

Mỗi tenant-owned query được filter theo tenant. External IDs không tạo foreign key sang database service khác.

## 3. gRPC API

| Group | RPC | Chức năng |
| --- | --- | --- |
| Shipment | `CreateShipment` | Tạo shipment, cargo ban đầu, Created history và outbox |
| Shipment | `GetShipment` | Lấy aggregate theo tenant |
| Shipment | `ListShipments` | Phân trang/lọc shipment theo tenant |
| Shipment | `SubmitShipment` | Validate prerequisites và submit |
| Shipment | `UpdateShipment` | Sửa field được phép theo trạng thái |
| Shipment | `UpdateShipmentStatus` | Thực hiện transition qua state machine |
| Shipment | `CancelShipment` | Cancel từ trạng thái cho phép |
| Shipment | `DeleteDraftShipment` | Xóa shipment chưa submit |
| Timeline | `GetShipmentTimeline` | Kết hợp status history và business milestones |
| Cargo | `AddCargoItem`, `UpdateCargoItem`, `RemoveCargoItem` | Quản lý cargo trong aggregate |
| Location | `AddShipmentLocation`, `UpdateShipmentLocation`, `RemoveShipmentLocation` | Quản lý ordered locations |
| Document | `AttachShipmentDocument`, `UpdateShipmentDocumentOcr`, `RemoveShipmentDocument` | Quản lý document metadata/OCR metadata |
| Milestone | `AddShipmentMilestone` | Thêm business milestone |
| Import | `ImportShipments` | Import CSV bounded với row-level result |

Request không có trusted `TenantId`; tenant đến từ authenticated current-user context.

## 4. Functional Requirements

### FR-01: Shipment creation và query

* Tạo shipment với required customer/origin/destination và cargo hợp lệ.
* Sinh shipment number collision-resistant.
* Lưu shipment, children, Created history và `ShipmentCreatedEvent` atomically.
* Get/List không tiết lộ shipment tenant khác; cross-tenant lookup trả NotFound tương đương.
* List có stable ordering, bounded page size và safe filters.

### FR-02: Lifecycle state machine

Lifecycle chính:

```text
Draft/Created -> Submitted -> Planning -> Negotiating -> Confirmed
-> PickedUp -> InTransit -> [CustomsProcessing] -> Delivered -> Completed
```

* Mọi transition phải đi qua domain validation.
* `Completed` và `Cancelled` là terminal.
* Cancellation chỉ cho phép từ state được định nghĩa.
* Transition thêm status history, milestone/timestamp cần thiết và outbox event.
* Client không được gán arbitrary status hoặc bỏ qua transition trung gian.

### FR-03: Cargo và locations

* Quantity/weight phải dương; HS code và metadata được validate.
* Location sequence phải dương, deterministic và không xung đột.
* Latitude thuộc `[-90, 90]`, longitude thuộc `[-180, 180]`.
* Shipment phải có pickup và delivery location trước khi submit.
* Mutation bị hạn chế sau khi operational processing bắt đầu.

### FR-04: Documents và milestones

* Chỉ lưu file metadata/storage URL; không lưu binary file.
* OCR status/confidence chỉ update qua controlled API; confidence thuộc `[0, 1]`.
* Attach document tạo `DocumentAttachedEvent`.
* Business milestones có source, recorded time và optional coordinates hợp lệ.
* Timeline không trả GPS telemetry chi tiết.

### FR-05: Import

* Chỉ hỗ trợ bounded synchronous CSV MVP.
* Reject oversized input, missing columns và client-supplied TenantId.
* Validate từng row qua domain rules và trả row-level success/error.
* Valid rows có thể commit độc lập theo documented partial-success policy.
* Shipment tạo thành công có `ShipmentCreatedEvent` outbox.

### FR-06: Integration events

Publish versioned events qua outbox; chi tiết tại [Shipment events](documents/events/shipment-workflow-events.md). Không publish trực tiếp từ command handler.

## 5. Non-functional Requirements

* .NET 10, gRPC, EF Core/PostgreSQL, MediatR và MassTransit/RabbitMQ.
* Separate database; không cross-service query hoặc foreign key.
* At-least-once event delivery và idempotent consumer expectation.
* Outbox worker dùng bounded batch/retry và `FOR UPDATE SKIP LOCKED`.
* Missing tenant context fail closed; không disable query filter.
* Validation error trả gRPC `InvalidArgument`; missing aggregate trả `NotFound`; missing identity trả `Unauthenticated`.
* Secrets và production connection strings chỉ đến từ deployment configuration.

Local development: gRPC `6000`, PostgreSQL `localhost:5433/aurora_shipment_workflow`. Azure deployment thay endpoint/connection bằng environment configuration.

## 6. Test Cases đại diện

| ID | Scenario | Expected result |
| --- | --- | --- |
| SHP-TC-01 | Create shipment hợp lệ | Aggregate, history và outbox commit cùng nhau |
| SHP-TC-02 | Missing tenant | `Unauthenticated`; không ghi dữ liệu |
| SHP-TC-03 | Cross-tenant Get/Update | Không lộ existence và không mutate |
| SHP-TC-04 | Submit thiếu pickup/delivery | Reject validation, state không đổi |
| SHP-TC-05 | Complete lifecycle | Mọi transition, history, milestones và timestamps đúng |
| SHP-TC-06 | Invalid/terminal transition | Reject; không tạo history/outbox giả |
| SHP-TC-07 | Cargo/location/document mutation | Validate ownership, values và state restrictions |
| SHP-TC-08 | Mixed CSV import | Row results đúng, không partial corruption |
| SHP-TC-09 | Duplicate outbox processing | Event identity ổn định, processed state/retry đúng |
| SHP-TC-10 | PostgreSQL migration/cascade | Schema, indexes và aggregate cascade đúng |

## 7. Trạng thái triển khai

Full Shipment MVP, migrations, outbox publisher và tenant-safe gRPC flows đã implemented. `RouteAssignedEvent` đã có contract/type registry và GPS consumer nhưng chưa có production command tạo event; đây là integration gap được ghi rõ, không phải RPC hiện hành.


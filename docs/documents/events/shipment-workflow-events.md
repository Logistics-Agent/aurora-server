# Shipment Workflow Integration Events

Shipment Workflow là owner của shipment aggregate và publish event thông qua transactional outbox. Registry hiện hỗ trợ 11 contract version 1.

## Common envelope

Mọi event có:

| Field | Type | Required | Meaning |
| --- | --- | --- | --- |
| `eventId` | UUID v7 | Yes | Idempotency key toàn cục của event |
| `contractVersion` | integer | Yes | Phiên bản contract, hiện là `1` |
| `tenantId` | UUID | Yes | Tenant sở hữu shipment |
| `shipmentId` | UUID | Yes | Shipment aggregate identifier |

## Published events

| Event / exchange | Trigger | Additional payload | Current consumers | Status |
| --- | --- | --- | --- | --- |
| `ShipmentCreatedEvent`<br>`Shipment.Contracts.Events:ShipmentCreatedEvent` | CreateShipment hoặc một CSV import row thành công | `shipmentNumber`, `orderId?`, `createdAt` | Notification | Implemented |
| `ShipmentSubmittedEvent`<br>`Shipment.Contracts.Events:ShipmentSubmittedEvent` | Shipment chuyển sang Submitted | `shipmentNumber`, `currentStatus`, `submittedAt` | Notification | Implemented |
| `ShipmentUpdatedEvent`<br>`Shipment.Contracts.Events:ShipmentUpdatedEvent` | Editable shipment fields thay đổi | `shipmentNumber`, `currentStatus`, `changedFields[]`, `updatedAt` | Chưa có consumer thuộc 5 service | Implemented |
| `ShipmentCancelledEvent`<br>`Shipment.Contracts.Events:ShipmentCancelledEvent` | Cancel từ trạng thái cho phép | `reason?`, `cancelledAt` | Notification, GPS Tracking | Implemented |
| `ShipmentStatusChangedEvent`<br>`Shipment.Contracts.Events:ShipmentStatusChangedEvent` | Mọi transition hợp lệ | `oldStatus`, `newStatus`, `note?`, `changedAt` | Notification | Implemented |
| `CargoUpdatedEvent`<br>`Shipment.Contracts.Events:CargoUpdatedEvent` | Add, update hoặc remove cargo | `cargoItemId`, `action`, `updatedAt` | Chưa có consumer thuộc 5 service | Implemented |
| `DocumentAttachedEvent`<br>`Shipment.Contracts.Events:DocumentAttachedEvent` | Attach document metadata | `documentId`, `documentType`, `fileName`, `attachedAt` | Notification | Implemented |
| `RouteAssignedEvent`<br>`Shipment.Contracts.Events:RouteAssignedEvent` | Route/vehicle assignment | `shipmentNumber`, `routeId`, `vehicleId?`, `assignedAt` | GPS Tracking | Contract và consumer implemented; production emitter chưa có |
| `ShipmentPickedUpEvent`<br>`Shipment.Contracts.Events:ShipmentPickedUpEvent` | Transition sang PickedUp | `shipmentNumber`, `currentStatus`, `pickedUpAt` | Notification | Implemented |
| `ShipmentDeliveredEvent`<br>`Shipment.Contracts.Events:ShipmentDeliveredEvent` | Transition sang Delivered | `shipmentNumber`, `currentStatus`, `deliveredAt` | Notification | Implemented |
| `ShipmentCompletedEvent`<br>`Shipment.Contracts.Events:ShipmentCompletedEvent` | Transition sang Completed | `shipmentNumber`, `currentStatus`, `completedAt` | Notification, GPS Tracking | Implemented |

Exchange được tạo khi contract lần đầu được publish. Vì vậy exchange của contract implemented nhưng chưa phát sinh message có thể chưa xuất hiện trong RabbitMQ runtime.

## Example: ShipmentSubmittedEvent

```json
{
  "eventId": "019fa9d8-59ce-7a4c-b324-676ab8eb678b",
  "contractVersion": 1,
  "tenantId": "01920000-0000-7000-8000-000000000001",
  "shipmentId": "019fa99c-4ab3-733d-9da3-91f7eb8f3790",
  "shipmentNumber": "SHP-20260728-019FA99C4A",
  "currentStatus": "Submitted",
  "submittedAt": "2026-07-28T17:49:13.8208712Z"
}
```

## Publication and compatibility

* Command handler tạo outbox message trong cùng `SaveChangesAsync` với shipment state.
* Publisher deserialize qua explicit type registry; unknown event type bị từ chối.
* Worker dùng PostgreSQL `FOR UPDATE SKIP LOCKED`, bounded batch/retry và lưu lỗi publish.
* Consumer phải dedupe theo cặp event type và `EventId`.
* Không thêm navigation object, database key nội bộ ngoài contract, raw file content hoặc client-controlled `TenantId`.


# Đặc tả Notification FCM Service

Notification nhận event từ RabbitMQ/MassTransit, lưu lịch sử in-app và gửi
FCM tới các device active. Service có database riêng, không đọc database của
Shipment, GPS, OCR hoặc Compliance.

## Boundary và auth

Luồng browser là `FE -> YARP -> Staff.Bff -> Notification gRPC`. Mọi route BFF
cần JWT và permission trực tiếp `notifications:access`. BFF truyền user/tenant
context cùng service credential riêng; Notification xác thực service trước
shared `AuthInterceptor`, sau đó áp tenant/user scope cho mọi query.

## Dữ liệu sở hữu

`notifications`, `notification_devices`, `notification_subscriptions`,
`notification_delivery_attempts`, và `processed_notification_events`.
Receipt event unique theo `(TenantId, EventId, Rule)` và phân loại
`AudienceResolved` hoặc `NoRecipient`.

## gRPC/BFF API

| BFF route | RPC | Chức năng |
| --- | --- | --- |
| `POST /api/v1/notifications/devices` | `RegisterDevice` | Register/refresh FCM token |
| `DELETE /api/v1/notifications/devices/{id}` | `RemoveDevice` | Deactivate device của current user |
| `POST /api/v1/notifications/subscriptions/shipments/{id}` | `SubscribeShipment` | Subscribe shipment |
| `GET /api/v1/notifications` | `ListNotifications` | Lịch sử có pagination/unread filter |
| `GET /api/v1/notifications/unread-count` | `GetUnreadCount` | Số unread |
| `PATCH /api/v1/notifications/{id}/read` | `MarkNotificationRead` | Mark một notification read |
| `PATCH /api/v1/notifications/read-all` | `MarkAllNotificationsRead` | Mark tất cả read |

Request gRPC không có `TenantId`/`UserId`; service lấy từ authenticated context.

## Event và delivery

Notification consume 13 event contracts: 8 Shipment lifecycle/document, GPS
monitoring alert, 2 OCR, và 2 Regulatory. Recipient hiện tại chỉ là user có
subscription cho trusted shipment ID. Event thiếu shipment ID hoặc không có
subscriber sẽ ghi `NoRecipient`, không broadcast tenant.

Processor ghi receipt và toàn bộ notification projection trong một transaction.
FCM payload có title/body và data `notificationId`, `type`, `shipmentId`,
`actionUrl`; action URL chỉ là internal allowlisted path. `Unregistered` làm
device inactive, transient errors retry bounded, invalid payload là permanent.

## Configuration

Firebase Admin JSON chỉ đặt tại `secrets/firebase/aurora-notification-admin.json`
(ignored), truyền qua `Firebase__CredentialsPath`. Service key truyền qua
`ServiceAuth__ApiKey` và `Grpc__Notification__ServiceApiKey`; không commit hoặc
log secret. `/health` và `/ready` chỉ trả trạng thái an toàn.

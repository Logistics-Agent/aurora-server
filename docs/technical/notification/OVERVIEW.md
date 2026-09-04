# Notification FCM Service Overview

Notification is a .NET 10 service that consumes approved producer events and
delivers authenticated, tenant-scoped in-app notifications through Firebase
Cloud Messaging (FCM). It owns its PostgreSQL database, device tokens,
shipment subscriptions, notification history, idempotency receipts, and
delivery attempts.

```text
FE -> YARP -> Staff.Bff --gRPC--> Notification
                                  ^
Shipment/GPS/OCR/Regulatory outbox -> RabbitMQ -> consumers -> FCM
```

The browser never calls Notification directly and never receives Firebase Admin
credentials. Staff BFF requires an authenticated session plus
`notifications:access`, forwards user/tenant context, and adds a Notification-
specific service credential. Notification validates that credential before the
shared identity interceptor and scopes all user operations by authenticated
tenant and user.

Notification consumes thirteen contracts: eight Shipment lifecycle/document
events, GPS monitoring alerts, OCR completed/failed, and Regulatory evaluation
completed/failed. Current contracts do not carry a recipient user, so only
users subscribed to the trusted shipment ID receive a notification. Missing
shipment IDs and empty audiences become `NoRecipient`, never tenant broadcasts.

FCM payloads contain title/body and data fields `notificationId`, `type`,
`shipmentId`, and internal `actionUrl`. Invalid tokens are deactivated;
transient failures use bounded retry. Duplicate event delivery is blocked by
the unique `(TenantId, EventId, Rule)` receipt.

Local Firebase Admin JSON belongs only at the ignored path
`secrets/firebase/aurora-notification-admin.json` and is referenced through
`Firebase__CredentialsPath`. See `docs/technical/notification/FCM-LOCAL.md`.

# Notification FCM Interview Q&A

### How is duplicate delivery prevented?

Notification writes a unique `(TenantId, EventId, Rule)` receipt together with
all per-user notification projections. RabbitMQ redelivery observes that receipt
and does not create or send another notification.

### How are recipients selected?

The current event contracts do not contain a recipient user. Notification uses
only the tenant and trusted shipment ID to resolve explicit shipment
subscriptions. No shipment ID or no subscribers means `NoRecipient`; it never
broadcasts to an entire tenant.

### How does the popup reach the browser?

The frontend registers an FCM Web token through Staff BFF. Notification sends a
title/body plus `notificationId`, `type`, `shipmentId`, and internal `actionUrl`.
The frontend handles foreground `onMessage` with a toast/popup and background
messages with `firebase-messaging-sw.js`, validating the action path before
navigation.

### What happens when FCM rejects a token?

An `Unregistered` response marks only that device inactive. Transient provider
failures are persisted and retried with bounded backoff; invalid payloads and
exhausted retries become terminal failures.

### How is the service secured?

Every BFF route requires JWT authentication and `notifications:access`.
Notification validates the BFF service credential before accepting forwarded
identity metadata, then applies tenant/user-scoped queries. Firebase Admin JSON
is server-only and injected at runtime, never committed or sent to the client.

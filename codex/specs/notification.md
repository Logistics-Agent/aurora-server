# Notification Service Specification

## Purpose

Notification owns authenticated in-app notification history, FCM device tokens,
shipment subscriptions, event idempotency receipts, and push delivery attempts.
It consumes approved integration events and never reads another service database.

## Boundaries and authorization

The browser boundary is `FE -> YARP -> Staff.Bff -> Notification gRPC`.
Every BFF notification action requires an authenticated session and the direct
capability `notifications:access`; roles do not replace that permission.
Staff BFF sends user/tenant context plus a Notification-only service credential.
Notification validates the service credential before the shared gRPC auth
interceptor and then scopes every query to the authenticated tenant and user.

## Owned data

- `notifications`: title, body, type, priority, read state, optional shipment
  reference, and an allowlisted internal action path.
- `notification_devices`: active FCM token, platform, and last-seen timestamp.
- `notification_subscriptions`: `(TenantId, UserId, ShipmentId)` subscriptions.
- `notification_delivery_attempts`: provider result, bounded error, attempt count,
  next-attempt time, and terminal state.
- `processed_notification_events`: unique `(TenantId, EventId, Rule)` receipt with
  `AudienceResolved` or `NoRecipient` outcome and recipient count.

The service does not own shipment, GPS, OCR, regulatory, billing, or identity data.
It does not own Firebase Web configuration or expose Admin credentials to clients.

## Public contract

Staff BFF exposes authenticated REST routes for device registration/removal,
shipment subscription, notification listing, unread count, mark-read, and
mark-all-read. The downstream gRPC request messages contain no client-controlled
`TenantId` or `UserId`; those values come from current-user context.

## Event consumers and audience

Notification consumes thirteen events: Shipment created, submitted, status
changed, cancelled, picked up, delivered, completed, and document attached;
GPS monitoring alert; OCR completed and failed; Regulatory evaluation completed
and failed. Producer services publish through their own transactional outboxes.

Current contracts do not carry an explicit recipient user. Therefore Notification
resolves only users subscribed to the trusted shipment ID. An absent shipment ID
or empty audience creates a durable `NoRecipient` receipt, creates no notification,
and never broadcasts to a tenant.

## Idempotency and delivery

The full audience is resolved before the receipt and all per-user projections are
written in one transaction. A duplicate event returns after the unique receipt is
observed. Delivery attempts are durable; invalid FCM tokens are deactivated,
transient provider failures use bounded exponential backoff, and permanent
payload failures are terminal. Tokens and credentials are never logged.

The FCM payload contains notification title/body plus data fields
`notificationId`, `type`, `shipmentId`, and internal `actionUrl`. Raw OCR JSON,
credentials, and sensitive provider payloads are excluded from notification text.

## Configuration and migration

Checked-in configuration keeps Firebase disabled and service API-key values empty.
Runtime injects `Firebase__CredentialsPath` pointing to an ignored server-only
Admin JSON and injects the same service key through
`ServiceAuth__ApiKey` and `Grpc__Notification__ServiceApiKey`.
`/health` and `/ready` report safe configuration status without secret content.

The current additive migration is
`20260830023639_NotificationFcmAudience`. It must only be applied after verifying
the target database migration history. The currently running local database has
legacy migration `20260719124939_InitialNotification` and is not a safe target
for this migration without a reviewed baseline/data migration.

## Verification status (2026-08-30)

Notification builds successfully with zero warnings/errors. The Notification
test project passes 41 tests covering domain bounds, tenant-safe queries,
multi-recipient atomicity, duplicate/no-recipient behavior, FCM outcomes,
retry scheduling, and gRPC service authentication. Staff BFF restores and builds
successfully; dependency advisory warnings remain. Real popup delivery remains
blocked until the legacy database is reconciled, the services can bind/connect in
the runtime environment, and a browser FCM registration token is available.

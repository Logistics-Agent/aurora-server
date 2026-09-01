# Notification FCM Technical Details

## Event processing

Each consumer maps trusted event metadata to a bounded notification envelope.
The processor resolves the shipment subscriber audience first, then writes one
processed-event receipt and all per-user notification rows in one transaction.
The unique receipt key is `(TenantId, EventId, Rule)`. A duplicate returns
without creating another row or sending another FCM attempt.

```text
event -> validate/bound text -> resolve shipment subscribers
      -> transaction(receipt + notification projections)
      -> active devices -> FCM attempt/result
```

The service does not include raw OCR JSON, credentials, arbitrary URLs, or
sensitive provider payloads in the notification. `actionUrl` is generated as
an internal route such as `/shipments/{id}` or `/notifications`.

## Delivery state

Every `(NotificationId, DeviceId)` pair has one durable delivery attempt. The
attempt stores provider message ID, bounded error code, attempt count,
`NextAttemptAt`, and terminal status. `Unregistered` FCM responses deactivate
the device. Transient responses are retried with capped exponential backoff;
invalid payloads and exhausted retries are terminal failures.

## Tenant and service boundaries

Notification never accesses another service database. Producer outboxes are the
only event source. gRPC request messages intentionally contain no tenant or
user fields; current-user context supplies both. BFF routes return `404` for a
notification/device outside the current tenant/user scope.

## Runtime

Checked-in Firebase and service-key settings are empty/disabled. Deployment
injects `ServiceAuth__ApiKey`, `Grpc__Notification__ServiceApiKey`, and the
server-only Firebase credential path. `/health` and `/ready` expose safe
configuration health without secret content.

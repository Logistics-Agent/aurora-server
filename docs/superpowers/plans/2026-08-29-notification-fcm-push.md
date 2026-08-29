# Notification FCM Push Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the empty `.NET Notification` service so authenticated web users can register FCM devices, receive shipment-driven browser popups, and view durable in-app notification history without violating Aurora's service and tenant boundaries.

**Architecture:** Shipment Workflow remains the source of shipment truth and publishes versioned events through its outbox and RabbitMQ/MassTransit. Notification consumes those events, resolves recipients from explicit event/API data, persists a tenant-scoped notification and delivery state, then sends FCM messages through an `IFcmPushProvider` abstraction. The browser renders foreground messages with `onMessage` and background messages through a Firebase service worker.

**Tech Stack:** .NET 10, ASP.NET Core, gRPC/REST at the BFF boundary, EF Core, PostgreSQL, MediatR/CQRS where existing service conventions require it, MassTransit, RabbitMQ, Firebase Admin SDK for .NET, xUnit, deterministic fake FCM provider.

**Spec:** `codex/specs/notification.md`, `codex/specs/logistics-architecture.md`, `codex/requirement.md`, `codex/tasks/notification/phase-01-project-foundation.md` through `phase-09-testing.md`.

## Global Constraints

- Notification is an independent service with its own PostgreSQL database and deployment boundary.
- Never read or write Shipment Workflow, IAM, or any other service database directly.
- Use gRPC or integration events for cross-service communication; use the existing Shipment outbox and RabbitMQ/MassTransit path.
- Resolve `TenantId` and `UserId` from authenticated context for client APIs; never trust client-supplied tenant ownership.
- Event consumers must be duplicate-safe, retry-aware, and safe to replay.
- Store shipment/customer/recipient references as external IDs only; do not create cross-service foreign keys.
- Firebase credentials and FCM tokens must not be committed or written to logs.
- Automated tests must use a fake provider and must not require paid Firebase credentials.
- Do not modify Java notification code or any source outside `src/dotnet/Notification` and the explicitly required shared contract files.
- Do not create `feat/notification-service` or update service progress until implementation is explicitly authorized.

## File Map

Create the following files under `src/dotnet/Notification`:

- `Notification.csproj`: .NET 10 executable project and references to shared building blocks and `Shipment.Contracts`.
- `Program.cs`: dependency injection, authentication/current-user context, EF Core, MassTransit consumers, health checks, and API endpoints.
- `appsettings.json` and `appsettings.Development.json`: non-secret defaults and local dependency settings; secrets come from environment/secret manager.
- `Domain/Entities/Notification.cs`: durable user-facing notification aggregate/entity.
- `Domain/Entities/NotificationDevice.cs`: tenant/user-owned FCM registration token record.
- `Domain/Entities/NotificationDeliveryAttempt.cs`: per-provider/per-device delivery history.
- `Domain/Entities/ProcessedNotificationEvent.cs`: inbox/idempotency record keyed by event ID and notification rule.
- `Domain/Enums/NotificationChannel.cs`, `NotificationStatus.cs`, `NotificationPriority.cs`, `DevicePlatform.cs`, `DeliveryAttemptStatus.cs`.
- `Application/Interfaces/IFcmPushProvider.cs`, `IRecipientResolver.cs`, `INotificationRule.cs`: provider and recipient boundaries.
- `Application/DTOs/Notifications/NotificationDtos.cs` and `DeviceDtos.cs`: API request/response contracts without EF entities.
- `Application/Commands/Devices/RegisterNotificationDeviceCommand.cs` and `RemoveNotificationDeviceCommand.cs`.
- `Application/Commands/Notifications/MarkNotificationReadCommand.cs` and `MarkAllNotificationsReadCommand.cs`.
- `Application/Queries/Notifications/ListNotificationsQuery.cs` and `GetUnreadNotificationCountQuery.cs`.
- `Infrastructure/Persistences/NotificationDbContext.cs` and entity configurations/migrations.
- `Infrastructure/Firebase/FirebaseOptions.cs`, `FirebaseAdminInitializer.cs`, `FirebasePushProvider.cs`, and `FakeFcmPushProvider.cs`.
- `Infrastructure/Messaging/Consumers/ShipmentCreatedConsumer.cs`, `ShipmentStatusChangedConsumer.cs`, `ShipmentCancelledConsumer.cs`, `ShipmentDeliveredConsumer.cs`.
- `Infrastructure/Messaging/NotificationEventProcessor.cs`: transaction, deduplication, notification creation, and dispatch orchestration.
- `Infrastructure/BackgroundJobs/NotificationDeliveryWorker.cs`: retrying pending delivery attempts if dispatch is asynchronous.
- `GrpcServices/NotificationGrpcService.cs` or the repository-approved service API surface for internal calls.

Create tests under `tests/dotnet/Notification.Tests`:

- Domain validation and state transition tests.
- Device registration and tenant-isolation tests.
- Consumer/idempotency tests using real `Shipment.Contracts` event types.
- Fake FCM provider and delivery-attempt tests.
- PostgreSQL/Testcontainers persistence and migration tests where the repository test conventions support them.

## Code Audit: Done, Partial, Missing, and Required to Finish

This matrix records the current repository state as checked on 2026-08-29. “Done” means reusable code already exists; it does not mean the FCM feature is complete.

| Area | Current status | Evidence | Required to finish |
|---|---|---|---|
| `.NET Notification` project | Missing | `src/dotnet/Notification` is empty | Create `Notification.csproj`, `Program.cs`, configuration, service registration, health checks, and tests |
| Notification database | Missing | No Notification DbContext/entities/migrations exist | Create a dedicated database, model, indexes, migration, and migration verification |
| Current user context | Done as shared foundation | `src/dotnet/shared/Security/ICurrentUserService.cs` exposes `UserId`, `TenantId`, permissions, and roles | Register it in Notification and enforce it on every API; do not trust request tenant/user fields |
| Shipment event contracts | Done as producer foundation | `src/dotnet/Contracts/Shipment.Contracts/Events/*.cs` contains versioned events with `EventId` and `TenantId` | Reuse contracts and add recipient metadata only if the chosen resolver requires a shared contract change |
| Shipment outbox writes | Done for Shipment Workflow flows | `src/dotnet/ShipmentWorkflow/Domain/OutboxMessage.cs` and shipment command helpers persist event payloads | Verify the publisher/broker path and consume from Notification without database access |
| RabbitMQ/MassTransit convention | Done as shared foundation | `src/dotnet/shared/Extensions/MessagingExtensions.cs` configures RabbitMQ, raw JSON, and bounded retry | Register Notification consumers, queue names, bindings, and consumer-specific idempotency |
| FCM provider | Missing | No Firebase/FCM references under `src/dotnet/Notification` | Add Firebase Admin SDK behind `IFcmPushProvider`, production secret configuration, and fake provider |
| FCM device registration | Missing | No device entity or endpoint exists | Add authenticated register/deactivate APIs, token upsert, multi-device support, and validation |
| FCM token lifecycle | Missing | No token/device table or invalid-token handling exists | Track active/last-seen state, token replacement, logout deactivation, and invalid-token cleanup |
| Recipient resolution | Incomplete at contract level | Shipment events have shipment/tenant identity but do not consistently carry recipient identity | Select and implement an explicit resolver, then test “no audience” as a non-broadcast failure |
| Notification payload contract | Missing | No Notification DTO or FCM message model exists | Define stable notification/data fields, enum values, internal `actionUrl` policy, and frontend handoff |
| Foreground popup | Missing in backend and frontend scope | No `.NET Notification` or frontend FCM handler exists in this scope | Frontend must implement `onMessage` and render the toast; backend only sends payload |
| Background popup | Missing in frontend scope | No `firebase-messaging-sw.js` exists in this backend repository scope | Frontend must add service worker, permission flow, VAPID configuration, and click handling |
| Notification history/read APIs | Missing in `.NET Notification` | No API or DTO exists | Add paged list, unread count, mark-read, and mark-all-read with tenant/user authorization |
| Delivery attempts | Missing | No delivery-attempt entity/service exists | Persist per-device result, provider ID, error category, retry count, and terminal status |
| Idempotency/inbox | Missing | No processed-event table exists | Add unique `(EventId, Rule, UserId)` or equivalent key and transactionally claim events |
| Retry worker | Missing | No Notification hosted worker exists | Add bounded retry worker, concurrency control, backoff, invalid-token handling, and metrics |
| BFF route to Notification | Missing/needs verification | Existing BFF shared code propagates identity headers, but no Notification gRPC client/route is present | Add authenticated BFF endpoints or gRPC client mapping for device and notification APIs; never expose Notification directly to the browser |
| Frontend authentication integration | Foundation exists, notification integration missing | BFF has Cognito/current-user middleware and tenant resolution | Forward the authenticated session through the existing BFF path and register the FCM token only after login/permission grant |
| Security controls | Partial foundation | Shared auth, tenant middleware, rate-limit/security middleware exist in BFF; Notification has no code | Apply authorization, input limits, rate limiting, secret hygiene, action URL allowlist, safe errors, and token redaction |
| Observability | Shared building blocks exist, feature instrumentation missing | Shared trace/correlation patterns exist; no Notification metrics/logging exist | Add correlation/event/notification/attempt IDs, delivery metrics, retry backlog metrics, and health checks |
| Automated verification | Missing for Notification | No `tests/dotnet/Notification.Tests` exists | Add unit, consumer, persistence, migration, security, idempotency, and fake-FCM integration tests |

### Completion gate

The feature is not complete until every row marked Missing or Incomplete has an implementation and test, and these runtime paths pass together:

```text
FE login
→ BFF authenticated context
→ register FCM device
→ Shipment Workflow mutation
→ Shipment outbox
→ RabbitMQ
→ Notification consumer
→ recipient resolution
→ Notification DB
→ FCM provider
→ FE foreground/background popup
→ notification history/read state
```

The following failure paths are also required before completion:

- [ ] No authenticated user/tenant: reject device registration and notification queries.
- [ ] Missing event audience: do not broadcast; record a classified processing outcome.
- [ ] Duplicate event: acknowledge safely without a duplicate notification.
- [ ] FCM transient error: retry within the configured bound and preserve attempt history.
- [ ] Invalid FCM token: deactivate only the invalid device.
- [ ] Cross-tenant ID: return safe not-found/forbidden behavior and expose no data.
- [ ] Firebase credential missing in production: fail health/startup validation clearly without logging the secret.
- [ ] Frontend permission denied: keep in-app history functional and do not treat permission denial as a server error.

## Contract Decisions Before Coding

The current Shipment event contracts contain `EventId`, `ContractVersion`, `ShipmentId`, `TenantId`, and shipment status data, but do not consistently identify notification recipients. Before implementing consumers, choose and document one concrete recipient path:

1. **Preferred:** add a versioned recipient/audience field to the producing event when the producer already knows the authorized recipient, for example `RecipientUserIds` or an external `CustomerId`.
2. **Alternative:** define `IRecipientResolver` backed by an explicit gRPC contract to the owning identity/customer service. It must not query a database directly.
3. **Fallback for user-followed shipments:** add a Notification-owned subscription endpoint and resolve recipients from Notification's own `ShipmentSubscription` records.

The first implementation must not silently broadcast a shipment event to every user in a tenant.

## Implementation Checkpoint

Status at the current implementation checkpoint: **In progress**.

Completed in `src/dotnet/Notification`:

- [x] .NET 10 executable project referencing shared services and `Shipment.Contracts`.
- [x] Tenant-scoped notification, device, shipment subscription, delivery-attempt, and processed-event entities.
- [x] PostgreSQL DbContext and initial migration `20260828174408_InitialNotification`.
- [x] JWT/current-user middleware and device registration/deactivation endpoints.
- [x] Subscription endpoint used as the initial explicit recipient-resolution strategy.
- [x] Firebase Admin SDK provider and deterministic fake provider.
- [x] Shipment created/status-changed/cancelled/delivered consumers using MassTransit.
- [x] Notification list, unread-count, mark-read, and mark-all-read endpoints.
- [x] Delivery-attempt persistence, bounded retry worker, and invalid-token deactivation.
- [x] Domain and processor tests; latest result is 3 passed.

Still required before the plan can be marked complete:

- [ ] Add BFF routing/client integration so the browser reaches Notification through the authenticated gateway.
- [ ] Add frontend Firebase setup, service worker, permission flow, token refresh handling, `onMessage`, and popup click navigation.
- [ ] Add consumer tests for every supported event and explicit no-recipient behavior.
- [ ] Add API tenant/user-isolation tests and migration/database integration tests.
- [ ] Verify real local PostgreSQL/RabbitMQ startup and event delivery; Firebase production send remains a secret-configured smoke test.
- [ ] Resolve package/security warnings before release, including the inherited `Microsoft.SemanticKernel.Core` critical advisory and the test SQLite dependency advisory.
- [ ] Update the nine Notification phase work logs and `codex/plan.md` only after all completion criteria pass.

## End-to-End Flows: FE → BFF → Services → FE

This section is the runtime contract for the whole logistics path. The frontend talks to the API Gateway/BFF, never directly to PostgreSQL, RabbitMQ, Firebase Admin, or another service's private API. The BFF forwards the authenticated identity and tenant context; each owning service validates authorization again.

### Service roles in the flow

| Component | Responsibility in this plan | Communication with Notification |
|---|---|---|
| Frontend | Authenticated UI, FCM token registration, foreground toast, service-worker popup, notification list/read state | Calls BFF APIs and receives FCM messages |
| API Gateway/BFF | TLS, Cognito/session validation, current-user and tenant context, API composition/routing | Proxies device and notification APIs; does not send FCM |
| IAM/Tenant | User, tenant, role, permission and customer identity ownership | Supplies trusted identity through auth context or an explicit contract; no DB sharing |
| Shipment Workflow | Shipment aggregate, lifecycle, cargo, locations, milestones and shipment outbox | Publishes versioned shipment events consumed by Notification |
| Route Planning | Route assignment/planning and route-owned events | Notification may consume an explicit route event if a notification rule is defined |
| GPS Tracking | Positions, geofences and monitoring alerts | Notification may consume explicit alert events; it does not read GPS data |
| Document OCR | OCR jobs, extraction and confidence | Notification may consume job/result events; it does not read OCR data |
| Compliance RAG | Compliance decision, evidence and violations | Notification may consume explicit compliance events; it does not read compliance data |
| Notification | Notification history, device registrations, templates/rules, attempts, FCM dispatch | Consumes events and exposes notification/device APIs |
| FCM | External push transport | Receives server-side messages and delivers them to the browser service worker/page |

### Flow A — Authentication and application bootstrap

```text
FE → Cognito/IAM: sign in or refresh session
IAM → FE/BFF: authenticated session/JWT
FE → API Gateway/BFF: request with session
BFF → downstream service: authenticated identity + trusted tenant context
downstream service → BFF → FE: authorized response
```

Implementation requirements:

- [ ] Use the existing Cognito/current-user middleware and shared `ICurrentUserService` conventions.
- [ ] Reject requests without authenticated user or tenant context.
- [ ] Do not accept `TenantId` as an authority from query, body, or header; if a header is propagated internally, validate it against the authenticated context.
- [ ] Return generic authorization/not-found errors without stack traces or database details.

### Flow B — FE registers an FCM device

```text
FE → Firebase Web SDK: request browser permission
Firebase Web SDK → FE: FCM registration token
FE → BFF: POST /api/v1/notification-devices { token, platform }
BFF → Notification: authenticated register-device command
Notification → Notification DB: upsert (TenantId, UserId, token)
Notification → BFF → FE: deviceId and active status
```

The FCM token is tied to the authenticated user and tenant at the server. A token must be replaceable, deactivatable, and safe to register again after browser refresh or token rotation.

### Flow C — Shipment creation/status change becomes a push notification

```text
FE → BFF → Shipment Workflow: create/update/submit shipment
Shipment Workflow → Shipment DB: commit shipment mutation + outbox row atomically
Shipment outbox worker → RabbitMQ: publish versioned Shipment event
RabbitMQ → Notification consumer: deliver event
Notification → Notification DB: validate + deduplicate + create notification/attempt rows
Notification → FCM Admin SDK: send to active devices for authorized recipients
FCM → browser page/service worker: deliver notification payload
browser → FE: render foreground toast or background system popup
```

Shipment Workflow remains the only writer of shipment state. Notification must use event fields and an explicit recipient resolver/subscription; it must never query Shipment Workflow tables to discover recipients or shipment details.

Initial rules to implement:

| Source event | Notification rule | Required recipient/input |
|---|---|---|
| `ShipmentCreatedEvent` | Shipment created/received | Authorized recipient identity or Notification subscription |
| `ShipmentStatusChangedEvent` | Status changed | Authorized recipient identity plus old/new status |
| `ShipmentCancelledEvent` | Shipment cancelled | Authorized recipient identity |
| `ShipmentDeliveredEvent` | Shipment delivered | Authorized recipient identity |

For each event, use `EventId` + rule name + `UserId` as the deduplication key. If the event has no authorized recipient, record a classified processing failure/dead-letter outcome; never broadcast to all tenant users.

### Flow D — Notification API history and read state

```text
FE → BFF: GET /api/v1/notifications?page=1&pageSize=20
BFF → Notification: authenticated list query
Notification → Notification DB: filter by current TenantId + current UserId
Notification → BFF → FE: paged notification DTOs

FE → BFF: PUT /api/v1/notifications/{id}/read
BFF → Notification: authenticated mark-read command
Notification → Notification DB: verify ownership and update ReadAt
Notification → BFF → FE: updated read state
```

Unread count, list, mark-read, and mark-all-read must use the same identity rules as device registration. A notification ID from another tenant must return the repository-approved not-found/forbidden behavior without revealing whether it exists.

### Flow E — FCM delivery and browser popup behavior

```text
Notification DB → Notification delivery worker: pending attempt
delivery worker → IFcmPushProvider: send title/body/data
IFcmPushProvider → Firebase Admin SDK → FCM: authenticated server send
FCM → FE page: onMessage when tab is focused
FCM → firebase-messaging-sw.js: background message when tab is hidden/closed
FE/service worker → user: toast or system popup
user clicks popup → FE: validate internal data.actionUrl and open shipment/notification view
```

The backend owns durable notification state and delivery attempts. The frontend owns rendering. A foreground browser popup is not automatic: the page must handle `onMessage` and render its own toast. Background display is handled by the service worker. The `actionUrl` must be an allowlisted internal route, not an arbitrary URL supplied by event data.

### Flow F — Other services produce operational alerts

These services are not Notification dependencies through databases. If they need user-facing push notifications, they publish a versioned event with an explicit audience:

```text
GPS Tracking / Route Planning / OCR / Compliance
  → service-owned DB + transactional outbox
  → RabbitMQ event
  → Notification consumer
  → recipient resolution + Notification DB
  → FCM
  → FE popup/history
```

Examples:

- GPS publishes a geofence breach or signal-loss event; Notification formats an alert but does not calculate the breach.
- Route Planning publishes route assigned or route delay risk; Notification formats the user-facing message but does not own route state.
- OCR publishes extraction-needs-review; Notification alerts authorized staff but does not inspect OCR storage.
- Compliance publishes a violation or missing-document result with evidence reference; Notification alerts authorized staff but does not make compliance decisions.

Each future source event must define `EventId`, `ContractVersion`, `TenantId`, source entity external ID, event timestamp, event type, and an explicit audience or resolver key before a Notification consumer is added.

### Flow G — Failure, retry, and replay

```text
RabbitMQ → Notification consumer: duplicate/transient event
Notification → ProcessedNotificationEvent: check EventId/rule/recipient key
duplicate → acknowledge without creating another notification
new event → persist notification + delivery attempt
FCM transient error → bounded retry with backoff
FCM invalid-token error → deactivate device and record terminal failure
broker/database failure → do not acknowledge until the transaction can be retried safely
```

The delivery state machine must make the following states observable: `Pending`, `Sent`, `Retrying`, `Failed`, `InvalidToken`, and `Read` where applicable. Correlation ID, event ID, notification ID, and attempt ID may be logged; FCM tokens and credentials may not.

### Flow H — Local test path

```text
Fake FE token registration
→ test Notification API
→ publish real Shipment.Contracts event to in-memory/test RabbitMQ
→ Notification consumer
→ fake recipient resolver
→ Notification DB
→ FakeFcmPushProvider
→ assert popup payload, delivery attempt, idempotency, tenant isolation
```

The test path must not call Firebase, Cognito production endpoints, or paid providers. It must cover the same boundaries and payload shapes as the runtime path.

## Implementation Tasks

### Task 1: Create the .NET service foundation

**Files:**
- Create: `src/dotnet/Notification/Notification.csproj`
- Create: `src/dotnet/Notification/Program.cs`
- Create: `src/dotnet/Notification/appsettings.json`
- Create: `src/dotnet/Notification/appsettings.Development.json`
- Modify only if required: solution/project registration files
- Test: `tests/dotnet/Notification.Tests/Notification.Tests.csproj`, `tests/dotnet/Notification.Tests/ServiceBuildTests.cs`

**Steps:**

- [ ] Write a build smoke test that references the new project and asserts the service assembly can load.
- [ ] Run `dotnet test tests/dotnet/Notification.Tests/Notification.Tests.csproj`; expect failure because the project does not exist yet.
- [ ] Create the executable targeting `net10.0`, reference `Shared` and `Shipment.Contracts`, and register ASP.NET Core health checks.
- [ ] Register shared current-user services and MassTransit using the repository's `AddSharedServices` and `AddSharedMassTransit` conventions.
- [ ] Configure RabbitMQ and Notification-specific database settings without embedding credentials.
- [ ] Run `dotnet build src/dotnet/Notification/Notification.csproj` and the foundation test; both must pass.
- [ ] Commit only the foundation files with `feat(notification): scaffold dotnet notification service`.

### Task 2: Define the notification domain and persistence model

**Files:**
- Create: `src/dotnet/Notification/Domain/Entities/*.cs`
- Create: `src/dotnet/Notification/Domain/Enums/*.cs`
- Create: `src/dotnet/Notification/Infrastructure/Persistences/NotificationDbContext.cs`
- Create: `src/dotnet/Notification/Infrastructure/Persistences/Configurations/*.cs`
- Test: `tests/dotnet/Notification.Tests/Domain/NotificationDomainTests.cs`
- Test: `tests/dotnet/Notification.Tests/Persistence/NotificationDbContextTests.cs`

**Interfaces:**

```csharp
public sealed class Notification
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid UserId { get; init; }
    public string Type { get; init; } = string.Empty;
    public NotificationChannel Channel { get; init; }
    public NotificationPriority Priority { get; init; }
    public NotificationStatus Status { get; private set; }
    public Guid? ShipmentId { get; init; }
    public string? ShipmentNumber { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string? ActionUrl { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ReadAt { get; private set; }
}
```

```csharp
public sealed class NotificationDevice
{
    public Guid Id { get; init; }
    public Guid TenantId { get; init; }
    public Guid UserId { get; init; }
    public string FcmToken { get; private set; } = string.Empty;
    public DevicePlatform Platform { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset LastSeenAt { get; private set; }
}
```

**Steps:**

- [ ] Write failing tests for required fields, valid status transitions, token upsert behavior, and tenant ownership.
- [ ] Add entities for notifications, devices, delivery attempts, and processed events; add optional preferences/subscriptions only if the selected recipient strategy requires them.
- [ ] Configure composite indexes for `(TenantId, UserId, CreatedAt)`, `(TenantId, UserId, IsActive)`, and idempotency keys.
- [ ] Configure all cross-service IDs as scalar external references with no foreign keys.
- [ ] Add global tenant query filters only where they can safely use current-user context; event processing must also pass explicit tenant checks.
- [ ] Run focused domain and persistence tests; expect all to pass.
- [ ] Commit with `feat(notification): add tenant-scoped notification domain`.

### Task 3: Add authenticated device registration APIs

**Files:**
- Create: `Application/Commands/Devices/RegisterNotificationDeviceCommand.cs`
- Create: `Application/Commands/Devices/RemoveNotificationDeviceCommand.cs`
- Create: `Application/DTOs/Notifications/DeviceDtos.cs`
- Create: `GrpcServices/NotificationGrpcService.cs` or the approved HTTP adapter
- Test: `tests/dotnet/Notification.Tests/Api/DeviceRegistrationTests.cs`

**Contract:**

```text
POST /api/v1/notification-devices
Authorization: Bearer <token>
Body: { token, platform, appVersion? }
Result: { deviceId, platform, isActive }
```

```text
DELETE /api/v1/notification-devices/{deviceId}
Authorization: Bearer <token>
```

**Steps:**

- [ ] Write tests proving `TenantId` and `UserId` come from the authenticated current-user service, not the request body.
- [ ] Validate token length/non-empty, supported platform, request size, and authenticated user presence.
- [ ] Upsert by token or `(TenantId, UserId, token)`; reactivate an existing token and update `LastSeenAt`.
- [ ] Allow deletion/deactivation only for the current user or an explicitly authorized tenant administrator.
- [ ] Return DTOs, never EF entities or raw FCM tokens.
- [ ] Run API unit tests and `dotnet build`; expect all to pass.
- [ ] Commit with `feat(notification): register authenticated push devices`.

### Task 4: Implement the FCM provider boundary

**Files:**
- Create: `Application/Interfaces/IFcmPushProvider.cs`
- Create: `Infrastructure/Firebase/FirebaseOptions.cs`
- Create: `Infrastructure/Firebase/FirebaseAdminInitializer.cs`
- Create: `Infrastructure/Firebase/FirebasePushProvider.cs`
- Create: `Infrastructure/Firebase/FakeFcmPushProvider.cs`
- Test: `tests/dotnet/Notification.Tests/Firebase/FirebasePushProviderTests.cs`

**Interface:**

```csharp
public interface IFcmPushProvider
{
    Task<FcmSendResult> SendAsync(
        NotificationDevice device,
        FcmMessage message,
        CancellationToken cancellationToken);
}
```

`FcmMessage` must contain title/body plus non-sensitive data fields such as `notificationId`, `type`, `shipmentId`, and `actionUrl`. It must not contain access tokens, private customer data, or unrestricted database payloads.

**Steps:**

- [ ] Write tests asserting the fake provider captures a message and can return success, transient failure, and invalid-token failure deterministically.
- [ ] Initialize Firebase Admin only when configured; fail startup clearly in production if required credential configuration is absent.
- [ ] Read credentials from environment/secret manager; support service-account JSON or application default credentials without storing a credential file in the repository.
- [ ] Map Firebase response errors into `Success`, `TransientFailure`, and `InvalidToken` without leaking provider details to API callers.
- [ ] Register the real provider in production and fake provider in tests.
- [ ] Run provider tests without network access; expect all to pass.
- [ ] Commit with `feat(notification): add firebase push provider abstraction`.

### Task 5: Process Shipment events into notifications

**Files:**
- Create: `Infrastructure/Messaging/Consumers/ShipmentCreatedConsumer.cs`
- Create: `Infrastructure/Messaging/Consumers/ShipmentStatusChangedConsumer.cs`
- Create: `Infrastructure/Messaging/Consumers/ShipmentCancelledConsumer.cs`
- Create: `Infrastructure/Messaging/Consumers/ShipmentDeliveredConsumer.cs`
- Create: `Infrastructure/Messaging/NotificationEventProcessor.cs`
- Modify only if contract decision requires it: `src/dotnet/Contracts/Shipment.Contracts/Events/*.cs`
- Test: `tests/dotnet/Notification.Tests/Messaging/ShipmentNotificationConsumerTests.cs`

**Steps:**

- [ ] Write a failing test for each supported event and assert the expected notification type/title/body/action URL.
- [ ] Write a duplicate-delivery test using the same `EventId`; assert exactly one user-visible notification is created.
- [ ] Validate `ContractVersion`, `TenantId`, `EventId`, and required external IDs before processing.
- [ ] Resolve recipients only through the selected explicit recipient strategy; reject or dead-letter events with no authorized audience rather than broadcasting.
- [ ] In one database transaction, create the processed-event record and notification records using a unique idempotency key.
- [ ] Register each MassTransit consumer with stable queue names and the existing raw JSON interoperability convention.
- [ ] Run focused consumer tests with in-memory/fake bus and fake recipient resolver; expect all to pass.
- [ ] Commit with `feat(notification): consume shipment events into notifications`.

### Task 6: Add delivery attempts, retry, and invalid-token handling

**Files:**
- Create: `Infrastructure/BackgroundJobs/NotificationDeliveryWorker.cs`
- Create: `Application/Services/NotificationDeliveryService.cs`
- Create: `Infrastructure/Messaging/NotificationRetryPolicy.cs`
- Test: `tests/dotnet/Notification.Tests/Delivery/NotificationDeliveryServiceTests.cs`
- Test: `tests/dotnet/Notification.Tests/Delivery/NotificationRetryTests.cs`

**Steps:**

- [ ] Write tests for success, transient failure, permanent failure, invalid token, max-attempt exhaustion, and cancellation.
- [ ] Persist one delivery attempt per device/provider call with status, attempt count, provider message ID, and sanitized error category.
- [ ] Retry only transient failures with bounded exponential backoff; mark permanent failures terminal.
- [ ] Deactivate FCM tokens when Firebase reports an unregistered/invalid token.
- [ ] Ensure worker concurrency cannot send the same `(NotificationId, DeviceId)` delivery twice at the same attempt state.
- [ ] Add structured logs containing correlation/event/notification IDs but never FCM tokens or credentials.
- [ ] Run delivery tests and build; expect all to pass.
- [ ] Commit with `feat(notification): add reliable fcm delivery retries`.

### Task 7: Add notification history and read-state APIs

**Files:**
- Create: `Application/Queries/Notifications/ListNotificationsQuery.cs`
- Create: `Application/Queries/Notifications/GetUnreadNotificationCountQuery.cs`
- Create: `Application/Commands/Notifications/MarkNotificationReadCommand.cs`
- Create: `Application/Commands/Notifications/MarkAllNotificationsReadCommand.cs`
- Create: `GrpcServices/NotificationGrpcService.cs` or the approved HTTP adapter endpoints
- Test: `tests/dotnet/Notification.Tests/Api/NotificationQueryTests.cs`

**Contract:**

```text
GET /api/v1/notifications?page=1&pageSize=20
GET /api/v1/notifications/unread-count
PUT /api/v1/notifications/{id}/read
PUT /api/v1/notifications/read-all
```

**Steps:**

- [ ] Write tests proving users cannot list, count, read, or mutate another tenant's notifications.
- [ ] Add stable pagination ordered by `CreatedAt DESC, Id DESC`.
- [ ] Restrict normal users to their own notifications; define a separate permission for tenant-wide staff views if needed.
- [ ] Return stable DTOs with `notificationId`, `type`, `title`, `body`, `actionUrl`, `isRead`, and timestamps.
- [ ] Map errors to consistent validation/not-found/forbidden responses without stack traces.
- [ ] Run API tests and build; expect all to pass.
- [ ] Commit with `feat(notification): expose notification history and read state`.

### Task 8: Create and validate the Notification database migration

**Files:**
- Create: `src/dotnet/Notification/Infrastructure/Persistences/Migrations/*`
- Modify: `src/dotnet/Notification/appsettings.Development.json` only for a clearly named Notification database
- Test: `tests/dotnet/Notification.Tests/Persistence/NotificationMigrationTests.cs`

**Steps:**

- [ ] Inspect the migration directory and confirm the target database name is Notification-specific, not `aurora_shipment_workflow`.
- [ ] Generate one initial Notification migration from the final model; do not reuse or alter Shipment migrations.
- [ ] Apply it only to the confirmed local Notification database.
- [ ] Verify tables, indexes, unique idempotency constraints, and absence of cross-service foreign keys.
- [ ] Run migration compatibility tests and `dotnet ef migrations list`; record command output in the Notification phase file.
- [ ] Commit with `feat(notification): add notification database migration`.

### Task 9: Complete integration verification and frontend handoff

**Files:**
- Modify: `codex/tasks/notification/phase-01-project-foundation.md` through `phase-09-testing.md` with actual command evidence
- Modify: `codex/specs/notification.md` only for decisions made during implementation
- Create: `docs/superpowers/plans/2026-08-29-notification-fcm-push.md` updates to checked steps
- Frontend handoff artifact outside this backend scope: Firebase config, `firebase-messaging-sw.js`, permission/token registration, `onMessage`, and click navigation
- Test: `tests/dotnet/Notification.Tests/NotificationServiceIntegrationTests.cs`

**Steps:**

- [ ] Run `dotnet build src/dotnet/Notification/Notification.csproj`.
- [ ] Run `dotnet test tests/dotnet/Notification.Tests/Notification.Tests.csproj`.
- [ ] Run `git diff --check`.
- [ ] Run an end-to-end local flow: register device → publish a Shipment event → consume once → create notification → fake-send FCM → persist delivery attempt → mark read.
- [ ] Verify duplicate event replay does not create a duplicate notification.
- [ ] Verify invalid FCM token deactivates only that device.
- [ ] Verify tenant A cannot access tenant B's device or notification records.
- [ ] Verify no Firebase credentials, raw tokens, or connection strings are present in tracked files or logs.
- [ ] Update task work logs with exact commands and results; do not mark phases complete without passing criteria.
- [ ] Inspect `git diff` and stage explicit paths only; do not push.

## Frontend FCM Contract

The frontend team must implement the browser side separately:

```text
1. Initialize Firebase client SDK.
2. Register firebase-messaging-sw.js.
3. Ask notification permission after a user action.
4. Call getToken({ vapidKey, serviceWorkerRegistration }).
5. POST the token to Notification Service using the authenticated session.
6. Use onMessage for an in-app popup while the tab is focused.
7. Use the service worker for background notification display.
8. On click, navigate using data.actionUrl after validating it is an internal route.
```

The browser Firebase config and VAPID public key may be client-visible. The Firebase Admin service-account private key must remain server-side.

## Acceptance Criteria

- `.NET Notification` builds and starts with local PostgreSQL and RabbitMQ.
- Notification owns its own database and has no cross-service database access or foreign keys.
- Authenticated users can register/deactivate their own FCM devices.
- Supported Shipment events create tenant-scoped notifications for an explicit authorized audience.
- Foreground and background FCM payloads contain stable notification metadata and an internal action URL.
- Duplicate Shipment events do not duplicate user-visible notifications.
- Delivery attempts, bounded retries, permanent failures, and invalid tokens are persisted.
- Notification history, unread count, and read-state APIs enforce tenant and user isolation.
- Automated tests pass with a fake FCM provider and no paid Firebase credentials.
- Migration and runtime smoke validation pass against the Notification database only.

## Known Risks and Decisions

- Recipient resolution is the primary integration risk because current Shipment events do not consistently carry recipient identity. Resolve this before consumer implementation.
- FCM delivery is not a durable source of truth; Notification DB remains the durable history and delivery-attempt record.
- Browser notification permission can be denied and browsers can invalidate tokens; registration and deactivation must be repeatable.
- “Popup while the app is open” is a frontend `onMessage` responsibility; the backend only sends the FCM payload.
- The existing repository contains a separate Java notification implementation, but it is outside this plan and must not be used as the `.NET Notification` implementation target.

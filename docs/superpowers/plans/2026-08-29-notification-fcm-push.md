# Notification FCM Push — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver an authenticated, tenant-safe Notification flow in which events from Shipment Workflow, GPS Tracking, Document OCR, and Regulatory Compliance become durable notifications and Firebase Cloud Messaging (FCM) popups.

**Architecture:** Frontend never calls Notification, RabbitMQ, Firebase Admin, or another service database directly. The frontend calls the authenticated YARP/BFF surface; BFF enforces the capability permission and forwards identity metadata over gRPC. On Notification calls only, Staff.Bff also authenticates itself with `x-service-id: staff-bff` and `x-service-api-key`, sourced from `Grpc:Notification:ServiceApiKey`. Notification validates both service headers before the shared `AuthInterceptor` may populate current-user state. Producer services persist domain state and an outbox row atomically, publish versioned events to RabbitMQ, and Notification consumes them idempotently, resolves an explicit audience, persists delivery state, and sends FCM. Notification owns device tokens and notification history only.

**Tech Stack:** .NET 10, ASP.NET Core, gRPC/Protobuf, ASP.NET Core BFF controllers, EF Core/PostgreSQL, MassTransit/RabbitMQ, Redis permission cache, Firebase Admin SDK for .NET, Firebase Web SDK in the frontend, xUnit, deterministic fake FCM provider.

**Spec:** `codex/requirement.md`, `codex/specs/logistics-architecture.md`, `codex/specs/notification.md`, `docs/technical/iam/AUTHORIZATION_MODEL.md`, `docs/documents/events/*.md`, and the existing service contracts under `src/dotnet/Contracts`.

## Global Constraints

- Authentication is mandatory for every client API; do not use DevelopmentIdentity as an HTTP authentication bypass.
- BFF endpoints must enforce `[Authorize]` and the direct capability permission from `AUTHORIZATION_MODEL.md`; base role alone is never authority.
- Forwarded `x-user-id`/`x-tenant-id` metadata is identity context, not service authentication. Staff.Bff must read `Grpc:Notification:ServiceApiKey` (environment variable `Grpc__Notification__ServiceApiKey`) and a Notification-specific client interceptor must add `x-service-id: staff-bff` plus `x-service-api-key` to Notification calls only.
- Notification must read `ServiceAuth:AllowedServiceId` (checked-in value `staff-bff`) and `ServiceAuth:ApiKey` (environment variable `ServiceAuth__ApiKey`). Its Notification-specific service-auth interceptor must execute before `AuthInterceptor`, compare the configured and supplied credential values with `CryptographicOperations.FixedTimeEquals`, reject missing or wrong values with gRPC `Unauthenticated`, and never log the API key or metadata collection.
- The application-level credential is necessary but not sufficient transport security. Production must use TLS for the gRPC channel and restrict Notification to the private service network.
- TenantId and UserId come from authenticated current-user context; never trust client-supplied tenant or recipient ownership.
- Notification must not read or write Shipment, GPS, OCR, Regulatory, or IAM databases.
- Cross-service communication uses gRPC or versioned MassTransit/RabbitMQ events; event publication uses the producer transactional outbox.
- Consumer processing is at-least-once, duplicate-safe, retry-aware, and fail-closed when no audience is known.
- FCM Admin service-account credentials and client FCM registration tokens must never be committed or logged.
- The Admin SDK service-account JSON is a server secret; the frontend uses Firebase Web SDK configuration and a registration token, never the Admin JSON.
- No real Firebase credentials are required for automated tests; use `FakeFcmPushProvider`.
- Do not modify non-Notification service behavior unless an explicit shared contract incompatibility blocks this plan.
- Do not commit this plan or code automatically; each task has an explicit commit step for the human/operator to approve.

## Current Baseline and Decisions

- Branch: `codex/api-management-fcm`, based on `origin/main`.
- Notification FCM code already exists under `src/dotnet/Notification`; its build passes and its current tests pass 5/5.
- Notification currently has FCM device/subscription HTTP endpoints and nine consumers, but the architecture documents describe BFF/gRPC and thirteen owned-service events.
- Staff.Bff already has a legacy `NotificationsController` and Notification gRPC client registration, but the controller has no capability attributes, the proto still exposes Email/InApp preference methods, and Notification has no gRPC server implementation.
- `PermissionConstants` has no Notification capability. Phase 2 adds the single self-service capability `PermissionConstants.Notification.Access = "notifications:access"`; every Notification BFF action requires it. Add it to the permission catalog and onboarding defaults, which remain templates rather than runtime role grants.
- Current recipient resolver only maps `ShipmentId -> NotificationSubscription -> UserId -> active FCM devices`. It intentionally does not broadcast when ShipmentId is absent.
- Current `appsettings.Development.json` must contain RabbitMQ local credentials matching the producers, but must keep Firebase disabled and credentials empty.
- Checked-in Staff.Bff and Notification settings keep both service API-key values empty. Local and deployed runtimes inject the same secret through `Grpc__Notification__ServiceApiKey` and `ServiceAuth__ApiKey`; startup validation fails when either required value is absent.
- The authoritative security rule is JWT/session authentication plus direct permissions. No unauthenticated HTTP DevelopmentIdentity fallback is allowed.
- Existing technical documents contain legacy Notification names and capabilities. Code/Protobuf/contracts and `AUTHORIZATION_MODEL.md` win; stale docs are updated in the documentation task.

## End-to-End Acceptance Flow

```text
FE login/session
  -> YARP
  -> Staff.Bff [Authorize + RequirePermission]
  -> Notification gRPC with staff-bff service credential + identity metadata
  -> register FCM token / subscribe shipment

Shipment/GPS/OCR/Compliance mutation
  -> service DB + transactional outbox
  -> RabbitMQ
  -> Notification consumer
  -> audience resolution + idempotent Notification DB write
  -> Firebase Admin SDK
  -> browser foreground toast or service-worker popup
  -> notification history/read state through BFF
```

---

## Task 1 — Phase 0: Reconcile Contracts, Ownership, and Security

**Goal:** Establish one implementable source of truth before changing interfaces.

**Files:**

- Read: `codex/requirement.md`
- Read: `codex/specs/logistics-architecture.md`
- Read: `codex/specs/notification.md`
- Read: `docs/technical/iam/AUTHORIZATION_MODEL.md`
- Read: `docs/documents/events/notification-events.md`
- Read: `docs/bff-api/API-MATRIX.md`
- Modify: this plan if an approved contract decision changes

**Tasks:**

- [x] Record the final public boundary as `FE -> YARP -> Staff.Bff -> Notification gRPC`.
- [x] Record the final internal boundary as `producer outbox -> RabbitMQ -> Notification consumer`.
- [x] Confirm the required permission names with `Shared.Constants.PermissionConstants`; add exactly `PermissionConstants.Notification.Access = "notifications:access"`, not a role check.
- [x] Fix the BFF-to-Notification trust mechanism as an application-level shared credential; do not leave service authentication as an implementation option.
- [x] Mark stale documentation claims (legacy Email/InApp gRPC model and missing/current event lists) for update after code is complete.
- [x] Define the no-audience policy: acknowledge safely, persist a classified no-recipient outcome, and never broadcast to a whole tenant.

**Audited decisions and implementation baseline (2026-08-30):**

- Public owner/transport/auth gate: the browser uses `FE -> YARP -> Staff.Bff -> Notification gRPC`; `StaffControllerBase` supplies `[Authorize]`, and every Notification action additionally uses `[RequirePermission(PermissionConstants.Notification.Access)]`.
- Internal owner/transport: all thirteen required producer contracts exist and are written through their producer transactional outboxes before MassTransit/RabbitMQ publication; Notification currently registers nine of the thirteen consumers.
- gRPC trust boundary: `ClientMetadataInterceptor` continues to forward user/tenant/version/role. A new `NotificationServiceCredentialInterceptor` on the Staff.Bff client reads `Grpc:Notification:ServiceApiKey` and adds `x-service-id: staff-bff` and `x-service-api-key` only to the Notification client. A new `NotificationServiceAuthInterceptor` on Notification reads `ServiceAuth:AllowedServiceId` and `ServiceAuth:ApiKey`, validates both before `AuthInterceptor` runs using fixed-time comparison, and returns `Unauthenticated` without populating current-user state when validation fails. After service authentication, Notification owns missing/malformed identity rejection and tenant/user resource scope. Production additionally requires TLS and a private service network.
- Audience rule: current contracts carry no explicit `UserId`. Resolve only shipment subscribers when a trusted `ShipmentId`/`ExternalShipmentId` exists. If the shipment ID is absent or no subscription resolves, atomically persist a duplicate-safe `NoRecipient` processing outcome, log only safe IDs/classification, return successfully so RabbitMQ may acknowledge, create no notification/delivery attempt, and never fall back to tenant-wide broadcast.
- Test boundaries: Notification tests live at `tests/dotnet/Notification.Tests` and include domain, persistence, processor, delivery-outcome, and gRPC service-auth coverage; a dedicated Staff.Bff test project is still pending.
- Frontend boundary: no frontend workspace exists inside `aurora-server`; the Next.js/Vitest frontend is the sibling repository `/home/kaito/project/aurora-client`, which currently has no Firebase dependency. Frontend work and commits happen there, separately.
- Deferred stale-doc updates: `codex/specs/notification.md`, `docs/documents/events/notification-events.md`, `docs/technical/notification-spec.md`, `docs/technical/notification/{OVERVIEW,DETAILS,INTERVIEW_QA}.md`, and the Notification rows in `docs/bff-api/API-MATRIX.md` describe legacy Email/InApp/preferences, consolidated consumers, unsupported test counts, or READY gRPC routes that do not match this branch.

**Exit criteria:** every later phase has an owner, transport, auth gate, audience rule, and test boundary.

---

## Task 2 — Phase 1: Notification Domain, Persistence, and Migration

**Goal:** Persist devices, subscriptions, notifications, attempts, and idempotency records with tenant-safe constraints.

**Files:**

- Modify: `src/dotnet/Notification/Domain/Entities/Notification.cs`
- Modify: `src/dotnet/Notification/Domain/Entities/NotificationDevice.cs`
- Modify: `src/dotnet/Notification/Domain/Entities/NotificationSubscription.cs`
- Modify: `src/dotnet/Notification/Domain/Entities/NotificationDeliveryAttempt.cs`
- Modify: `src/dotnet/Notification/Domain/Entities/ProcessedNotificationEvent.cs`
- Modify: `src/dotnet/Notification/Domain/Enums/NotificationEnums.cs`
- Modify: `src/dotnet/Notification/Infrastructure/Persistences/NotificationDbContext.cs`
- Verify: the eventual additive migration after delivery schema decisions are stable (owned by Task 5)
- Test: `tests/dotnet/Notification.Tests/Domain/NotificationDomainTests.cs`
- Test: `tests/dotnet/Notification.Tests/Persistence/NotificationPersistenceTests.cs`

**Required model rules:**

- `Notification`: `TenantId`, `UserId`, stable type, title/body, optional `ShipmentId`, safe internal `ActionUrl`, status, priority, timestamps.
- `NotificationDevice`: active flag, platform, last-seen timestamp, and one active owner per FCM token. Registration by a different authenticated tenant/user must safely transfer/deactivate the previous ownership or reject it; a token must never remain active for two users. Token values are never returned in logs.
- `NotificationSubscription`: unique `(TenantId, UserId, ShipmentId)`; user and tenant come from current identity.
- `ProcessedNotificationEvent`: unique `(TenantId, EventId, Rule)` receipt with classified outcome (`AudienceResolved` or `NoRecipient`) and recipient count; event IDs come from trusted contracts only. Persist the receipt and all per-user notification projections atomically so redelivery cannot duplicate or lose a partial audience.
- `NotificationDeliveryAttempt`: notification/device/provider result, bounded error, retry count, next-attempt time, and terminal state.

**Tasks:**

- [x] Write and run coverage for cross-tenant queries, duplicate/same-owner device registration, cross-user token reuse, duplicate subscription, bounded title/body, invalid FCM token input, and both processed-event outcomes.
- [ ] Capture a separate red-before-green run for every invariant; implementation was resumed after the available subagent quota was exhausted.
- [x] Implement the domain/configuration changes needed by the invariant tests.
- [x] Verify the EF model is migration-ready and generate the final migration after `NextAttemptAt` and delivery-attempt state stabilized; the migration was reviewed but not applied to the legacy live database.
- [ ] Run the persistence tests against the intended local Notification database after its migration history is reconciled; the current database is a legacy target and was deliberately not modified.
- [ ] Commit: `feat(notification): harden FCM persistence and audience constraints`.

---

## Task 3 — Phase 2: Authenticated Notification gRPC and BFF Surface

**Goal:** Make every client operation follow the Aurora authorization pipeline.

**Files:**

- Modify: `protos/notification.proto`
- Create/modify: `src/dotnet/Notification/GrpcServices/NotificationGrpcService.cs`
- Modify: `src/dotnet/Notification/Program.cs`
- Modify: `src/dotnet/Notification/Notification.csproj`
- Modify: `src/dotnet/Notification/Infrastructure/Security/NotificationCurrentUserMiddleware.cs`
- Create: `src/dotnet/Notification/Infrastructure/Security/NotificationServiceAuthOptions.cs`
- Create: `src/dotnet/Notification/Infrastructure/Security/NotificationServiceAuthInterceptor.cs`
- Modify: `src/dotnet/Notification/appsettings.json`
- Create/modify: `src/dotnet/BFF/Staff.Bff/Controllers/NotificationsController.cs`
- Modify: `src/dotnet/BFF/Staff.Bff/Program.cs`
- Modify: `src/dotnet/BFF/Staff.Bff/appsettings.json`
- Create: `src/dotnet/BFF/BuildingBlocks.BFF/Interceptors/NotificationServiceCredentialOptions.cs`
- Create: `src/dotnet/BFF/BuildingBlocks.BFF/Interceptors/NotificationServiceCredentialInterceptor.cs`
- Modify: `src/dotnet/BFF/BuildingBlocks.BFF/Extensions/GrpcClientExtensions.cs`
- Modify: `src/dotnet/shared/Security/JwtClaims.cs` to add `GrpcMetadataKeys.ServiceApiKey = "x-service-api-key"`
- Modify: `src/dotnet/shared/Constants/PermissionConstants.cs`
- Test: `tests/dotnet/Notification.Tests/Grpc/NotificationGrpcServiceTests.cs`
- Test: `tests/dotnet/Notification.Tests/Grpc/NotificationGrpcAuthenticationTests.cs`
- Create: `src/dotnet/BFF/Staff.Bff/Tests/Staff.Bff.Tests.csproj`
- Create: `src/dotnet/BFF/Staff.Bff/Tests/NotificationsControllerAuthorizationTests.cs`
- Create: `src/dotnet/BFF/Staff.Bff/Tests/Interceptors/NotificationServiceCredentialInterceptorTests.cs`

**Contract:**

```protobuf
rpc RegisterDevice(RegisterDeviceRequest) returns (DeviceResponse);
rpc RemoveDevice(RemoveDeviceRequest) returns (Empty);
rpc SubscribeShipment(SubscribeShipmentRequest) returns (Empty);
rpc ListNotifications(ListNotificationsRequest) returns (ListNotificationsResponse);
rpc GetUnreadCount(GetUnreadCountRequest) returns (UnreadCountResponse);
rpc MarkNotificationRead(MarkNotificationReadRequest) returns (Empty);
rpc MarkAllNotificationsRead(MarkAllNotificationsReadRequest) returns (CountResponse);
```

Every request excludes `TenantId` and `UserId`. The service obtains them from authenticated context. Every BFF action has `[Authorize]` plus `[RequirePermission(PermissionConstants.Notification.Access)]`, where `PermissionConstants.Notification.Access` is exactly `"notifications:access"`; no role grants authority.

**Service authentication contract:**

- Staff.Bff binds `Grpc:Notification:Url` and required secret `Grpc:Notification:ServiceApiKey`; the corresponding environment variable is `Grpc__Notification__ServiceApiKey`. `NotificationServiceCredentialInterceptor` adds `x-service-id: staff-bff` and `x-service-api-key: <configured value>` only to the Notification gRPC client, in addition to the existing identity metadata interceptor.
- Notification binds `ServiceAuth:AllowedServiceId` to `staff-bff` and required secret `ServiceAuth:ApiKey`; the secret environment variable is `ServiceAuth__ApiKey`. Checked-in appsettings contain no key value. Both applications validate required service-auth configuration at startup.
- `NotificationServiceAuthInterceptor` is registered first in `AddGrpc`, before `AuthInterceptor`. It rejects a missing/wrong key or wrong service ID with `StatusCode.Unauthenticated` before current-user population or handler execution. For both the service ID and API key, its comparison helper converts the supplied and configured values to fixed-size SHA-256 digests and uses `CryptographicOperations.FixedTimeEquals`; logs may contain only a safe failure category and never the key/header collection.
- After service authentication succeeds, the existing identity interceptor populates current-user context. The Notification handler rejects missing/malformed user or tenant identity with `Unauthenticated` and enforces tenant/user resource scope. `DevelopmentIdentity` must remain disabled for this gRPC path.
- Production config sets an `https://` Notification URL, enforces TLS at the workload/ingress boundary, and exposes Notification only on the private service network. The shared API key does not replace either control.

Checked-in configuration shape:

Staff.Bff `appsettings.json`:

```json
"Grpc": {
  "Notification": {
    "Url": "http://localhost:6001",
    "ServiceApiKey": ""
  }
}
```

Notification `appsettings.json`:

```json
"ServiceAuth": {
  "AllowedServiceId": "staff-bff",
  "ApiKey": ""
}
```

**Tasks:**

- [x] Restore Staff.Bff dependencies and build it with the restored assets.
- [ ] Add the dedicated BFF controller/interceptor test project covering 401/403, Notification-only headers, fail-closed missing key, and no secret logging.
- [ ] Complete the full pipeline matrix for missing/wrong credentials, valid credential with/without identity, forged identity, malformed identity, and wrong-user NotFound behavior; current Notification auth coverage includes the missing/wrong/id and valid-credential handler cases.
- [x] Bind and validate both service-auth option objects, register the Notification-only client interceptor after the existing identity metadata interceptor, and register `NotificationServiceAuthInterceptor` before `AuthInterceptor` on the Notification server.
- [x] Add the Protobuf methods and generated server implementation.
- [x] Map BFF REST routes to gRPC using the existing `ClientMetadataInterceptor` and `RequirePermissionAttribute` patterns.
- [x] Remove Notification HTTP minimal routes as the browser-facing boundary; the BFF is the authenticated browser surface.
- [x] Run focused Notification auth tests and build the BFF.
- [ ] Commit: `feat(notification): expose authenticated notification contract`.

---

## Task 4 — Phase 3: Audience Resolution and All Owned-Service Consumers

**Goal:** Ensure every supported producer event maps to an explicit, authorized recipient without database coupling.

**Files:**

- Modify/create: `src/dotnet/Notification/Application/Interfaces/IRecipientResolver.cs`
- Modify: `src/dotnet/Notification/Infrastructure/Messaging/SubscriptionRecipientResolver.cs`
- Modify: `src/dotnet/Notification/Infrastructure/Messaging/NotificationEventProcessor.cs`
- Create: `src/dotnet/Notification/Infrastructure/Messaging/Consumers/ShipmentSubmittedConsumer.cs`
- Create: `src/dotnet/Notification/Infrastructure/Messaging/Consumers/ShipmentPickedUpConsumer.cs`
- Create: `src/dotnet/Notification/Infrastructure/Messaging/Consumers/ShipmentCompletedConsumer.cs`
- Create: `src/dotnet/Notification/Infrastructure/Messaging/Consumers/DocumentAttachedConsumer.cs`
- Modify: `src/dotnet/Notification/Infrastructure/Messaging/Consumers/*.cs`
- Modify: `src/dotnet/Notification/Program.cs`
- Test: `tests/dotnet/Notification.Tests/Messaging/NotificationEventProcessorTests.cs`
- Test: `tests/dotnet/Notification.Tests/Messaging/OwnedServiceConsumerTests.cs`

**Supported events:**

- Shipment: created, submitted, status changed, cancelled, picked up, delivered, completed, document attached.
- GPS: monitoring alert only; never position updates.
- OCR: completed and failed.
- Regulatory: evaluation completed and failed.

All thirteen producer contracts and outbox publication paths already exist. Notification currently lacks only the four Shipment consumers named in this phase. None of the thirteen contracts carries an explicit recipient `UserId`; shipment subscription resolution is therefore the only approved current audience source.

**Tasks:**

- [ ] Add dedicated failing consumer contract tests for every event metadata field; current processor tests cover duplicate/no-recipient/multi-recipient delivery behavior.
- [x] Cover duplicate event delivery and no-recipient behavior in processor tests.
- [x] Implement the four missing consumers and register all thirteen planned consumers in MassTransit.
- [x] Keep raw OCR `NormalizedJson`, credentials, and sensitive payloads out of notification content/logs; event text is bounded before persistence.
- [x] Ensure the processor creates one notification per authorized user/device policy and uses a stable rule name per event type.
- [x] Resolve the full audience first, then atomically persist the event receipt plus all per-user notification projections. For absent `ShipmentId` or zero subscriptions, persist `NoRecipient`, acknowledge safely, and never broadcast.
- [x] Run the available focused processor tests with real contract-compatible data.
- [ ] Commit: `feat(notification): consume complete owned-service event set`.

---

## Task 5 — Phase 4: FCM Provider, Token Lifecycle, and Delivery Retry

**Goal:** Reliably send to active devices and make provider failures observable and recoverable.

**Files:**

- Modify: `src/dotnet/Notification/Infrastructure/Firebase/FirebaseOptions.cs`
- Modify: `src/dotnet/Notification/Infrastructure/Firebase/FirebasePushProvider.cs`
- Modify: `src/dotnet/Notification/Infrastructure/Firebase/FirebaseAdminInitializer.cs`
- Modify: `src/dotnet/Notification/Infrastructure/Firebase/FakeFcmPushProvider.cs`
- Modify: `src/dotnet/Notification/Infrastructure/BackgroundJobs/NotificationDeliveryWorker.cs`
- Modify: `src/dotnet/Notification/Infrastructure/Messaging/NotificationEventProcessor.cs`
- Create: `src/dotnet/Notification/Infrastructure/Persistences/Migrations/<timestamp>_NotificationFcmAudience.cs` after the final delivery schema is stable
- Test: `tests/dotnet/Notification.Tests/FirebaseProviderTests.cs`
- Test: `tests/dotnet/Notification.Tests/Delivery/NotificationDeliveryTests.cs`

**FCM payload contract:**

```json
{
  "notification": {
    "title": "Shipment delivered",
    "body": "Shipment SHP-001 was delivered."
  },
  "data": {
    "notificationId": "uuid",
    "type": "SHIPMENT_DELIVERED",
    "shipmentId": "uuid",
    "actionUrl": "/shipments/uuid"
  }
}
```

`actionUrl` must be generated from an allowlisted internal route. Event data must not supply an arbitrary external URL.

**Tasks:**

- [ ] Add separate worker/provider integration tests for permanent failure and bounded worker retries; processor tests cover sent, invalid-token, transient retry, deactivation, and no-resend-after-success.
- [x] Implement fake-provider coverage before finalizing provider behavior.
- [x] Map Firebase `Unregistered` to device deactivation, invalid payloads to permanent failure, transient provider responses to bounded retry, and persist every attempt.
- [x] Ensure a provider failure leaves durable Notification/attempt state available for retry before RabbitMQ acknowledgement.
- [x] Add structured event/notification/attempt delivery logs; tokens and credential content are excluded. Correlation ID remains supplied by the hosting/logging pipeline.
- [ ] Commit: `feat(notification): complete FCM delivery lifecycle`.

---

## Task 6 — Phase 5: Firebase Secret and Local Runtime Configuration

**Goal:** Make local Firebase testing possible without placing a secret in source control.

**Files:**

- Modify: `src/dotnet/Notification/appsettings.json`
- Modify: `src/dotnet/Notification/appsettings.Development.json`
- Modify: `.gitignore`
- Create: `.github/workflows/secret-scan.yml`
- Do not commit: Firebase Admin service-account JSON
- Local ignored path: `secrets/firebase/aurora-notification-admin.json`

**Safe configuration:**

Keep checked-in config like this:

```json
"Firebase": {
  "Enabled": false,
  "ProjectId": "",
  "ClientEmail": "",
  "PrivateKey": "",
  "CredentialsPath": ""
}
```

For a local smoke test, place the key at the generic ignored path and provide it only at runtime:

```bash
export Firebase__Enabled=true
export Firebase__CredentialsPath="$(pwd)/secrets/firebase/aurora-notification-admin.json"
```

Do not paste the JSON into `appsettings*.json`, source code, `codex`, or a commit. Do not use the Firebase Web SDK config as the Admin SDK credential. Production should mount the JSON from a secret manager and set only `Firebase__CredentialsPath` (or inject the three inline Admin fields through the deployment secret store).

Use this same tracked-files scan locally and in `.github/workflows/secret-scan.yml`; no output is the passing result:

```bash
if git grep -nEI -- \
  '"private_key"[[:space:]]*:[[:space:]]*"-----BEGIN PRIVATE KEY-----|"client_email"[[:space:]]*:[[:space:]]*"[^"]+@[^"]+\.iam\.gserviceaccount\.com"' \
  -- .; then
  echo "Tracked credential material detected."
  exit 1
fi
```

**Tasks:**

- [x] Add `/secrets/firebase/*.json` to `.gitignore`; `git check-ignore -v secrets/firebase/aurora-notification-admin.json` names the rule.
- [x] Run `git ls-files --error-unmatch secrets/firebase/aurora-notification-admin.json`; it fails non-zero because the credential is not tracked.
- [x] Add `.github/workflows/secret-scan.yml` on pull requests and pushes and run the tracked-files scan with no matches.
- [x] Add startup validation for enabled Firebase/service-auth, credential path presence, and no secret logging.
- [x] Add `/ready` Firebase configuration readiness reporting without exposing credential content.
- [x] Run disabled Firebase and missing-credentials tests; real FCM provider testing remains dependent on a valid runtime credential/token.
- [ ] Commit only config validation and ignore-rule changes: `chore(notification): secure Firebase runtime configuration`.

---

## Task 7 — Phase 6: BFF and Frontend Popup Contract

**Goal:** Connect authenticated browser behavior to the backend payload and make both foreground and background notifications visible.

**Backend files:**

- Modify/create: `src/dotnet/BFF/Staff.Bff/Controllers/NotificationsController.cs`
- Modify: `docs/technical/frontend/API_CATALOG.md`
- Modify: `docs/technical/frontend/FE_INTEGRATION_GUIDE.md`
- Modify: `docs/documents/events/notification-events.md`

**Frontend deliverables:**

Frontend repository: `/home/kaito/project/aurora-client` (separate sibling repository; Next.js with Vitest, no Firebase dependency at audit time). Do not place frontend implementation or commits in `aurora-server`.

- Add Firebase Web SDK initialization using public web config, not Admin JSON.
- Request browser notification permission after authenticated app bootstrap.
- Register/update token through the authenticated BFF endpoint.
- Handle token refresh by upserting the device again; deactivate on logout where supported.
- Handle foreground `onMessage` and render a toast/popup.
- Add `firebase-messaging-sw.js` for background display and click handling.
- Validate `data.actionUrl` against internal allowed routes before navigation.
- Fetch notification history/unread count through BFF and mark read through BFF.

**Tasks:**

- [x] Define exact JSON request/response examples for device registration, subscription, list, unread count, and mark-read in the frontend API catalog and local runbook.
- [x] Verify all BFF routes have `[Authorize]` and the correct direct permission; role labels in docs do not replace capability checks.
- [ ] Add frontend tests for foreground message, background message, permission denied, token refresh, invalid action URL, and click navigation.
- [ ] Add API contract tests that prove frontend routes never expose Notification directly.
- [ ] Commit backend/docs separately from frontend if the frontend lives in another repository.

---

## Task 8 — Phase 7: Real Integration Verification

**Goal:** Prove the complete path with local infrastructure and a real FCM smoke test.

**Files/commands:**

- Read: `docker-compose.dev.yml`
- Verify: all service `appsettings.Development.json` RabbitMQ/DB settings
- Verify: `src/dotnet/Notification/Program.cs`
- Verify: all producer outbox processors and Notification consumer registrations

**Automated verification:**

- [x] `dotnet build src/dotnet/Notification/Notification.csproj --no-restore`
- [x] `dotnet test tests/dotnet/Notification.Tests/Notification.Tests.csproj --no-restore`
- [x] `dotnet restore src/dotnet/BFF/Staff.Bff/Staff.Bff.csproj` before the first BFF build in an environment with package access
- [x] `dotnet build src/dotnet/BFF/Staff.Bff/Staff.Bff.csproj --no-restore`
- [ ] `dotnet test src/dotnet/BFF/Staff.Bff/Tests/Staff.Bff.Tests.csproj --no-restore`
- [ ] `dotnet test src/dotnet/ShipmentWorkflow/Tests/ShipmentWorkflow.Tests.csproj --no-restore`
- [ ] `dotnet test src/dotnet/GpsTracking/Tests/GpsTracking.Tests.csproj --no-restore`
- [ ] `dotnet test src/dotnet/DocumentOcr/Tests/DocumentOcr.Tests.csproj --no-restore`
- [ ] `dotnet test src/dotnet/RegulatoryCompliance/Tests/RegulatoryCompliance.Tests.csproj --no-restore`
- [x] Run `git diff --check` and inspect staged/uncommitted diffs; no commit was created because the user explicitly asked not to commit.

**Runtime verification:**

- [ ] Start PostgreSQL databases, Redis, RabbitMQ, Notification, and the producer services with matching local credentials.
- [ ] Apply only the Notification migration to the confirmed Notification database.
- [ ] Authenticate as a real/development test user through the approved auth mechanism; do not bypass auth in HTTP.
- [ ] Register an actual browser FCM token through BFF and subscribe that user to a known shipment.
- [ ] Trigger a Shipment status event and verify: producer DB/outbox row, RabbitMQ delivery, Notification DB row, FCM delivery attempt, and browser popup.
- [ ] Repeat the same event and verify no duplicate notification.
- [ ] Trigger OCR/GPS/Compliance events and verify each registered consumer and audience rule.
- [ ] Revoke/invalid-token test: verify only the bad device is deactivated.
- [ ] Record any unavailable infrastructure honestly; do not mark runtime proof complete from a build-only result.

**Exit criteria:** authenticated BFF flow, all required consumers, durable notification history, duplicate safety, FCM delivery, and frontend popup are proven or the exact external blocker is recorded.

**Current blocker record:** backend verification is green, but frontend implementation is in the sibling
`/home/kaito/project/aurora-client` repository and was not modified from this server workspace. The
running Notification PostgreSQL database is a legacy schema with incompatible migration history, and
the sandbox denies Kestrel/RabbitMQ local network binding. Therefore real end-to-end browser popup
delivery remains pending and is not marked complete.

---

## Task 9 — Documentation and Plan Completion

- [ ] Update `codex/plan.md` only with commands and evidence from this branch.
- [ ] Update `codex/specs/notification.md` to describe FCM devices, subscriptions, BFF/gRPC boundary, and the actual consumer list.
- [ ] Update `docs/technical/notification-spec.md`, `docs/technical/notification/OVERVIEW.md`, and `DETAILS.md` to remove legacy claims that are not implemented.
- [ ] Update `docs/technical/notification/INTERVIEW_QA.md` to remove legacy Email/InApp/preference and realtime-publication claims that are not implemented.
- [ ] Update `docs/bff-api/API-MATRIX.md` only after the BFF controllers and permissions actually exist.
- [ ] Add a service integration matrix row for every real producer event and consumer.
- [ ] Run the final verification commands again after documentation changes.
- [ ] Do not call the feature complete while any phase exit criterion, auth gate, recipient rule, migration check, or runtime proof is missing.

## Recommended Execution Order

```text
Phase 0 contract/security reconciliation
  → Phase 1 persistence
  → Phase 2 authenticated gRPC + BFF
  → Phase 3 audience + all consumers
  → Phase 4 FCM delivery
  → Phase 5 secret/runtime configuration
  → Phase 6 frontend popup
  → Phase 7 end-to-end proof
```

The next coding phase is Phase 1 only. Do not start frontend or real Firebase smoke testing before the authenticated BFF boundary and recipient policy are implemented.

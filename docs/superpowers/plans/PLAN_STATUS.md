# Superpowers Implementation Plans — Status & Synchronization

> **Status:** Canonical Plan Synchronization Tracker  
> **Last Synchronized:** 2026-09-04  
> **Audited Plans:**
> 1. `2026-08-29-notification-fcm-push.md` — Notification FCM Push Backend Implementation Plan
> 2. `2026-08-30-frontend-fcm-popup.md` — Frontend FCM Popup Implementation Plan

---

## 1. Plan Summaries & Traceability

### Plan 1: Notification FCM Push (`2026-08-29-notification-fcm-push.md`)

- **Goal:** Deliver an authenticated, tenant-isolated notification flow where business events across Shipment Workflow, GPS Tracking, Document OCR, and Regulatory Compliance become durable notifications and Firebase Cloud Messaging (FCM) web push deliveries.
- **Architecture Decisions:**
  - **Client Boundary:** Browser/Frontend never talks to Notification service, RabbitMQ, or Firebase Admin directly. Frontend calls `Staff.Bff` via HTTP cookie session.
  - **Authorization:** `Staff.Bff` enforces `[Authorize]` and `[RequirePermission(PermissionConstants.Notification.Access)]` (`notifications:access`).
  - **Service-to-Service Trust:** `Staff.Bff` authenticates to `Notification` gRPC using `x-service-id: staff-bff` and `x-service-api-key` (configured via `Grpc:Notification:ServiceApiKey` and `ServiceAuth:ApiKey`), verified with fixed-time SHA-256 comparison before `AuthInterceptor` runs.
  - **Producer Outbox & Messaging:** Domain events are written to producer transactional outboxes and published to RabbitMQ via MassTransit.
  - **Audience Resolution:** Resolves explicitly via `ShipmentSubscription` (`(TenantId, UserId, ShipmentId)`). When no recipient is resolved, it persists a duplicate-safe `NoRecipient` outcome and acknowledges the message without tenant broadcast.
  - **Secrets:** Firebase Admin service-account credentials remain on the server; client uses public Web SDK credentials.
- **Implementation Status:** `IMPLEMENTED` (Backend C# / gRPC / BFF / MassTransit Consumers).
  - Controller: [NotificationsController.cs](file:///d:/IT/CD/aurora-server/src/dotnet/BFF/Staff.Bff/Controllers/NotificationsController.cs)
  - Proto: [notification.proto](file:///d:/IT/CD/aurora-server/protos/notification.proto)
  - Interceptors: `NotificationServiceCredentialInterceptor.cs`, `NotificationServiceAuthInterceptor.cs`
  - Permission: `PermissionConstants.Notification.Access = "notifications:access"`

---

### Plan 2: Frontend FCM Popup (`2026-08-30-frontend-fcm-popup.md`)

- **Goal:** Connect the Aurora Next.js frontend to `Staff.Bff` to register FCM web tokens, receive foreground (Sonner toast) and background (`firebase-messaging-sw.js`) notifications, maintain unread count, and navigate safely to internal shipment/notification routes.
- **Architecture Decisions:**
  - **Client Boundary:** Frontend calls `NEXT_PUBLIC_API_BASE_URL` with `withCredentials: true` (HttpOnly cookie session).
  - **Firebase Web SDK:** Uses public Web SDK config and VAPID key; never receives Firebase Admin JSON.
  - **Navigation Security:** Restricts action paths to internal allowlisted routes (`/shipments/{id}`, `/notifications`).
- **Implementation Status:** `PARTIAL` (Implemented in sibling repository `aurora-client`; runtime end-to-end acceptance requires active Firebase project and live gateway).

---

## 2. Decision Reconciliation & Source Precedence

| Decision Area | Plan Decision | Code Reality | Documentation Action |
|---|---|---|---|
| **Notification Permission** | `notifications:access` | `PermissionConstants.Notification.Access = "notifications:access"` | Verified. Propagated to `docs/bff-api/` and `docs/technical/frontend/`. |
| **BFF Controller** | `Staff.Bff/Controllers/NotificationsController.cs` | Implemented with 7 endpoints (`devices`, `subscriptions`, list, unread, read, read-all) | Verified. Documented in `shared-api.md` & `API_CATALOG.md`. |
| **Service Auth** | `x-service-id` + `x-service-api-key` | Registered in `Staff.Bff` & `Notification` service pipelines | Verified. Documented in technical architecture. |
| **Audience Fallback** | Fail-closed, persist `NoRecipient`, no tenant broadcast | Implemented in `NotificationEventProcessor.cs` | Verified. Documented in event architecture. |

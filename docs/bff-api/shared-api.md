# Aurora Platform — Shared Cross-Persona API Catalog

> **Document ID:** `DOC-BFF-SHARED`  
> **Status:** Canonical Specification (Synchronized with C# BFF Source)  
> **Scope:** HTTP REST APIs consumed across multiple platform personas (`TENANT_ADMIN`, `MANAGER`, `STAFF`).  
> **Source Precedence:** Source Code & Protos > docs/technical/frontend > docs/bff-api > Figma UI Specs.

---

## 1. Shared APIs Table

| Category | Method | Path | Purpose | Required Permission / Auth | Backend RPC | Status |
|---|---|---|---|---|---|:---:|
| **Auth** | `POST` | `/api/v1/auth/identify` | Step 1 login: check email & tenant code | `AllowAnonymous` | `AuthService.IdentifyUser` | `CURRENT` |
| **Auth** | `POST` | `/api/v1/auth/login` | Authenticate & set HttpOnly cookies | `AllowAnonymous` | `AuthService.Login` | `CURRENT` |
| **Auth** | `POST` | `/api/v1/auth/refresh` | Refresh access token cookie | Cookie Session | `AuthService.RefreshToken` | `CURRENT` |
| **Auth** | `POST` | `/api/v1/auth/logout` | Revoke session & clear cookies | Cookie Session | `AuthService.Logout` | `CURRENT` |
| **Auth** | `GET` | `/api/v1/auth/me` | Get current user profile & capabilities | Cookie Session | `AuthService.GetCurrentUser` | `CURRENT` |
| **Auth** | `POST` | `/api/v1/auth/forgot-password` | Request password reset code via email | `AllowAnonymous` | `AuthService.ForgotPassword` | `CURRENT` |
| **Auth** | `POST` | `/api/v1/auth/confirm-forgot-password` | Complete password reset with code | `AllowAnonymous` | `AuthService.ConfirmForgotPassword` | `CURRENT` |
| **Auth** | `POST` | `/api/v1/auth/complete-invitation` | Set permanent password for newly invited user | `AllowAnonymous` | `AuthService.CompleteInvitation` | `CURRENT` |
| **Notifications** | `POST` | `/api/v1/notifications/devices` | Register browser FCM device token | `notifications:access` | `NotificationService.RegisterDevice` | `CURRENT` |
| **Notifications** | `DELETE`| `/api/v1/notifications/devices/{id}` | Deactivate FCM device registration | `notifications:access` | `NotificationService.RemoveDevice` | `CURRENT` |
| **Notifications** | `POST` | `/api/v1/notifications/subscriptions/shipments/{shipmentId}` | Subscribe to shipment events | `notifications:access` | `NotificationService.SubscribeShipment` | `CURRENT` |
| **Notifications** | `GET` | `/api/v1/notifications` | List user notification history | `notifications:access` | `NotificationService.ListNotifications` | `CURRENT` |
| **Notifications** | `GET` | `/api/v1/notifications/unread-count` | Get unread notification counter badge | `notifications:access` | `NotificationService.GetUnreadCount` | `CURRENT` |
| **Notifications** | `PATCH` | `/api/v1/notifications/{id}/read` | Mark single notification as read | `notifications:access` | `NotificationService.MarkNotificationRead` | `CURRENT` |
| **Notifications** | `PATCH` | `/api/v1/notifications/read-all` | Mark all notifications as read | `notifications:access` | `NotificationService.MarkAllNotificationsRead` | `CURRENT` |
| **Dashboard** | `GET` | `/api/v1/dashboard/summary` | Aggregated user profile & active routes | `route_planning:read` | `IamService.GetUser` + `RoutePlanningService.ListRoutes` | `CURRENT` |
| **Search** | `POST` | `/api/v1/search` | Unified search across regulations & SOPs | `compliance:read` | `RegulatoryComplianceService` (Parallel Query) | `CURRENT` |
| **Assistant** | `POST` | `/api/v1/assistant/query` | Grounded AI question answering | `compliance:read` | `RegulatoryComplianceService.GenerateGroundedAnswer` | `CURRENT` |

---

## 2. Authentication & Session Cookie Contract

### `POST /api/v1/auth/login`
- **Request Body:**
  ```json
  {
    "email": "staff@acmelogistics.com",
    "password": "SecurePassword123!",
    "tenantCode": "acmelogistics"
  }
  ```
- **Response Headers:**
  ```http
  Set-Cookie: access_token=<JWT>; Path=/; HttpOnly; Secure; SameSite=Lax; Max-Age=3600
  Set-Cookie: refresh_token=<Token>; Path=/api/v1/auth; HttpOnly; Secure; SameSite=Strict; Max-Age=2592000
  ```
- **Response Body (`200 OK`):**
  ```json
  {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "tenantId": "e5b8ba84-0000-0000-0000-000000000001",
    "roles": ["STAFF"],
    "permissions": [
      "shipments:read",
      "shipments:create",
      "mail:read",
      "mail:draft:create",
      "mail:send",
      "mail:thread:claim",
      "notifications:access"
    ],
    "expiresIn": 3600
  }
  ```
- **Security Rule:** Tokens are **never** returned in the JSON body. Frontend Axios client uses `withCredentials: true`.

---

## 3. Notification Center Contract

### `POST /api/v1/notifications/devices`
- **Permission:** `notifications:access`
- **Request Body:**
  ```json
  {
    "token": "dK3f9...browser_fcm_token...",
    "platform": "Web",
    "appVersion": "1.0.0"
  }
  ```
- **Response (`200 OK`):**
  ```json
  {
    "id": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
    "platform": "Web",
    "isActive": true,
    "createdAt": "2026-09-04T12:00:00Z"
  }
  ```

---

### `GET /api/v1/notifications`
- **Permission:** `notifications:access`
- **Query Parameters:** `page=1`, `pageSize=20`, `unreadOnly=false`
- **Response (`200 OK`):**
  ```json
  {
    "notifications": [
      {
        "id": "8fa85f64-5717-4562-b3fc-2c963f66afa6",
        "eventType": "SHIPMENT_DELIVERED",
        "channel": "FCM",
        "title": "Shipment Delivered",
        "body": "Shipment SHP-2026-001 has been delivered to Rotterdam Hub.",
        "isRead": false,
        "createdAt": "2026-09-04T12:30:00Z",
        "readAt": null,
        "shipmentId": "9fa85f64-5717-4562-b3fc-2c963f66afa6",
        "shipmentNumber": "SHP-2026-001",
        "actionUrl": "/shipments/9fa85f64-5717-4562-b3fc-2c963f66afa6"
      }
    ],
    "page": 1,
    "pageSize": 20,
    "totalItems": 1,
    "totalPages": 1
  }
  ```

# Aurora Platform — Authoritative Frontend REST API Catalog

> **Document ID:** `DOC-FE-CATALOG`  
> **Status:** Canonical Frontend Engineering Reference (Synchronized with C# .NET 10 BFF Controllers)  
> **Source Precedence:** Source Code & Protos > docs/technical/frontend > docs/bff-api > Figma UI Specs.  
> **Architecture Rule:** Distinguish between `CURRENT` (implemented in C# BFF) and `TARGET (BACKEND_REQUIRED)`.

---

## 1. Authentication & Identity APIs (`Staff.Bff` / Gateway)

### 1.1 `POST /api/v1/auth/identify`
- **Persona Shell:** All / Public Landing
- **Purpose:** Step 1 of the enterprise login flow. Checks if the email exists and retrieves associated `tenantCode` and `userType`.
- **Permission:** `[AllowAnonymous]`
- **Scope:** Public
- **Request Body:**
  ```json
  {
    "email": "operator@acmelogistics.com"
  }
  ```
- **Response Body (`200 OK`):**
  ```json
  {
    "exists": true,
    "tenantCode": "acmelogistics",
    "userType": "STAFF"
  }
  ```
- **Backend RPC:** `AuthService.IdentifyUser`
- **Status:** `CURRENT`

---

### 1.2 `POST /api/v1/auth/login`
- **Persona Shell:** All / Public Landing
- **Purpose:** Authenticates user with credentials and issues secure HttpOnly session cookies.
- **Permission:** `[AllowAnonymous]`
- **Scope:** Public
- **Request Body:**
  ```json
  {
    "email": "operator@acmelogistics.com",
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
- **Error Codes:** `401 Unauthorized` (Invalid credentials), `409 Conflict` (`requiresInvitationCompletion = true`).
- **Status:** `CURRENT`

---

### 1.3 `POST /api/v1/auth/refresh`
- **Persona Shell:** All Authenticated Users
- **Purpose:** Rotates the `access_token` cookie using the `refresh_token` cookie.
- **Permission:** Cookie Session (`[Authorize]`)
- **Scope:** User Session
- **Response Body (`200 OK`):**
  ```json
  {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "tenantId": "e5b8ba84-0000-0000-0000-000000000001",
    "roles": ["STAFF"],
    "permissions": ["..."],
    "expiresIn": 3600
  }
  ```
- **Status:** `CURRENT`

---

### 1.4 `POST /api/v1/auth/logout`
- **Persona Shell:** All Authenticated Users
- **Purpose:** Invalidates session cookies on the client and backend.
- **Permission:** Cookie Session
- **Scope:** User Session
- **Response:** `200 OK` (Clears `access_token`, `refresh_token`, `tenant_code`, `user_type` cookies).
- **Status:** `CURRENT`

---

### 1.5 `GET /api/v1/auth/me`
- **Persona Shell:** All Authenticated Users
- **Purpose:** Retrieves the current authenticated user profile, base role, and active permissions array on SPA bootstrap.
- **Permission:** Cookie Session
- **Scope:** Current User
- **Response Body (`200 OK`):**
  ```json
  {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "operator@acmelogistics.com",
    "firstName": "Alex",
    "lastName": "Nguyen",
    "tenantId": "e5b8ba84-0000-0000-0000-000000000001",
    "tenantCode": "acmelogistics",
    "roles": ["STAFF"],
    "permissions": [
      "shipments:read",
      "shipments:create",
      "mail:read",
      "mail:thread:claim",
      "notifications:access"
    ]
  }
  ```
- **Status:** `CURRENT`

---

## 2. Notification Center APIs (`Staff.Bff`)

### 2.1 `POST /api/v1/notifications/devices`
- **Persona Shell:** Operations Workspace / Admin Console
- **Purpose:** Registers browser FCM device token for web push popups.
- **Permission:** `notifications:access`
- **Scope:** Current User
- **Request Body:**
  ```json
  {
    "token": "dK3f9...browser_fcm_registration_token...",
    "platform": "Web",
    "appVersion": "1.0.0"
  }
  ```
- **Response Body (`200 OK`):**
  ```json
  {
    "id": "7fa85f64-5717-4562-b3fc-2c963f66afa6",
    "platform": "Web",
    "isActive": true,
    "createdAt": "2026-09-04T12:00:00Z"
  }
  ```
- **Status:** `CURRENT`

---

### 2.2 `GET /api/v1/notifications`
- **Persona Shell:** Operations Workspace / Admin Console
- **Purpose:** Lists durable in-app notifications with pagination.
- **Permission:** `notifications:access`
- **Query Parameters:** `page=1`, `pageSize=20`, `unreadOnly=false`
- **Response Body (`200 OK`):**
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
- **Status:** `CURRENT`

---

### 2.3 `GET /api/v1/notifications/unread-count`
- **Permission:** `notifications:access`
- **Response Body (`200 OK`):**
  ```json
  {
    "count": 3
  }
  ```
- **Status:** `CURRENT`

---

### 2.4 `PATCH /api/v1/notifications/{id}/read` & `PATCH /api/v1/notifications/read-all`
- **Permission:** `notifications:access`
- **Status:** `CURRENT`

---

## 3. Operations Workspace: Mail Module APIs (`Staff.Bff`)

### 3.1 `GET /api/v1/mail/threads`
- **Persona Shell:** Aurora Operations Workspace -> Mail Module
- **Purpose:** Retrieves email threads for queue triage.
- **Permission:**
  - `scope=UNASSIGNED` or `scope=MY_WORK`: requires `mail:read`.
  - `scope=ALL`: requires supervisory permission `mail:thread:read_all`.
- **Query Parameters:**
  - `scope` (string, optional, default: `MY_WORK`): `UNASSIGNED` | `MY_WORK` | `ALL`
  - `mailboxId` (UUID, optional): Filter by shared mailbox.
  - `pageSize` (int, default: 20, max: 100)
  - `pageToken` (string, optional): Base64 pagination cursor.
  - `status` (string, optional): `UNASSIGNED` | `IN_PROGRESS` | `WAITING_CUSTOMER` | `RESOLVED`
  - `search` (string, optional): Search keyword in subject/snippet/participants.
- **Response Body (`200 OK`):**
  ```json
  {
    "threads": [
      {
        "threadId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "mailboxId": "4ba85f64-5717-4562-b3fc-2c963f66afa6",
        "subject": "Urgent Booking Request - 2x40HC HCM to Rotterdam",
        "participants": ["shipper@client.com", "ops@acmelogistics.com"],
        "lastMessageAt": "2026-09-04T12:00:00Z",
        "messageCount": 3,
        "draftCount": 1,
        "hasUnread": false,
        "snippet": "Please confirm space allocation for container...",
        "primaryAssigneeUserId": "1fa85f64-5717-4562-b3fc-2c963f66afa6",
        "assignedAt": "2026-09-04T12:15:00Z",
        "status": "IN_PROGRESS",
        "priority": "HIGH"
      }
    ],
    "nextPageToken": "eyJ...",
    "hasMore": false
  }
  ```
- **Status:** `CURRENT`

---

### 3.2 `POST /api/v1/mail/threads/{id}/claim`
- **Purpose:** Atomically claims an unassigned thread (**"Take Thread"**).
- **Permission:** `mail:thread:claim`
- **Concurrency Protection:** Uses thread `Version`. If another staff member claimed the thread concurrently, returns `409 Conflict`.
- **Response Body (`200 OK`):**
  ```json
  {
    "success": true,
    "threadId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "primaryAssigneeUserId": "1fa85f64-5717-4562-b3fc-2c963f66afa6",
    "assignedAt": "2026-09-04T12:15:00Z",
    "status": "IN_PROGRESS"
  }
  ```
- **Status:** `CURRENT`

---

### 3.3 `POST /api/v1/mail/threads/{id}/reassign`
- **Purpose:** Supervisory reassignment of a thread to another team member.
- **Permission:** `mail:thread:reassign`
- **Request Body:**
  ```json
  {
    "targetUserId": "2fa85f64-5717-4562-b3fc-2c963f66afa6",
    "reason": "Reassigned to Customs Specialist for document clearance."
  }
  ```
- **Status:** `CURRENT`

---

### 3.4 `POST /api/v1/mail/threads/{id}/unassign`
- **Purpose:** Releases thread back to the unassigned queue.
- **Permission:** `mail:thread:unassign`
- **Request Body:**
  ```json
  {
    "reason": "Customer inquiry outside assigned scope."
  }
  ```
- **Status:** `CURRENT`

---

### 3.5 `POST /api/v1/mail/drafts` & `GET /api/v1/mail/drafts`
- **Purpose:** Creates or fetches email drafts tied to a thread.
- **Permission:** `mail:draft:create` (write) / `mail:read` (read)
- **Request Body:**
  ```json
  {
    "mailboxId": "4ba85f64-5717-4562-b3fc-2c963f66afa6",
    "threadId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "subject": "Re: Urgent Booking Request - 2x40HC HCM to Rotterdam",
    "body": "<p>Dear Shipper, booking confirmed for vessel CMA CGM...</p>",
    "source": "Manual",
    "toRecipients": ["shipper@client.com"]
  }
  ```
- **Status:** `CURRENT`

---

### 3.6 `POST /api/v1/mail/messages/outbound`
- **Purpose:** Submits outbound email to Stalwart SMTP relay with human attribution.
- **Permission:** `mail:send`
- **Request Body:**
  ```json
  {
    "senderAddress": "ops@acmelogistics.com",
    "recipientAddresses": ["shipper@client.com"],
    "subject": "Booking Confirmation - 2x40HC HCM to Rotterdam",
    "bodyHtml": "<p>Dear Shipper, your booking is confirmed.</p>",
    "threadId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  }
  ```
- **Response Body (`200 OK`):**
  ```json
  {
    "processedMessageId": "5fa85f64-5717-4562-b3fc-2c963f66afa6",
    "stalwartQueueId": "queue_msg_9842",
    "submittedAt": "2026-09-04T12:20:00Z"
  }
  ```
- **Status:** `CURRENT`

---

## 4. Operations Workspace: Shipments, Routes & Approvals

### 4.1 Shipments Endpoints
- `POST /api/v1/shipments` (`shipments:create`) — Creates draft shipment.
- `GET /api/v1/shipments` (`shipments:read`) — Lists shipments with query filters (`page`, `limit`, `status`, `shipmentNo`, `customerName`).
- `GET /api/v1/shipments/{id}` (`shipments:read`) — Retrieves detailed shipment aggregate.
- `PUT /api/v1/shipments/{id}` (`shipments:update`) — Updates draft shipment.
- `POST /api/v1/shipments/{id}/submit` (`shipments:submit`) — Transitions shipment to active workflow.
- `POST /api/v1/shipments/{id}/cancel` (`shipments:cancel`) — Cancels active shipment.
- `POST /api/v1/shipments/{id}/milestones` (`shipments:milestones:update`) — Records tracking milestone.
- **Status:** `CURRENT`

---

### 4.2 Routes & Approvals Endpoints
- `POST /api/v1/routes` (`route_planning:create`) — Creates multi-stop route.
- `GET /api/v1/routes` (`route_planning:read`) — Lists planned routes.
- `POST /api/v1/routes/{id}/optimize` (`route_planning:optimize`) — Optimizes stops and travel time.
- `POST /api/v1/routes/{id}/evaluate-risk` (`route_planning:risk:evaluate`) — Generates route risk score $[0, 100]$.
- `POST /api/v1/routes/{id}/dispatch` (`route_planning:dispatch`) — Dispatches approved route.
- `GET /api/v1/approvals` (`route_planning:approve`) — Lists pending high-risk route approval tickets.
- `POST /api/v1/approvals/{id}/approve` (`route_planning:approve`) — Authorizes high-risk route dispatch.
- `POST /api/v1/approvals/{id}/reject` (`route_planning:approve`) — Rejects high-risk route dispatch.
- **Status:** `CURRENT`

---

## 5. Tenant Admin Console APIs (`Admin.Bff`)

### 5.1 Staff & IAM Lifecycle
- `POST /api/v1/admin/staff` (`iam:user:invite`) — Invites user with baseline permissions.
- `GET /api/v1/admin/staff` (`iam:user:read`) — Lists directory with pagination (`page`, `limit`).
- `PATCH /api/v1/admin/staff/{id}/role` (`iam:role:manage`) — Updates base persona role (`STAFF` ↔ `MANAGER` ↔ `TENANT_ADMIN`).
- `PUT /api/v1/admin/staff/{id}/permissions` (`iam:permission:manage`) — Sets explicit capability permissions.
- `DELETE /api/v1/admin/staff/{id}` (`iam:user:delete`) — Deactivates staff member.
- **Status:** `CURRENT`

---

### 5.2 Operations & AI Governance Configuration
- `GET /api/v1/admin/ai-configs/{feature}` (`route_planning:policy:manage`) — Gets AI policy (e.g. `RulesAndLlm`).
- `PUT /api/v1/admin/ai-configs/{feature}` (`route_planning:policy:manage`) — Updates AI policy.
- `GET /api/v1/admin/rule-configs` (`route_planning:policy:manage`) — Lists risk rule thresholds.
- `PUT /api/v1/admin/rule-configs/{ruleName}` (`route_planning:policy:manage`) — Configures risk rule threshold.
- **Status:** `CURRENT`

---

### 5.3 Mail Administration
- `POST /api/v1/admin/mail/domains` (`mail:domain:manage`) — Provision arbitrary domain.  
  *Status:* `CURRENT_LEGACY` *(Target architecture requires System Admin provisioning & Tenant Admin viewing)*.
- `GET /api/v1/admin/mail/domains` (`mail:domain:manage`) — List assigned domains.  
  *Status:* `TARGET (BACKEND_REQUIRED)`.
- `POST /api/v1/admin/mail/mailboxes` (`mail:mailbox:manage`) — Creates shared company mailbox.  
  *Status:* `CURRENT`.
- `POST /api/v1/admin/mail/aliases` (`mail:mailbox:manage`) — Creates forwarding alias.  
  *Target Constraint:* 1 Alias -> exactly 1 Shared Mailbox.  
  *Status:* `CURRENT` (supports `List<string>`), `TARGET_CHANGE_REQUIRED`.
- `DELETE /api/v1/admin/mail/quarantine/{id}` (`mail:quarantine:delete`) — Purges security threat.  
  *Status:* `CURRENT`.
- `GET /api/v1/admin/mail/audit` (`mail:audit:read`) — Queries mail audit trail.  
  *Status:* `CURRENT`.

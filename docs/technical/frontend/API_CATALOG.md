# Aurora Server — Authoritative Frontend REST API Catalog

> **Source-of-Truth**: Audited directly against `BuildingBlocks.BFF`, `Staff.Bff`, `Admin.Bff`, `System.Bff`, and downstream gRPC services. All routes use prefix `/api/v1/` (or `/api/v1/admin/`, `/api/v1/system/`).

---

## 1. Authentication & Session APIs (`BuildingBlocks.BFF`)

### 1.1 `GET /api/v1/auth/login`
- **Role / UI**: All Personas (`STAFF`, `MANAGER`, `TENANT_ADMIN`, `SYSTEM_ADMIN`)
- **Purpose**: Initiates Cognito Hosted UI OAuth2 challenge and redirects user to Cognito login page.
- **Permission**: `[AllowAnonymous]` (Public)
- **Resource Scope**: Global / Tenant
- **Query Parameters**:
  - `returnUrl` (string, optional, default: `/`): Target redirect URI upon successful authentication.
- **Request DTO**: None (HTTP GET redirect)
- **Response**: HTTP 302 Redirect to AWS Cognito / IdP Hosted UI.
- **Validation**: `returnUrl` must be a relative local URL or match allowed AppDomain.
- **Possible HTTP Errors**: None (Redirect).
- **Backend Service / RPC**: AWS Cognito OpenID Connect challenge.
- **Side Effects**: Sets temporary state cookie for PKCE/OAuth handshake.
- **Events Emitted**: None.
- **AI Involvement**: None.
- **FE Usage Notes**: Triggered when the SPA encounters an unauthenticated state or when user clicks "Log In".
- **Status**: `READY`

---

### 1.2 `POST /api/v1/auth/logout` & `GET /api/v1/auth/logout`
- **Role / UI**: All Authenticated Users
- **Purpose**: Clears the BFF cookie session and redirects to Cognito global sign-out.
- **Permission**: `[Authorize]` (Authenticated)
- **Resource Scope**: Current User Session
- **Query Parameters**:
  - `returnUrl` (string, optional): Redirect URI after logout.
- **Request DTO**: None
- **Response**: HTTP 302 Redirect to Cognito logout endpoint (`/logout?client_id=...`).
- **Validation**: Valid session cookie required.
- **Possible HTTP Errors**: `401 Unauthorized`.
- **Backend Service / RPC**: In-memory / distributed cookie invalidation.
- **Side Effects**: Revokes `.AspNetCore.Cookies` session and clears refresh token.
- **Events Emitted**: None.
- **AI Involvement**: None.
- **FE Usage Notes**: SPA should clear local client-side state / stores and invoke this endpoint.
- **Status**: `READY`

---

### 1.3 `GET /api/v1/auth/me`
- **Role / UI**: All Authenticated Users
- **Purpose**: Returns user profile and identity claims extracted directly from the authenticated session cookie.
- **Permission**: `[Authorize]`
- **Resource Scope**: Current User
- **Request DTO**: None
- **Response DTO**:
  ```json
  {
    "email": "operator@acme-logistics.com",
    "emailDomain": "acme-logistics.com",
    "cognitoSub": "4f1a23b4-...",
    "userId": "9a3c7e81-...",
    "tenantId": "e5b8ba84-...",
    "name": "Jane Doe",
    "role": "STAFF",
    "permissions": [
      "mail:read",
      "mail:draft:create",
      "mail:send",
      "shipments:read",
      "shipments:create",
      "route_planning:read",
      "route_planning:optimize"
    ],
    "isAuthenticated": true
  }
  ```
- **Validation**: Requires active session.
- **Possible HTTP Errors**: `401 Unauthorized`.
- **Backend Service / RPC**: Stateless cookie decryption + current user context.
- **Side Effects**: None.
- **Events Emitted**: None.
- **AI Involvement**: None.
- **FE Usage Notes**: Called on SPA bootstrap to initialize global auth state, persona role, and capability permissions.
- **Status**: `READY`

---

## 2. Mail Platform & Shared Inbox APIs (`Staff.Bff`)

### 2.1 `GET /api/v1/mail/threads`
- **Role / UI**: `STAFF` (own work), `MANAGER` (all threads supervision)
- **Purpose**: Lists inbound email threads categorized by folder (`UNASSIGNED`, `MY_WORK`, `ALL`, `ARCHIVED`).
- **Permission**: `mail:read`
- **Resource Scope**:
  - `folder=UNASSIGNED`: Unclaimed threads across tenant mailbox.
  - `folder=MY_WORK`: Threads where `PrimaryAssigneeUserId == CurrentUser.UserId`.
  - `folder=ALL`: Requires supervisory permission `mail:thread:read_all`.
- **Query Parameters**:
  - `folder` (string, default: `MY_WORK`): `UNASSIGNED` | `MY_WORK` | `ALL` | `ARCHIVED`
  - `page` (int, default: 1)
  - `pageSize` (int, default: 20)
  - `searchTerm` (string, optional)
- **Request DTO**: None
- **Response DTO**:
  ```json
  {
    "threads": [
      {
        "threadId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "tenantId": "e5b8ba84-...",
        "subject": "Quote Request - Shipment SG-VN #8942",
        "snippet": "Please find attached the packing list for shipment...",
        "participantEmails": ["customer@importer.com", "ops@acme.com"],
        "assignedUserId": "9a3c7e81-...",
        "assignedStaffName": "Jane Doe",
        "status": "ASSIGNED",
        "classification": "FREIGHT_QUOTE",
        "messageCount": 3,
        "lastMessageAt": "2026-08-27T08:30:00Z",
        "riskScore": 0.05
      }
    ],
    "totalCount": 12,
    "page": 1,
    "pageSize": 20,
    "unassignedCount": 4
  }
  ```
- **Validation**: `page >= 1`, `pageSize <= 100`.
- **Possible HTTP Errors**: `401 Unauthorized`, `403 Forbidden` (if accessing `folder=ALL` without `mail:thread:read_all`).
- **gRPC RPC**: `mail.MailSecurity.ListThreads` -> `MailService`
- **Side Effects**: None.
- **Events Emitted**: None.
- **AI Involvement**: Thread classification and risk score evaluated during ingestion via `mail.risk_scoring`.
- **FE Usage Notes**: Powers the main 3-pane split inbox view.
- **Status**: `READY`

---

### 2.2 `POST /api/v1/mail/threads/{id}/claim`
- **Role / UI**: `STAFF`
- **Purpose**: Atomically claims an unassigned email thread for the current user.
- **Permission**: `mail:thread:claim` (Fallback: `mail:update`)
- **Resource Scope**: Thread must be in `UNASSIGNED` state or unassigned.
- **Request DTO**: None (Path ID only).
- **Response DTO**: `ThreadDto` (Updated thread with `assignedUserId = CurrentUser.UserId`).
- **Validation**: Thread must not already be claimed by another staff member.
- **Possible HTTP Errors**: `400 BadRequest`, `404 NotFound`, `409 Conflict` (if claimed concurrently by another agent).
- **gRPC RPC**: `mail.MailSecurity.ClaimThread` -> `MailService`
- **Side Effects**: Acquires Redis distributed claim lock, updates DB assignment, records entry in `thread_assignment_history`.
- **Events Emitted**: `thread_claimed_event` (broadcasts live lock to other staff via `RealtimeHub`).
- **AI Involvement**: None.
- **FE Usage Notes**: Disables the "Claim" button in real-time on other users' UIs when WebSocket event arrives.
- **Status**: `READY`

---

### 2.3 `POST /api/v1/mail/threads/{id}/reassign`
- **Role / UI**: `MANAGER`, `TENANT_ADMIN`
- **Purpose**: Supervisory reassignment of a thread from one staff member to another.
- **Permission**: `mail:thread:reassign` (Fallback: `mail:assign`)
- **Resource Scope**: Any thread within the tenant.
- **Request DTO**:
  ```json
  {
    "newAssigneeUserId": "7b8c9d01-...",
    "reason": "Escalation to senior customs broker"
  }
  ```
- **Response DTO**: `ThreadDto`
- **Validation**: `newAssigneeUserId` must exist and be an active staff member in the same tenant.
- **Possible HTTP Errors**: `400 BadRequest`, `403 Forbidden`, `404 NotFound`.
- **gRPC RPC**: `mail.MailSecurity.ReassignThread` -> `MailService`
- **Side Effects**: Updates assignment, writes to audit log and history table.
- **Events Emitted**: `thread_reassigned_event`.
- **AI Involvement**: None.
- **FE Usage Notes**: Available only in Manager supervision view or thread action dropdown.
- **Status**: `READY`

---

### 2.4 `POST /api/v1/mail/threads/{id}/unassign`
- **Role / UI**: `MANAGER`, `TENANT_ADMIN`
- **Purpose**: Releases a thread back to the `UNASSIGNED` queue.
- **Permission**: `mail:thread:unassign` (Fallback: `mail:assign`)
- **Resource Scope**: Tenant thread.
- **Request DTO**:
  ```json
  {
    "reason": "Staff member out of office"
  }
  ```
- **Response DTO**: `ThreadDto`
- **Validation**: Valid thread ID.
- **Possible HTTP Errors**: `400 BadRequest`, `403 Forbidden`, `404 NotFound`.
- **gRPC RPC**: `mail.MailSecurity.UnassignThread` -> `MailService`
- **Side Effects**: Resets `assignedUserId = null`, adds history record.
- **Events Emitted**: `thread_unassigned_event`.
- **Status**: `READY`

---

### 2.5 `GET /api/v1/mail/threads/{id}/assignment-history`
- **Role / UI**: `STAFF`, `MANAGER`
- **Purpose**: Returns complete chronological audit trail of all assignments, reassignments, and claims for a thread.
- **Permission**: `mail:read`
- **Resource Scope**: Tenant thread.
- **Request DTO**: None.
- **Response DTO**:
  ```json
  {
    "threadId": "3fa85f64-...",
    "history": [
      {
        "id": "1111-...",
        "previousAssigneeUserId": null,
        "newAssigneeUserId": "9a3c7e81-...",
        "assignedByUserId": "9a3c7e81-...",
        "action": "CLAIM",
        "reason": "Self-claimed from unassigned queue",
        "timestamp": "2026-08-27T08:35:00Z"
      }
    ]
  }
  ```
- **Status**: `READY`

---

### 2.6 `POST /api/v1/mail/drafts` & `GET /api/v1/mail/drafts`
- **Role / UI**: `STAFF`
- **Purpose**: Creates or retrieves draft email messages.
- **Permission**: `mail:draft:create` / `mail:read`
- **Request DTO (`POST`)**:
  ```json
  {
    "threadId": "3fa85f64-...",
    "recipientEmails": ["customer@importer.com"],
    "ccEmails": [],
    "bccEmails": [],
    "subject": "Re: Freight Quote SG-VN",
    "bodyHtml": "<p>Dear Customer, our quoted rate is $1,250 USD.</p>",
    "bodyText": "Dear Customer, our quoted rate is $1,250 USD.",
    "attachmentStorageReferences": ["r2://mail-attachments/quote-123.pdf"]
  }
  ```
- **Response DTO**: `DraftDto`
- **gRPC RPC**: `mail.MailSecurity.CreateDraftMessage`, `ListDrafts`, `GetDraft`
- **Status**: `READY`

---

### 2.7 `POST /api/v1/mail/messages/outbound`
- **Role / UI**: `STAFF`
- **Purpose**: Submits a finalized email message to the Stalwart SMTP pipeline for delivery.
- **Permission**: `mail:send`
- **Resource Scope**: Staff can only send replies on threads assigned to them.
- **Request DTO**:
  ```json
  {
    "draftId": "3fa85f64-...",
    "senderEmail": "jane.doe@acme-logistics.com",
    "recipientEmails": ["customer@importer.com"],
    "subject": "Re: Freight Quote SG-VN",
    "bodyHtml": "<p>Confirmed quote details attached.</p>",
    "bodyText": "Confirmed quote details attached."
  }
  ```
- **Response DTO**:
  ```json
  {
    "messageId": "msg-8812-...",
    "status": "QUEUED_FOR_DELIVERY",
    "stalwartQueueId": "queue-99120"
  }
  ```
- **Side Effects**: Verifies sender domain authorization, signs DKIM, enqueues to Stalwart, publishes `outbound_email_sent_event`.
- **Status**: `READY`

---

### 2.8 `GET /api/v1/mail/quarantine` & `POST /api/v1/mail/quarantine/{id}/release`
- **Role / UI**: `MANAGER`, `TENANT_ADMIN`
- **Purpose**: Lists quarantined threat emails and releases false positives after inspection.
- **Permission**: `mail:quarantine:read` / `mail:quarantine:release`
- **Request DTO (`POST release`)**:
  ```json
  {
    "releaseReason": "Verified legitimate invoice sender after phone confirmation"
  }
  ```
- **Response DTO**: `QuarantineRecordDto`
- **Status**: `READY`

---

## 3. Shipments & Logistics Workflow APIs (`Staff.Bff`)

### 3.1 `POST /api/v1/shipments`
- **Role / UI**: `STAFF` (`OPERATOR`)
- **Purpose**: Creates a new shipment in `DRAFT` status.
- **Permission**: `shipments:create` (Fallback: `documents:create`)
- **Request DTO**:
  ```json
  {
    "orderId": "ORD-2026-0091",
    "customerName": "VinFast Global Trade",
    "originAddress": "Cat Lai Port, Ho Chi Minh City, Vietnam",
    "destinationAddress": "Jurong Port, Singapore",
    "originCountry": "VN",
    "destinationCountry": "SG",
    "cargoItems": [
      {
        "name": "Lithium Battery Cells Pack",
        "quantity": 500,
        "weightKg": 4500.0,
        "hsCode": "8507.60.00"
      }
    ]
  }
  ```
- **Response DTO**: `ShipmentDto` (contains generated `shipmentNumber`, `id`, `status: DRAFT`).
- **Validation**: Non-empty origin/destination; positive weight; valid ISO country codes.
- **Possible HTTP Errors**: `400 BadRequest`, `401 Unauthorized`, `403 Forbidden`.
- **gRPC RPC**: `shipment.ShipmentWorkflowService.CreateShipment` -> `ShipmentWorkflow`
- **Side Effects**: Inserts `Shipment` record and writes `ShipmentCreatedEvent` to PostgreSQL Outbox.
- **Status**: `READY`

---

### 3.2 `GET /api/v1/shipments` & `GET /api/v1/shipments/{id}`
- **Role / UI**: `STAFF`
- **Purpose**: Lists paginated shipments or retrieves shipment details (with cargo, locations, documents, milestones).
- **Permission**: `shipments:read`
- **Query Parameters**:
  - `page` (int, default: 1), `pageSize` (int, default: 20)
  - `status` (string, optional: `DRAFT`, `SUBMITTED`, `BOOKED`, `IN_TRANSIT`, `DELIVERED`, `COMPLETED`, `CANCELLED`)
  - `search` (string, optional)
- **Response DTO**:
  ```json
  {
    "shipments": [
      {
        "id": "7fa85f64-...",
        "shipmentNumber": "SHP-20260827-0042",
        "orderId": "ORD-2026-0091",
        "customerName": "VinFast Global Trade",
        "status": "DRAFT",
        "transportMode": "SEA",
        "originAddress": "Cat Lai Port, VN",
        "destinationAddress": "Jurong Port, SG",
        "cargoItemsCount": 1,
        "totalWeightKg": 4500.0,
        "createdAt": "2026-08-27T06:00:00Z"
      }
    ],
    "totalCount": 45,
    "page": 1,
    "pageSize": 20
  }
  ```
- **Status**: `READY`

---

### 3.3 `POST /api/v1/shipments/{id}/submit`
- **Role / UI**: `STAFF`
- **Purpose**: Transitions shipment from `DRAFT` to `SUBMITTED` for compliance and dispatch approval.
- **Permission**: `shipments:submit` (Fallback: `documents:update`)
- **Request DTO**: None.
- **Response DTO**: `ShipmentDto` (`status: SUBMITTED`).
- **Side Effects**: Publishes `ShipmentSubmittedEvent` which triggers automated trade compliance checking.
- **Status**: `READY`

---

### 3.4 `POST /api/v1/shipments/{id}/documents` & `DELETE /api/v1/shipments/{id}/documents/{documentId}`
- **Role / UI**: `STAFF`
- **Purpose**: Attaches or removes documents (Bill of Lading, Invoice, Packing List, Certificate of Origin).
- **Permission**: `shipments:create` / `shipments:delete`
- **Request DTO**:
  ```json
  {
    "fileName": "Commercial_Invoice_INV0981.pdf",
    "documentType": "COMMERCIAL_INVOICE",
    "storageUrl": "r2://shipment-docs/e5b8/INV0981.pdf",
    "ocrStatus": "PENDING"
  }
  ```
- **Response DTO**: `ShipmentDto`
- **Side Effects**: Attaching triggers `DocumentAttachedEvent` which starts background OCR job.
- **Status**: `READY`

---

### 3.5 `POST /api/v1/shipments/import`
- **Role / UI**: `MANAGER`, `TENANT_ADMIN`
- **Purpose**: Batch import of multiple shipments from structured CSV/Excel payloads.
- **Permission**: `shipments:import`
- **Request DTO**:
  ```json
  {
    "fileName": "batch_import_august.csv",
    "content": "base64EncodedContent...",
    "importRequestId": "imp-9912-..."
  }
  ```
- **Response DTO**:
  ```json
  {
    "importRequestId": "imp-9912-...",
    "totalProcessed": 150,
    "successCount": 148,
    "failureCount": 2,
    "errors": [
      { "rowNumber": 14, "reason": "Invalid destination country code 'XX'" }
    ]
  }
  ```
- **Status**: `READY`

---

## 4. Route Planning & Optimization APIs (`Staff.Bff` & `Admin.Bff`)

### 4.1 `POST /api/v1/routes` & `POST /api/v1/routes/{id}/optimize`
- **Role / UI**: `STAFF`
- **Purpose**: Creates route waypoints and runs VROOM vehicle routing optimization.
- **Permission**: `route_planning:create` / `route_planning:optimize`
- **Request DTO (`POST optimize`)**:
  ```json
  {
    "trafficModel": "BEST_GUESS",
    "maxDriverHours": 9,
    "avoidTolls": false
  }
  ```
- **Response DTO**:
  ```json
  {
    "routeId": "rt-5512-...",
    "totalDistanceKm": 248.5,
    "totalDurationMinutes": 310,
    "riskLevel": "MEDIUM",
    "requiresApproval": false,
    "waypoints": [
      { "sequence": 1, "name": "Warehouse A", "eta": "2026-08-27T08:00:00Z" },
      { "sequence": 2, "name": "Hub B", "eta": "2026-08-27T11:30:00Z" }
    ]
  }
  ```
- **Status**: `READY`

---

### 4.2 `GET /api/v1/approvals/routes` & `POST /api/v1/approvals/routes/{id}/approve`
- **Role / UI**: `MANAGER`
- **Purpose**: Review and approve/reject high-risk route execution requests.
- **Permission**: `route_planning:approval:read` / `route_planning:approve`
- **Request DTO (`POST approve`)**:
  ```json
  {
    "comment": "Approved following weather clearance confirmation."
  }
  ```
- **Response DTO**: `ApprovalResponse`
- **Side Effects**: Transitions route to `APPROVED` and publishes `route_approved_event`.
- **Status**: `READY`

---

### 4.3 `GET /api/v1/admin/ai-configs/{feature}` & `PUT /api/v1/admin/ai-configs/{feature}`
- **Role / UI**: `TENANT_ADMIN`
- **Purpose**: Configures AI model preferences and automation thresholds per feature.
- **Permission**: `route_planning:policy:manage`
- **Request DTO (`PUT`)**:
  ```json
  {
    "provider": "AzureOpenAI",
    "model": "gpt-4o",
    "temperature": 0.2,
    "maxTokens": 4096,
    "automationLevel": "SEMI_AUTONOMOUS"
  }
  ```
- **Response DTO**: `TenantAiConfigResponse`
- **Status**: `READY`

---

## 5. Document OCR & Regulatory Compliance APIs (`Staff.Bff`)

### 5.1 `POST /api/v1/documents/ocr/jobs` & `POST /api/v1/documents/ocr/jobs/{id}/review`
- **Role / UI**: `STAFF` (submit), `CUSTOMS_OFFICER` / `MANAGER` (review)
- **Purpose**: Submits documents for OCR extraction and reviews low-confidence fields.
- **Permission**: `ocr:review` (for human correction).
- **Request DTO (`POST review`)**:
  ```json
  {
    "action": "CORRECT",
    "fields": [
      { "name": "TaxId", "value": "0312345678" },
      { "name": "TotalAmount", "value": "12500.00" }
    ],
    "comment": "Corrected blurry tax identification number"
  }
  ```
- **Response DTO**: `UnifiedDocumentStatusResponse`
- **Status**: `READY`

---

### 5.2 `POST /api/v1/compliance/evaluate`
- **Role / UI**: `CUSTOMS_OFFICER`, `STAFF`
- **Purpose**: Evaluates a shipment manifest against trade regulations and returns findings & citations.
- **Permission**: Standard operational access (Override requires `compliance:override`).
- **Request DTO**:
  ```json
  {
    "shipmentId": "7fa85f64-...",
    "originCountry": "VN",
    "destinationCountry": "SG",
    "hsCodes": ["8507.60.00"],
    "totalDeclaredValue": 45000.00,
    "currency": "USD"
  }
  ```
- **Response DTO**:
  ```json
  {
    "evaluationId": "eval-9901-...",
    "status": "PASSED_WITH_WARNINGS",
    "complianceConfidence": 0.94,
    "findings": [
      {
        "type": "DOCUMENT_REQUIREMENT",
        "severity": "WARNING",
        "message": "Dangerous goods declaration required for Lithium batteries under Singapore Maritime Authority Circular #14/2024.",
        "citations": [
          { "authority": "MPA Singapore", "article": "Circular 14/2024", "section": "Section 4.2" }
        ]
      }
    ]
  }
  ```
- **Status**: `READY`

---

### 5.3 `POST /api/v1/compliance/grounded-answer`
- **Role / UI**: `STAFF`, `CUSTOMS_OFFICER`
- **Purpose**: Conversational legal QA synthesizing verified regulatory knowledge chunks.
- **Permission**: Baseline read.
- **Request DTO**:
  ```json
  {
    "question": "What are the required import certificates for cold-chain frozen seafood into Japan?",
    "jurisdictionCode": "JP"
  }
  ```
- **Response DTO**:
  ```json
  {
    "answer": "Importing frozen seafood into Japan requires an Animal/Plant Quarantine Certificate and an accredited Health Certificate under MAFF Order No. 42...",
    "citations": [
      { "title": "MAFF Seafood Import Guidelines 2025", "page": "12", "section": "Article 3" }
    ],
    "confidence": 0.96
  }
  ```
- **Status**: `READY`

---

## 6. GPS Tracking & Telematics APIs (`Staff.Bff`)

### 6.1 `GET /api/v1/tracking/{id}/current` & `GET /api/v1/tracking/{id}/history`
- **Role / UI**: `STAFF`
- **Purpose**: Real-time GPS coordinate retrieval and route path breadcrumbs.
- **Permission**: `shipments:read` (Fallback: `gps_tracking:read`)
- **Query Parameters (`history`)**: `from` (ISO date), `to` (ISO date), `page`, `pageSize`.
- **Response DTO**:
  ```json
  {
    "shipmentId": "7fa85f64-...",
    "vehicleId": "TRK-VN-59A-12345",
    "latitude": 10.7769,
    "longitude": 106.7009,
    "speedKph": 45.2,
    "headingDegrees": 180.0,
    "recordedAt": "2026-08-27T08:45:10Z"
  }
  ```
- **Status**: `READY`

---

### 6.2 `POST /api/v1/tracking/geofences` & `PATCH /api/v1/tracking/geofences/{id}/active`
- **Role / UI**: `MANAGER`
- **Purpose**: Defines circular/polygon geofence boundaries around logistics hubs and ports.
- **Permission**: `gps_tracking:geofence:manage`
- **Request DTO (`POST`)**:
  ```json
  {
    "name": "Cat Lai Terminal 1 Entry Gate",
    "latitude": 10.7600,
    "longitude": 106.7900,
    "radiusMeters": 500.0,
    "shipmentId": "7fa85f64-..."
  }
  ```
- **Status**: `READY`

---

## 7. Financial & Billing APIs (`Staff.Bff`)

### 7.1 `POST /api/v1/financial/estimate-cost` & `POST /api/v1/financial/customs-duty`
- **Role / UI**: `STAFF` (`FINANCE_OFFICER`, `OPERATOR`)
- **Purpose**: Computes freight cost estimates and customs duties for shipment quotes.
- **Permission**: `financial_tax:calculate`
- **Request DTO (`customs-duty`)**:
  ```json
  {
    "originCountry": "VN",
    "destinationCountry": "US",
    "hsCode": "8507.60.00",
    "declaredValue": 50000.0,
    "currency": "USD"
  }
  ```
- **Response DTO**:
  ```json
  {
    "dutyRatePercent": 3.4,
    "estimatedDutyAmount": 1700.0,
    "vatPercent": 0.0,
    "totalTaxes": 1700.0,
    "currency": "USD"
  }
  ```
- **Status**: `READY`

---

### 7.2 `POST /api/v1/billing/invoices/generate` & `GET /api/v1/billing/invoices`
- **Role / UI**: `FINANCE_OFFICER`, `MANAGER`
- **Purpose**: Generates and lists customer freight invoices.
- **Permission**: `billing_settlement:invoice:create` / `billing_settlement:read`
- **Request DTO (`POST generate`)**:
  ```json
  {
    "shipmentId": "7fa85f64-...",
    "customerId": "CUST-VN-009",
    "currency": "USD",
    "issueDate": "2026-08-27T00:00:00Z",
    "dueDate": "2026-09-27T00:00:00Z"
  }
  ```
- **Response DTO**: `InvoiceResponse`
- **Status**: `READY`

---

### 7.3 `POST /api/v1/billing/escrow/release`
- **Role / UI**: `FINANCE_OFFICER`, `MANAGER`
- **Purpose**: Releases frozen escrow funds to carrier following POD verification.
- **Permission**: `billing_settlement:settlement:manage`
- **Request DTO**:
  ```json
  {
    "walletId": "wlt-8812-...",
    "transactionId": "tx-escrow-441",
    "amount": 3500.0,
    "currency": "USD",
    "note": "Proof of delivery confirmed by recipient"
  }
  ```
- **Response DTO**: `TransactionResponse`
- **Status**: `READY`

---

## 8. Customer Assistant & Chat APIs (`Staff.Bff` & NestJS)

### 8.1 `POST /api/v1/assistant/conversations` & `POST /api/v1/assistant/conversations/{id}/messages`
- **Role / UI**: `STAFF`, `CUSTOMER`
- **Purpose**: Multi-turn AI assistant with live tool execution (Tracking, Invoices, Regulations).
- **Permission**: Standard authenticated access.
- **Request DTO (`POST message`)**:
  ```json
  {
    "content": "Where is my shipment SHP-20260827-0042 right now and when will it arrive?"
  }
  ```
- **Response DTO**:
  ```json
  {
    "messageId": "msg-conv-9912",
    "role": "ASSISTANT",
    "content": "Your shipment SHP-20260827-0042 is currently In Transit near Vung Tau Port. Current ETA is August 29, 2026 at 14:00 SGT.",
    "toolsExecuted": ["shipment_lookup", "gps_current_location"]
  }
  ```
- **Status**: `READY`

---

## 9. Tenant Administration & IAM APIs (`Admin.Bff` & `System.Bff`)

### 9.1 `POST /api/v1/admin/staff` & `GET /api/v1/admin/staff`
- **Role / UI**: `TENANT_ADMIN`
- **Purpose**: Staff member onboarding, invitation, and paged listing.
- **Permission**: `iam:user:invite` (`POST`) / `iam:user:read` (`GET`)
- **Request DTO (`POST`)**:
  ```json
  {
    "email": "alex.nguyen@acme.com",
    "firstName": "Alex",
    "lastName": "Nguyen",
    "phoneNumber": "+84901234567",
    "role": "STAFF",
    "applyDefaultPermissions": true,
    "permissions": ["ocr:review"]
  }
  ```
- **Response DTO**: `UserResponse` (`id`, `email`, `role`, `permissions: []`, `status`, `permissionVersion`)
- **Status**: `READY`

---

### 9.2 `GET /api/v1/admin/staff/{id}` & `PUT /api/v1/admin/staff/{id}`
- **Role / UI**: `TENANT_ADMIN`
- **Purpose**: Gets or updates staff profile information (First/Last name).
- **Permission**: `iam:user:read` (`GET`) / `iam:user:update` (`PUT`)
- **Request DTO (`PUT`)**:
  ```json
  {
    "firstName": "Alex",
    "lastName": "Nguyen"
  }
  ```
- **Status**: `READY`

---

### 9.3 `PATCH /api/v1/admin/staff/{id}/role`
- **Role / UI**: `TENANT_ADMIN`
- **Purpose**: Updates a user's single Base Role (`STAFF` ↔ `MANAGER` ↔ `TENANT_ADMIN`). Preserves existing `UserPermissions` unless `applyDefaultPermissions: true` is explicitly passed (which performs a union with default template).
- **Permission**: `iam:role:manage`
- **Request DTO**:
  ```json
  {
    "role": "MANAGER",
    "applyDefaultPermissions": false
  }
  ```
- **Response DTO**:
  ```json
  {
    "userId": "9a3c7e81-...",
    "role": "MANAGER",
    "permissions": ["mail:read", "mail:send", "route_planning:read"],
    "permissionVersion": 5,
    "elevatedPermissionsRetained": []
  }
  ```
- **Status**: `READY`

---

### 9.4 `GET /api/v1/admin/staff/{id}/permissions`
- **Role / UI**: `TENANT_ADMIN`
- **Purpose**: Retrieves a user's active direct capability permissions.
- **Permission**: `iam:user:read`
- **Response DTO**:
  ```json
  {
    "userId": "9a3c7e81-...",
    "role": "STAFF",
    "permissions": [
      "mail:read",
      "mail:send",
      "route_planning:read",
      "route_planning:approve"
    ],
    "permissionVersion": 5
  }
  ```
- **Status**: `READY`

---

### 9.5 `PATCH /api/v1/admin/staff/{id}/permissions` (Single User Delta)
- **Role / UI**: `TENANT_ADMIN`
- **Purpose**: Atomically grants and/or revokes direct capability permissions for an individual user using delta semantics.
- **Permission**: `iam:permission:manage`
- **Request DTO**:
  ```json
  {
    "grant": [
      "route_planning:approve",
      "ocr:review"
    ],
    "revoke": [
      "route_planning:reject"
    ]
  }
  ```
- **Response DTO**: `UserPermissionsResponse` (`userId`, `role`, `permissions: []`, `permissionVersion`)
- **Status**: `READY`

---

### 9.6 `PATCH /api/v1/admin/staff/permissions` (Bulk Users Delta)
- **Role / UI**: `TENANT_ADMIN`
- **Purpose**: Atomically grants and/or revokes direct capability permissions for multiple selected users using delta semantics.
- **Permission**: `iam:permission:manage`
- **Request DTO**:
  ```json
  {
    "userIds": [
      "9a3c7e81-7788-4221-9988-112233445566",
      "b41c2299-1122-3344-5566-778899aabbcc"
    ],
    "grant": [
      "route_planning:approve"
    ],
    "revoke": []
  }
  ```
- **Response DTO**:
  ```json
  {
    "updatedUsersCount": 2,
    "affectedUserIds": [
      "9a3c7e81-7788-4221-9988-112233445566",
      "b41c2299-1122-3344-5566-778899aabbcc"
    ]
  }
  ```
- **Status**: `READY`

---

### 9.7 `GET /api/v1/admin/roles` & `GET /api/v1/admin/roles/{code}`
- **Role / UI**: `TENANT_ADMIN`
- **Purpose**: Lists canonical Base Roles (`STAFF`, `MANAGER`, `TENANT_ADMIN`) and their default permission template definitions.
- **Permission**: `iam:role:read`
- **Response DTO**:
  ```json
  {
    "items": [
      {
        "code": "STAFF",
        "name": "Tenant Staff",
        "description": "Standard tenant operational staff persona with baseline operational capabilities.",
        "defaultPermissions": ["mail:read", "mail:send", "shipments:read", "shipments:create", "route_planning:read", "financial_tax:read", "billing_settlement:read", "iam:user:read"]
      },
      {
        "code": "MANAGER",
        "name": "Operations Manager",
        "description": "Operations and risk supervisor persona with elevated review and approval capabilities.",
        "defaultPermissions": ["mail:read", "mail:thread:reassign", "route_planning:approve", "ocr:review", "compliance:override", "billing_settlement:settlement:manage", "iam:role:read"]
      },
      {
        "code": "TENANT_ADMIN",
        "name": "Tenant Administrator",
        "description": "Tenant administrator persona with full administrative permissions across tenant services.",
        "defaultPermissions": ["mail:domain:manage", "mail:mailbox:manage", "iam:user:invite", "iam:role:manage", "iam:permission:manage"]
      }
    ],
    "totalItems": 3
  }
  ```
- **Status**: `READY`

---

### 9.8 `POST /api/v1/system/tenants` & `GET /api/v1/system/tenants`
- **Role / UI**: `SYSTEM_ADMIN`
- **Purpose**: Platform super-admin tenant provisioning and suspension.
- **Permission**: `SYSTEM_ADMIN` role gate.
- **Request DTO (`POST`)**:
  ```json
  {
    "name": "Global Forwarding Corp",
    "tenantCode": "GFC",
    "adminEmail": "admin@gfc-logistics.com",
    "adminFirstName": "David",
    "adminLastName": "Miller",
    "planType": "ENTERPRISE",
    "companyDomain": "gfc-logistics.com"
  }
  ```
- **Status**: `READY`

---

## 10. Gaps: Backend Capabilities Missing in BFF

The following backend gRPC RPCs or administrative queries do not yet have dedicated REST controllers in the BFF layer:

1. **Permission Catalog Descriptions Endpoint**: `GET /api/v1/admin/permissions` (Listing all system capabilities with descriptions and modules for UI picker). Currently, the catalog is accessible through `GET /api/v1/admin/roles`.
   - **Report**: `BACKEND READY — BFF MISSING` (`GET /api/v1/admin/permissions`).
2. **Draft Risk Policy Lifecycle**: `RoutePlanningService.CreateRiskPolicyDraft`, `UpdateRiskPolicyDraft`, `SubmitRiskPolicyForReview`, `PublishRiskPolicy` are implemented in `RoutePlanningAgent`, but `Admin.Bff` currently only exposes `RuleConfigController`.
   - **Report**: `BACKEND READY — BFF MISSING` (`POST /api/v1/admin/risk-policies/draft`, `POST /api/v1/admin/risk-policies/{id}/publish`).
3. **Autonomous DevOps Incident Remediation**: `DevOpsIncidentService` and `DevOpsRuleService` in Java are operational via gRPC, but have no REST controllers in `System.Bff`.
   - **Report**: `BACKEND READY — BFF MISSING` (`/api/v1/system/devops/incidents`).

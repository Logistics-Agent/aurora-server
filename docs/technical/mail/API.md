# Aurora Mail Platform — Complete REST & gRPC API Specification

> **Status**: AUTHORITATIVE / FROZEN API CONTRACT (CODE-FIRST)  
> **Source-of-Truth**: Audited against `Staff.Bff.Controllers.MailController`, `Admin.Bff.Controllers.MailAdminController`, `System.Bff.Controllers.MailSystemController`, `NegotiationsController`, `mail_platform.proto`, and `PermissionConstants.cs`.

---

## 1. Quick Reference & Route Index

All endpoints are exposed through the YARP API Gateway with prefix `/api/v1/`:

| # | Method | Path | Required Permission | Description | Target BFF |
|:---:|:---:|---|---|---|---|
| **1** | `GET` | `/api/v1/mail/threads` | `mail:read` | List email conversation threads with scope filtering | `Staff.Bff` |
| **2** | `GET` | `/api/v1/mail/threads/{id}` | `mail:read` | Retrieve thread detail with messages and drafts | `Staff.Bff` |
| **3** | `POST` | `/api/v1/mail/threads/{id}/claim` | `mail:thread:claim` | Atomically claim an unassigned thread for current user | `Staff.Bff` |
| **4** | `POST` | `/api/v1/mail/threads/{id}/reassign` | `mail:thread:reassign` | Supervisory reassignment of thread to another staff member | `Staff.Bff` |
| **5** | `POST` | `/api/v1/mail/threads/{id}/unassign` | `mail:thread:unassign` | Supervisory release of thread back to unassigned queue | `Staff.Bff` |
| **6** | `GET` | `/api/v1/mail/threads/{id}/assignment-history` | `mail:read` | Immutable historical log of thread assignment events | `Staff.Bff` |
| **7** | `POST` | `/api/v1/mail/drafts` | `mail:draft:create` | Create a new draft message or revision | `Staff.Bff` |
| **8** | `GET` | `/api/v1/mail/drafts` | `mail:read` | List and filter draft messages | `Staff.Bff` |
| **9** | `GET` | `/api/v1/mail/drafts/{id}` | `mail:read` | Get specific draft details | `Staff.Bff` |
| **10** | `POST` | `/api/v1/mail/messages/outbound` | `mail:send` | Submit outbound email for pipeline processing & SMTP relay | `Staff.Bff` |
| **11** | `GET` | `/api/v1/mail/messages` | `mail:read` | List processed inbound/outbound emails | `Staff.Bff` |
| **12** | `GET` | `/api/v1/mail/messages/{id}` | `mail:read` | Get processed email metadata, body, and security checks | `Staff.Bff` |
| **13** | `GET` | `/api/v1/mail/quarantine` | `mail:quarantine:read` | List quarantined security threats | `Staff.Bff` |
| **14** | `GET` | `/api/v1/mail/quarantine/{id}` | `mail:quarantine:read` | Get quarantine threat details and scoring analysis | `Staff.Bff` |
| **15** | `POST` | `/api/v1/mail/quarantine/{id}/release` | `mail:quarantine:release` | Release safe email from quarantine into mailbox | `Staff.Bff` |
| **16** | `POST` | `/api/v1/negotiations/{id}/mail-draft` | `mail:draft:create` | Human-in-the-loop: convert AI negotiation suggestion to draft | `Staff.Bff` |
| **17** | `POST` | `/api/v1/admin/mail/domains` | `mail:domain:manage` | Provision email domain and generate DKIM keys | `Admin.Bff` |
| **18** | `POST` | `/api/v1/admin/mail/mailboxes` | `mail:mailbox:manage` | Create shared department mailbox | `Admin.Bff` |
| **19** | `POST` | `/api/v1/admin/mail/aliases` | `mail:mailbox:manage` | Create forwarding email alias | `Admin.Bff` |
| **20** | `POST` | `/api/v1/admin/mail/mailboxes/{id}/reset-password` | `mail:mailbox:manage` | Reset mailbox account credentials | `Admin.Bff` |
| **21** | `DELETE`| `/api/v1/admin/mail/quarantine/{id}` | `mail:quarantine:delete` | Permanently purge quarantined record | `Admin.Bff` |
| **22** | `GET` | `/api/v1/admin/mail/audit` | `mail:audit:read` | Tenant mail security audit log | `Admin.Bff` |
| **23** | `POST` | `/api/v1/system/mail/dead-letter/{id}/requeue` | `mail:system:manage` | Requeue dead-lettered message for reprocessing | `System.Bff` |
| **24** | `GET` | `/api/v1/system/mail/audit` | `mail:system:manage` | Global platform-wide audit log | `System.Bff` |

---

## 2. Detailed Staff & Operational Endpoints (`Staff.Bff`)

### 2.1 `GET /api/v1/mail/threads`
- **Permission**: `mail:read`
- **Query Parameters**:
  - `scope` (string, optional, default: `MY_WORK`): `UNASSIGNED` | `MY_WORK` | `ALL` *(Note: `ALL` requires `mail:thread:read_all`)*
  - `status` (string, optional): `UNASSIGNED` | `IN_PROGRESS` | `WAITING_CUSTOMER` | `RESOLVED`
  - `mailboxId` (UUID string, optional): Filter by shared mailbox
  - `pageSize` (int, default: 20, max: 100)
  - `pageToken` (string, optional)
- **Response (200 OK)**:
  ```json
  {
    "threads": [
      {
        "threadId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "tenantId": "e5b8ba84-0000-0000-0000-000000000001",
        "mailboxId": "8ba12345-0000-0000-0000-000000000001",
        "subject": "Urgent Booking Request - 2x40HC Ho Chi Minh to Rotterdam",
        "snippet": "Dear Aurora Logistics team, please provide a freight quote...",
        "participants": ["shipper@clientcorp.com", "ops@acmelogistics.com"],
        "primaryAssigneeUserId": "9a3c7e81-7788-4221-9988-112233445566",
        "assignedStaffName": "Nguyen Van A",
        "status": "IN_PROGRESS",
        "priority": "HIGH",
        "messageCount": 3,
        "draftCount": 1,
        "hasUnread": false,
        "lastMessageAt": "2026-08-28T07:15:00Z"
      }
    ],
    "nextPageToken": "eyJvZmZzZXQiOjIwfQ==",
    "totalCount": 42
  }
  ```

---

### 2.2 `POST /api/v1/mail/threads/{id}/claim`
- **Permission**: `mail:thread:claim`
- **Purpose**: Atomically claims an unassigned thread for the authenticated staff user.
- **Request Body**: None
- **Response (200 OK)**:
  ```json
  {
    "success": true,
    "threadId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "primaryAssigneeUserId": "9a3c7e81-7788-4221-9988-112233445566",
    "assignedAt": "2026-08-28T07:20:00Z",
    "status": "IN_PROGRESS"
  }
  ```
- **Error Responses**:
  - `409 Conflict`: Thread has already been claimed by another staff member (`THREAD_ALREADY_ASSIGNED`).
  - `404 Not Found`: Thread ID does not exist in current tenant.

---

### 2.3 `POST /api/v1/mail/threads/{id}/reassign`
- **Permission**: `mail:thread:reassign`
- **Purpose**: Supervisor reassigns thread responsibility to a target staff member.
- **Request Body**:
  ```json
  {
    "newAssigneeUserId": "b41c2299-1122-3344-5566-778899aabbcc",
    "reason": "Rebalancing workload for European freight desk."
  }
  ```
- **Response (200 OK)**:
  ```json
  {
    "success": true,
    "threadId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "previousAssigneeUserId": "9a3c7e81-7788-4221-9988-112233445566",
    "newAssigneeUserId": "b41c2299-1122-3344-5566-778899aabbcc",
    "status": "IN_PROGRESS"
  }
  ```

---

### 2.4 `POST /api/v1/mail/threads/{id}/unassign`
- **Permission**: `mail:thread:unassign`
- **Purpose**: Supervisor releases thread back to the `UNASSIGNED` queue.
- **Request Body**:
  ```json
  {
    "reason": "Staff member on sick leave; returning to shared triage queue."
  }
  ```
- **Response (200 OK)**:
  ```json
  {
    "success": true,
    "threadId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "status": "UNASSIGNED"
  }
  ```

---

### 2.5 `GET /api/v1/mail/threads/{id}/assignment-history`
- **Permission**: `mail:read`
- **Response (200 OK)**:
  ```json
  {
    "threadId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "histories": [
      {
        "id": "hist-001-...",
        "action": "CLAIMED",
        "fromUserId": null,
        "toUserId": "9a3c7e81-...",
        "actorUserId": "9a3c7e81-...",
        "reason": "Staff explicitly claimed unassigned thread",
        "createdAt": "2026-08-28T07:20:00Z"
      },
      {
        "id": "hist-002-...",
        "action": "REASSIGNED",
        "fromUserId": "9a3c7e81-...",
        "toUserId": "b41c2299-...",
        "actorUserId": "manager-4421-...",
        "reason": "Rebalancing workload for European freight desk.",
        "createdAt": "2026-08-28T08:00:00Z"
      }
    ]
  }
  ```

---

### 2.6 `POST /api/v1/mail/drafts`
- **Permission**: `mail:draft:create`
- **Request Body**:
  ```json
  {
    "mailboxId": "8ba12345-0000-0000-0000-000000000001",
    "threadId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "replyToMessageId": "msg-9901-...",
    "to": ["shipper@clientcorp.com"],
    "subject": "Re: Urgent Booking Request - 2x40HC Ho Chi Minh to Rotterdam",
    "body": "Dear Shipper, we have secured space on Vessel CMA CGM Marco Polo...",
    "idempotencyKey": "draft-uniq-1092"
  }
  ```
- **Response (201 Created)**:
  ```json
  {
    "draftId": "draft-uuid-7711",
    "draftRootId": "draft-root-7711",
    "revisionNumber": 1,
    "status": "DRAFT",
    "contentHash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    "isExisting": false
  }
  ```

---

### 2.7 `POST /api/v1/mail/messages/outbound`
- **Permission**: `mail:send`
- **Request Body**:
  ```json
  {
    "senderAddress": "ops@acmelogistics.com",
    "recipientAddresses": ["shipper@clientcorp.com"],
    "subject": "Re: Urgent Booking Request - 2x40HC Ho Chi Minh to Rotterdam",
    "bodyText": "Dear Shipper, we have secured space on Vessel CMA CGM Marco Polo...",
    "bodyHtml": "<p>Dear Shipper, we have secured space on <strong>Vessel CMA CGM Marco Polo</strong>...</p>",
    "draftRootId": "draft-root-7711",
    "threadId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "replyToMessageId": "msg-9901-...",
    "idempotencyKey": "send-idem-8812"
  }
  ```
- **Response (200 OK)**:
  ```json
  {
    "messageId": "msg-outbound-4412-...",
    "direction": "OUTBOUND",
    "status": "DELIVERED",
    "stalwartQueueId": "q-stalwart-9921",
    "processedAt": "2026-08-28T08:30:00Z"
  }
  ```

---

### 2.8 `POST /api/v1/negotiations/{id}/mail-draft`
- **Permission**: `mail:draft:create`
- **Purpose**: Staff converts an AI-generated negotiation proposal into an immutable, threaded `EmailDraft`.
- **Request Body**:
  ```json
  {
    "mailboxId": "8ba12345-0000-0000-0000-000000000001",
    "idempotencyKey": "neg-draft-session-4401"
  }
  ```
- **Response (201 Created)**:
  ```json
  {
    "draftId": "draft-neg-7788",
    "threadId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "subject": "Re: Rate Counter-Offer - Freight Route SG-VN",
    "body": "Thank you for your proposal. We can accommodate your shipment at $1,380 USD...",
    "sourceType": "NEGOTIATION",
    "sourceId": "neg-session-4401",
    "status": "DRAFT"
  }
  ```

---

## 3. Detailed Tenant Admin Endpoints (`Admin.Bff`)

### 3.1 `POST /api/v1/admin/mail/domains`
- **Permission**: `mail:domain:manage`
- **Request Body**:
  ```json
  {
    "domainName": "acmelogistics.com",
    "maxMailboxCount": 50,
    "retentionDays": 365
  }
  ```
- **Response (201 Created)**:
  ```json
  {
    "domainId": "dom-9912-...",
    "domainName": "acmelogistics.com",
    "dkimSelector": "aurora-2025",
    "dkimTxtRecord": "v=DKIM1; k=rsa; p=MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA...",
    "spfTxtRecord": "v=spf1 include:relay.aurora-logistics.com ~all",
    "dmarcTxtRecord": "v=DMARC1; p=quarantine; rua=mailto:dmarc-reports@acmelogistics.com",
    "status": "ACTIVE"
  }
  ```

---

### 3.2 `POST /api/v1/admin/mail/mailboxes`
- **Permission**: `mail:mailbox:manage`
- **Request Body**:
  ```json
  {
    "domainId": "dom-9912-...",
    "localPart": "ops",
    "initialPassword": "StrongPassword123!"
  }
  ```
- **Response (201 Created)**:
  ```json
  {
    "mailboxId": "8ba12345-...",
    "fullAddress": "ops@acmelogistics.com",
    "status": "ACTIVE"
  }
  ```

---

## 4. Detailed System Admin Endpoints (`System.Bff`)

### 4.1 `POST /api/v1/system/mail/dead-letter/{id}/requeue`
- **Permission**: `mail:system:manage`
- **Purpose**: System Admin re-submits a permanently failed/dead-lettered email message back to the pipeline runner.
- **Response (200 OK)**:
  ```json
  {
    "success": true,
    "processedMessageId": "3fa85f64-...",
    "status": "PENDING",
    "requeuedAt": "2026-08-28T09:00:00Z"
  }
  ```

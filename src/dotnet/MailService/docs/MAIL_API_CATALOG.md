# Aurora Mail Platform — Consolidated Mail API Catalog

> [!WARNING]
> **DEPRECATED / SUPERSEDED DOCUMENT**  
> This document is retained for historical reference only. The authoritative, code-audited API specification is located at [docs/technical/mail/API.md](file:///D:/IT/CD/aurora-server/docs/technical/mail/API.md) and [docs/technical/mail/OVERVIEW.md](file:///D:/IT/CD/aurora-server/docs/technical/mail/OVERVIEW.md).

**Version:** 1.1.0 (Historical Archive)  

---

## 1. API Summary Table

| # | HTTP Method | Route | Business Function | Permission / Gate | MailService gRPC RPC | Internal Dependencies |
|:---:|:---:|---|---|---|---|---|
| 1 | `POST` | `/api/v1/admin/mail/domains` | Provision mail domain with DKIM keys | `mail:create` (`TENANT_ADMIN`) | `MailManagement.ProvisionDomain` | Neon DB, Stalwart JMAP |
| 2 | `POST` | `/api/v1/admin/mail/mailboxes` | Create tenant mailbox account | `mail:create` (`TENANT_ADMIN`) | `MailManagement.CreateMailbox` | Neon DB, Stalwart JMAP |
| 3 | `POST` | `/api/v1/admin/mail/aliases` | Create forwarding email alias | `mail:create` (`TENANT_ADMIN`) | `MailManagement.CreateAlias` | Neon DB, Stalwart JMAP |
| 4 | `POST` | `/api/v1/admin/mail/mailboxes/{id}/reset-password` | Request mailbox credential reset | `mail:update` (`TENANT_ADMIN`) | `MailManagement.ResetPassword` | Neon DB, Cognito IAM |
| 5 | `POST` | `/api/v1/mail/drafts` | Create immutable draft email (Rev 1) | `mail:create` (Staff / Admin / AI Agent) | `MailSecurity.CreateDraftMessage` | Neon DB |
| 6 | `GET` | `/api/v1/mail/drafts` | Query and filter email drafts | `mail:read` (Staff / Admin / AI Agent) | `MailSecurity.ListDrafts` | Neon DB |
| 7 | `GET` | `/api/v1/mail/drafts/{id}` | Retrieve specific draft by ID | `mail:read` (Staff / Admin / AI Agent) | `MailSecurity.GetDraft` | Neon DB |
| 8 | `POST` | `/api/v1/mail/messages/outbound` | Submit outbound email (Sync processing) | `mail:send` (Staff / Admin) | `MailSecurity.SubmitOutboundMessage` | Neon DB, ClamAV, AI Governance, Redis, Stalwart SMTP, Outbox |
| 9 | `GET` | `/api/v1/mail/messages` | List inbound/outbound processed emails | `mail:read` (Staff / Admin) | `MailSecurity.ListProcessedMessages` | Neon DB |
| 10 | `GET` | `/api/v1/mail/messages/{id}` | Get processed email and security checks | `mail:read` (Staff / Admin) | `MailSecurity.GetProcessedMessage` | Neon DB |
| 11 | `GET` | `/api/v1/mail/quarantine` | Query quarantined security threats | `mail:read` (Staff / Admin) | `MailSecurity.ListQuarantineRecords` | Neon DB |
| 12 | `GET` | `/api/v1/mail/quarantine/{id}` | Get quarantine threat details | `mail:read` (Staff / Admin) | `MailSecurity.GetQuarantineRecord` | Neon DB |
| 13 | `POST` | `/api/v1/mail/quarantine/{id}/release` | Release safe message from quarantine | `mail:release` (Staff / Admin) | `MailSecurity.ReleaseQuarantine` | Neon DB, Cloudflare R2, Stalwart JMAP, Audit |
| 14 | `DELETE`| `/api/v1/admin/mail/quarantine/{id}` | Permanently purge quarantined record | `mail:delete` (`TENANT_ADMIN`) | `MailSecurity.DeleteQuarantine` | Neon DB, Audit |
| 15 | `GET` | `/api/v1/admin/mail/audit` | Query tenant mail security audit logs | `mail:read` (`TENANT_ADMIN`) | `MailManagement.GetAuditRecords` | Neon DB |
| 16 | `POST` | `/api/v1/system/mail/dead-letter/{id}/requeue` | Replay dead-lettered message to queue | `SYSTEM_ADMIN` (Role Gate) | `MailManagement.RequeueDeadLetter` | Neon DB, Outbox, RabbitMQ |
| 17 | `GET` | `/api/v1/system/mail/audit` | System-wide mail audit trail | `SYSTEM_ADMIN` (Role Gate) | `MailManagement.GetAuditRecords` | Neon DB |
| 18 | `GET` | `/api/v1/mail/threads` | List Gmail-like conversation threads | `mail:read` (Staff / Admin) | `MailSecurity.ListThreads` | Neon DB |
| 19 | `GET` | `/api/v1/mail/threads/{id}` | Get thread detail with messages & drafts | `mail:read` (Staff / Admin) | `MailSecurity.GetThread` | Neon DB |
| 20 | `POST` | `/api/v1/negotiations/{id}/mail-draft` | Create threaded draft from Negotiation Suggestion | `mail:create` (Staff / Admin) | `Negotiation.GetDraftSuggestion` + `MailSecurity.CreateDraftMessage` | Neon DB, Negotiation gRPC |

---

## 2. Functional Grouping

1. **Domain Management** (Tenant Admin)
2. **Mailbox Management** (Tenant Admin)
3. **Alias Management** (Tenant Admin)
4. **Draft Management** (Staff, Admin, AI Agent)
5. **Thread Management (Gmail-Like Threading)** (Staff & Admin)
6. **Negotiation AI Human-in-the-Loop Draft Flow** (Staff & Admin)
7. **Outbound Mail Delivery** (Staff & Admin)
8. **Processed Message History** (Staff & Admin)
9. **Quarantine & Threat Review** (Staff & Admin)
10. **Audit Trail** (Admin & System Admin)
11. **Operations & Dead Letter Requeue** (System Admin)

---

## 3. Detailed API Specifications

### Group 1: Domain Management

#### 3.1 Provision Mail Domain
- **Function (Mô tả):** Khởi tạo tên miền email cho Tenant trên hạ tầng Stalwart Mail Server, đồng thời sinh cặp khóa ký số DKIM (RSA-2048) và lưu trữ cấu hình bảo mật vào cơ sở dữ liệu.
- **HTTP:**
  - `Method:` `POST`
  - `Path:` `/api/v1/admin/mail/domains`
  - `Content-Type:` `application/json`
- **MailService RPC:** `MailManagement.ProvisionDomain` (`protos/mail_platform.proto`)
- **Authorization:**
  - Required Permission: `mail:create`
  - Allowed Roles: `TENANT_ADMIN`
  - Scope: Tenant-scoped (Tenant ID trích xuất từ JWT metadata).
- **Request Example:**
  ```json
  {
    "domainName": "aurora.vn",
    "maxMailboxCount": 100,
    "retentionDays": 365
  }
  ```
- **Response Example:**
  ```json
  {
    "domainId": "01917b20-6d30-74e5-9c88-123456789abc",
    "domainName": "aurora.vn",
    "dkimSelector": "aurora-2025",
    "dkimTxtRecord": "v=DKIM1; k=rsa; p=MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA...",
    "provisionedAt": "2026-08-22T04:30:00Z"
  }
  ```
- **Validation Rules:**
  - `domainName`: Bắt buộc, định dạng FQDN hợp lệ, tối đa 253 ký tự.
  - `maxMailboxCount`: `1` – `10,000`.
  - `retentionDays`: `1` – `3,650` ngày.
- **Side Effects:**
  - Neon DB: Tạo bản ghi `MailDomain`.
  - Stalwart: Gọi JMAP REST API tạo domain entity.
- **Events:** `None`
- **AI Governance:** `No`
- **Error Responses:** `400 Bad Request`, `401 Unauthorized`, `403 Forbidden`, `409 Conflict`, `503 Service Unavailable`.
- **Source References:**
  - BFF Endpoint: [`src/dotnet/BFF/Admin.Bff/Controllers/MailAdminController.cs`](file:///d:/IT/CD/aurora-server/src/dotnet/BFF/Admin.Bff/Controllers/MailAdminController.cs)
  - BFF Client: [`src/dotnet/BFF/BuildingBlocks.BFF/Mail/Clients/GrpcMailServiceClient.cs`](file:///d:/IT/CD/aurora-server/src/dotnet/BFF/BuildingBlocks.BFF/Mail/Clients/GrpcMailServiceClient.cs)
  - Handler: [`src/dotnet/MailService/Application/Commands/Provisioning/ProvisionDomainCommand.cs`](file:///d:/IT/CD/aurora-server/src/dotnet/MailService/Application/Commands/Provisioning/ProvisionDomainCommand.cs)

---

### Group 2: Mailbox Management

#### 3.2 Create Mailbox
- **Function (Mô tả):** Cấp phát thủ công hộp thư phòng ban/dùng chung (Shared Mailbox như `support@`, `sales@`) hoặc phục hồi/sửa chữa cấu hình hộp thư. **Lưu ý:** Hộp thư cá nhân cho Tenant Admin và Nhân viên (Staff) được **tự động khởi tạo theo mô hình hướng sự kiện (Event-Driven Auto-Provisioning)** qua RabbitMQ mà không cần Frontend gọi API này. Chi tiết xem tại [`MAILBOX_PROVISIONING_FLOW.md`](file:///d:/IT/CD/aurora-server/src/dotnet/MailService/docs/MAILBOX_PROVISIONING_FLOW.md).
- **HTTP:**
  - `Method:` `POST`
  - `Path:` `/api/v1/admin/mail/mailboxes`
  - `Content-Type:` `application/json`
- **MailService RPC:** `MailManagement.CreateMailbox`
- **Authorization:** `mail:create` (`TENANT_ADMIN`).
- **Request Example:**
  ```json
  {
    "domainId": "01917b20-6d30-74e5-9c88-123456789abc",
    "localPart": "dispatch",
    "userId": "01917b20-6d30-74e5-9c88-a1b2c3d4e5f6"
  }
  ```
- **Response Example:**
  ```json
  {
    "mailboxId": "01917b20-6d30-74e5-9c88-f7a8b9c0d1e2",
    "fullAddress": "dispatch@aurora.vn",
    "createdAt": "2026-08-22T04:31:00Z"
  }
  ```
- **Validation Rules:**
  - `domainId`: Bắt buộc, GUID hợp lệ.
  - `localPart`: Bắt buộc, 1–64 ký tự chữ cái/số/ký tự hợp lệ của email.
  - `userId`: Không bắt buộc; nếu có phải là GUID (để trống khi tạo Shared Mailbox phòng ban).
- **Side Effects:** Tạo bản ghi `Mailbox` trong DB, ghi nhận `AuditRecord`, và đồng bộ/cấp phát tài khoản trên Stalwart.
- **Events:** `None` (Đồng bộ trực tiếp; quy trình tự động dùng `TenantStaffCreatedEvent`/`TenantAdminCreatedEvent`).
- **AI Governance:** `No`
- **Error Responses:** `400 Bad Request`, `401 Unauthorized`, `403 Forbidden`, `404 Not Found`, `409 Conflict`.

#### 3.3 Reset Mailbox Password
- **Function (Mô tả):** Yêu cầu reset thông tin xác thực mailbox. Mật khẩu được ủy quyền qua Cognito OIDC.
- **HTTP:**
  - `Method:` `POST`
  - `Path:` `/api/v1/admin/mail/mailboxes/{id}/reset-password`
- **MailService RPC:** `MailManagement.ResetPassword`
- **Authorization:** `mail:update` (`TENANT_ADMIN`).
- **Response Example:**
  ```json
  {
    "acknowledged": true,
    "message": "Mailbox password reset acknowledged. Authentication is delegated to Cognito OIDC."
  }
  ```
- **Error Responses:** `401 Unauthorized`, `403 Forbidden`, `404 Not Found`.

---

### Group 3: Alias Management

#### 3.4 Create Email Alias
- **Function (Mô tả):** Tạo địa chỉ email alias chuyển tiếp (forwarding alias) tới một hoặc nhiều hộp thư đích.
- **HTTP:**
  - `Method:` `POST`
  - `Path:` `/api/v1/admin/mail/aliases`
  - `Content-Type:` `application/json`
- **MailService RPC:** `MailManagement.CreateAlias`
- **Authorization:** `mail:create` (`TENANT_ADMIN`).
- **Request Example:**
  ```json
  {
    "domainId": "01917b20-6d30-74e5-9c88-123456789abc",
    "aliasAddress": "support@aurora.vn",
    "targetAddresses": [
      "staff1@aurora.vn",
      "staff2@aurora.vn"
    ]
  }
  ```
- **Response Example:**
  ```json
  {
    "aliasId": "01917b20-6d30-74e5-9c88-c9d8e7f6a5b4",
    "createdAt": "2026-08-22T04:32:00Z"
  }
  ```
- **Validation Rules:**
  - `domainId`: Bắt buộc, GUID.
  - `aliasAddress`: Bắt buộc, định dạng email hợp lệ.
  - `targetAddresses`: Bắt buộc, danh sách từ 1–20 email hợp lệ.
- **Side Effects:** Lưu `MailAlias` vào Neon DB và cấu hình alias trên Stalwart server.

---

### Group 4: Draft Management

#### 3.5 Create Draft Message
- **Function (Mô tả):** Khởi tạo bản nháp email ban đầu (Revision #1). Áp dụng tính bất biến (Immutable Revision Pattern) — mỗi lần chỉnh sửa tạo một Revision mới có băm nội dung `ContentHash`. AI Agent được phép tạo bản nháp trong tenant context để Staff review.
- **HTTP:**
  - `Method:` `POST`
  - `Path:` `/api/v1/mail/drafts`
  - `Content-Type:` `application/json`
- **MailService RPC:** `MailSecurity.CreateDraftMessage`
- **Authorization:** `mail:create` (Staff, Tenant Admin, AI Agent).
- **Request Example:**
  ```json
  {
    "mailboxId": "01917b20-6d30-74e5-9c88-f7a8b9c0d1e2",
    "assignedStaffId": "01917b20-6d30-74e5-9c88-a1b2c3d4e5f6",
    "subject": "Báo giá dịch vụ vận tải tuyến Bắc Nam",
    "body": "Kính gửi Quý khách hàng, Aurora Logistics xin gửi báo giá chi tiết..."
  }
  ```
- **Response Example:**
  ```json
  {
    "draftId": "01917b20-6d30-74e5-9c88-b1c2d3e4f5a6",
    "draftRootId": "01917b20-6d30-74e5-9c88-b1c2d3e4f5a6",
    "revisionNumber": 1,
    "isLatestRevision": true,
    "source": "Manual",
    "status": "Draft",
    "mailboxId": "01917b20-6d30-74e5-9c88-f7a8b9c0d1e2",
    "assignedStaffId": "01917b20-6d30-74e5-9c88-a1b2c3d4e5f6",
    "subject": "Báo giá dịch vụ vận tải tuyến Bắc Nam",
    "body": "Kính gửi Quý khách hàng, Aurora Logistics xin gửi báo giá chi tiết...",
    "contentHash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    "createdAt": "2026-08-22T04:33:00Z"
  }
  ```
- **Side Effects:** Lưu `EmailDraft` vào DB.
- **AI Governance:** `No`

#### 3.6 List Drafts
- **HTTP:** `GET /api/v1/mail/drafts?mailboxId={id}&status={status}&pageSize=20&pageToken={token}`
- **MailService RPC:** `MailSecurity.ListDrafts`
- **Authorization:** `mail:read` (Staff / Admin / AI Agent).

#### 3.7 Get Draft
- **HTTP:** `GET /api/v1/mail/drafts/{id}`
- **MailService RPC:** `MailSecurity.GetDraft`
- **Authorization:** `mail:read` (Staff / Admin / AI Agent).

---

### Group 5: Outbound Mail Delivery

#### 3.8 Submit Outbound Message
- **Function (Mô tả):** Gửi email outbound từ Tenant ra Internet qua quy trình kiểm duyệt an ninh đồng bộ (Synchronous Pipeline):
  1. Draft revision validation (kiểm tra trạng thái bản nháp nếu gửi từ Draft).
  2. ClamAV streaming scanning các tệp đính kèm.
  3. Domain Policy & Rate-Limiting validation.
  4. AI Governance BEC / phishing risk scoring (Business Email Compromise).
  5. MailKit SMTP submission tới Stalwart Mail Server (cổng 587 STARTTLS / submission).
  6. Atomic commit: Lưu `ProcessedMessage`, cập nhật `EmailDraft` sang `Sent`, ghi `OutboundEmailSentEvent` vào Outbox table.
- **HTTP:**
  - `Method:` `POST`
  - `Path:` `/api/v1/mail/messages/outbound`
  - `Content-Type:` `application/json`
- **MailService RPC:** `MailSecurity.SubmitOutboundMessage`
- **Authorization:**
  - Required Permission: `mail:send`
  - Allowed Roles: `STAFF`, `TENANT_ADMIN`
  - **Restriction:** AI Agent **không được phép** trực tiếp gọi Submit Outbound Message (phải qua phê duyệt Staff).
- **Request Example:**
  ```json
  {
    "senderAddress": "sales@aurora.vn",
    "recipientAddresses": [
      "partner@example.com"
    ],
    "subject": "Hợp đồng nguyên tắc vận chuyển 2026",
    "bodyText": "Kính gửi Quý đối tác, xin gửi hợp đồng kèm theo...",
    "bodyHtml": "<p>Kính gửi Quý đối tác, xin gửi hợp đồng kèm theo...</p>",
    "attachments": [
      {
        "filename": "hop_dong_nguyen_tac.pdf",
        "contentType": "application/pdf",
        "contentBase64": "JVBERi0xLjQKJ..."
      }
    ],
    "idempotencyKey": "idem-outbound-991823",
    "draftRootId": "01917b20-6d30-74e5-9c88-b1c2d3e4f5a6"
  }
  ```
- **Response Example (200 OK):**
  ```json
  {
    "processedMessageId": "01917b20-6d30-74e5-9c88-a9b8c7d6e5f4",
    "stalwartQueueId": "STALWART-QUEUE-882194",
    "submittedAt": "2026-08-22T04:35:00Z"
  }
  ```
- **Payload Limits:**
  - `MaxAttachmentCount`: **10** files.
  - `MaxSingleAttachmentBytes`: **25 MB** (Decoded).
  - `MaxTotalAttachmentBytes`: **50 MB** (Decoded total).
  - `MaxHttpRequestBodyBytes`: **80 MB** (BFF Kestrel).
  - `MaxGrpcMessageBytes`: **75 MB** (gRPC channel).
- **Side Effects:**
  - Neon DB: Lưu `ProcessedMessage`, cập nhật `EmailDraft`, ghi Outbox.
  - Stalwart: Gửi SMTP mail submission.
  - Redis: Tăng counter rate-limit.
  - AI Governance: Phân tích BEC risk.
  - RabbitMQ: Xuất bản `OutboundEmailSentEvent`.
- **Events:**
  - Thành công: `OutboundEmailSentEvent`
  - Bị chặn chính sách/malware: `OutboundEmailRejectedEvent`
- **AI Governance:**
  - **Yes** (`Generate` / Policy analysis) — Đánh giá nguy cơ BEC và rò rỉ dữ liệu.
- **Future Roadmap Note:**
  - MVP sử dụng Base64 transport qua BFF/gRPC với giới hạn dung lượng chặt chẽ.
  - Tương lai sẽ hỗ trợ Presigned Cloudflare R2 direct upload + attachment reference.
- **Error Responses:** `400 Bad Request`, `401 Unauthorized`, `403 Forbidden` (Bị chặn do malware/BEC), `422 Unprocessable Entity` (Bản nháp đã gửi trước đó), `429 Too Many Requests`, `503 Service Unavailable`, `504 Gateway Timeout`.

---

### Group 6: Processed Messages History

#### 3.9 List Processed Messages
- **HTTP:** `GET /api/v1/mail/messages?direction=Inbound&emailCategory=Commercial&pipelineStatus=Clean&pageSize=20&pageToken={token}`
- **MailService RPC:** `MailSecurity.ListProcessedMessages`
- **Authorization:** `mail:read` (Staff / Admin).
- **Response Example:**
  ```json
  {
    "messages": [
      {
        "processedMessageId": "01917b20-6d30-74e5-9c88-a9b8c7d6e5f4",
        "messageId": "<202608220435.MSG1234@aurora.vn>",
        "direction": "Outbound",
        "senderAddress": "sales@aurora.vn",
        "recipientAddresses": ["partner@example.com"],
        "subject": "Hợp đồng nguyên tắc vận chuyển 2026",
        "receivedAt": "2026-08-22T04:35:00Z",
        "processedAt": "2026-08-22T04:35:02Z",
        "emailCategory": "Commercial",
        "pipelineStatus": "Clean",
        "spamScore": 0.1,
        "phishingScore": 0.0,
        "isQuarantined": false,
        "r2RawEmlPath": "eml/2026/08/22/msg1234.eml",
        "securityChecks": [
          {
            "stage": "ClamAV",
            "result": "Clean",
            "detailJson": "{\"viruses\":[]}",
            "durationMs": 14
          }
        ]
      }
    ],
    "nextPageToken": null
  }
  ```

#### 3.10 Get Processed Message
- **HTTP:** `GET /api/v1/mail/messages/{id}`
- **MailService RPC:** `MailSecurity.GetProcessedMessage`
- **Authorization:** `mail:read` (Staff / Admin).

---

### Group 7: Quarantine & Threat Review

#### 3.11 List Quarantine Records
- **HTTP:** `GET /api/v1/mail/quarantine?status=Quarantined&pageSize=20&pageToken={token}`
- **MailService RPC:** `MailSecurity.ListQuarantineRecords`
- **Authorization:** `mail:read` (Staff / Admin).

#### 3.12 Get Quarantine Record
- **HTTP:** `GET /api/v1/mail/quarantine/{id}`
- **MailService RPC:** `MailSecurity.GetQuarantineRecord`
- **Authorization:** `mail:read` (Staff / Admin).

#### 3.13 Release Quarantine Message
- **Function (Mô tả):** Giải phóng email an toàn khỏi khu vực cách ly (Quarantine). Đối với email bị cách ly do Spam/Phishing sai (False Positive), hệ thống lấy file EML gốc từ Cloudflare R2 và đẩy lại vào hòm thư người nhận trên Stalwart. (Lưu ý: Malware bị cấm release trực tiếp trừ khi có quyền ghi đè bảo mật cấp cao).
- **HTTP:**
  - `Method:` `POST`
  - `Path:` `/api/v1/mail/quarantine/{id}/release`
- **MailService RPC:** `MailSecurity.ReleaseQuarantine`
- **Authorization:** `mail:release` (Staff / Admin).
- **Response Example:**
  ```json
  {
    "success": true,
    "releasedAt": "2026-08-22T04:40:00Z"
  }
  ```
- **Side Effects:**
  - R2: Đọc file EML.
  - Stalwart: Nạp email vào inbox người nhận.
  - DB: Cập nhật `QuarantineRecord.Status = 'Released'`.
  - Audit: Ghi nhận vết kiểm tra an ninh.

#### 3.14 Delete Quarantine Record
- **HTTP:** `DELETE /api/v1/admin/mail/quarantine/{id}`
- **MailService RPC:** `MailSecurity.DeleteQuarantine`
- **Authorization:** `mail:delete` (`TENANT_ADMIN`).
- **Response Example:**
  ```json
  {
    "success": true
  }
  ```

---

### Group 8: Audit Trail

#### 3.15 Get Audit Records (Tenant Admin)
- **HTTP:** `GET /api/v1/admin/mail/audit?resourceType=MailDomain&resourceId={id}&pageSize=50&pageToken={token}`
- **MailService RPC:** `MailManagement.GetAuditRecords`
- **Authorization:** `mail:read` (`TENANT_ADMIN`).
- **Response Example:**
  ```json
  {
    "records": [
      {
        "auditId": "01917b20-6d30-74e5-9c88-112233445566",
        "actorId": "01917b20-6d30-74e5-9c88-a1b2c3d4e5f6",
        "actorType": "User",
        "action": "ReleaseQuarantine",
        "resourceType": "QuarantineRecord",
        "resourceId": "01917b20-6d30-74e5-9c88-e1f2a3b4c5d6",
        "timestamp": "2026-08-22T04:40:00Z",
        "result": "Success",
        "detailJson": "{\"reason\":\"False positive spam detection confirmed by admin.\"}"
      }
    ],
    "nextPageToken": null
  }
  ```

---

### Group 9: Operations & Dead-Letter Requeue

#### 3.16 Requeue Dead-Letter Message
- **Function (Mô tả):** Replay một message bị lỗi nghiêm trọng hoặc fail vĩnh viễn (Dead Letter) để pipeline thử lại.
- **HTTP:**
  - `Method:` `POST`
  - `Path:` `/api/v1/system/mail/dead-letter/{id}/requeue`
- **MailService RPC:** `MailManagement.RequeueDeadLetter`
- **Authorization:** `SYSTEM_ADMIN` (Role-gate toàn quyền hệ thống).
- **Response Example:**
  ```json
  {
    "success": true,
    "message": "Processed message 01917b20-6d30-74e5-9c88-a9b8c7d6e5f4 requeued successfully."
  }
  ```
- **Side Effects:** Đặt lại trạng thái `PipelineStatus` về `Received`, kích hoạt Outbox event.

#### 3.17 Get System Audit Records
- **HTTP:** `GET /api/v1/system/mail/audit?pageSize=50&pageToken={token}`
- **Authorization:** `SYSTEM_ADMIN`.

---

## 4. Restricted BFF APIs & Internal RPC Summary

Tất cả **16 gRPC RPCs** được định nghĩa trong `protos/mail_platform.proto` đều đã được ánh xạ qua một trong ba Gateway BFF (`Staff.Bff`, `Admin.Bff`, `System.Bff`). Do đó, **số lượng RPC nội bộ thuần túy (True Internal-Only RPCs) = 0**.

Các RPC dưới đây được phân loại là **Restricted BFF APIs** do yêu cầu đặc quyền hạn chế:

| RPC Name | HTTP Route | Caller / Gate | Restricted Reason |
|---|---|---|---|
| `RequeueDeadLetter` | `/api/v1/system/mail/dead-letter/{id}/requeue` | `System.Bff` (`SYSTEM_ADMIN`) | Tác vụ khắc phục sự cố cấp hệ thống (Ops only). Không mở cho Tenant Admin hay Staff. |
| `DeleteQuarantine` | `/api/v1/admin/mail/quarantine/{id}` | `Admin.Bff` (`TENANT_ADMIN`) | Xóa vĩnh viễn mẫu mã độc/threat. Chỉ dành cho Tenant Administrator. |

---

## 5. Consolidated Permission Matrix

| API / Operation | Staff | Tenant Admin | System Admin | AI Agent | Internal Services |
|---|:---:|:---:|:---:|:---:|:---:|
| **Provision Domain** | ❌ | ✅ (`mail:create`) | ✅ | ❌ | ❌ |
| **Create Mailbox** | ❌ | ✅ (`mail:create`) | ✅ | ❌ | ❌ |
| **Create Alias** | ❌ | ✅ (`mail:create`) | ✅ | ❌ | ❌ |
| **Reset Password** | ❌ | ✅ (`mail:update`) | ✅ | ❌ | ❌ |
| **Create Draft** | ✅ (`mail:create`) | ✅ (`mail:create`) | ✅ | ✅ (Drafts only)| ❌ |
| **List/Get Drafts** | ✅ (`mail:read`) | ✅ (`mail:read`) | ✅ | ✅ | ❌ |
| **Submit Outbound Mail** | ✅ (`mail:send`) | ✅ (`mail:send`) | ✅ | ❌ (Strictly Blocked)| ✅ (`SendSystemEmailCommand` via MQ) |
| **Read Processed Messages**| ✅ (`mail:read`) | ✅ (`mail:read`) | ✅ | ❌ | ❌ |
| **Read Quarantine** | ✅ (`mail:read`) | ✅ (`mail:read`) | ✅ | ❌ | ❌ |
| **Release Quarantine** | ✅ (`mail:release`)| ✅ (`mail:release`)| ✅ | ❌ | ❌ |
| **Delete Quarantine** | ❌ | ✅ (`mail:delete`)| ✅ | ❌ | ❌ |
| **Query Audit Trail** | ❌ | ✅ (`mail:read`) | ✅ | ❌ | ❌ |
| **Requeue Dead Letter** | ❌ | ❌ | ✅ (`SYSTEM_ADMIN`)| ❌ | ❌ |

---

## 6. Infrastructure Dependency Matrix

| API / Operation | Neon DB | Redis | RabbitMQ | Stalwart | Cloudflare R2 | AI Governance | ClamAV | SpamAssassin |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Provision Domain** | ✓ | — | — | ✓ (JMAP) | — | — | — | — |
| **Create Mailbox** | ✓ | — | — | ✓ (JMAP) | — | — | — | — |
| **Create Alias** | ✓ | — | — | ✓ (JMAP) | — | — | — | — |
| **Create Draft** | ✓ | — | — | — | — | — | — | — |
| **Submit Outbound Mail**| ✓ | ✓ (RateLimit)| ✓ (Outbox)| ✓ (SMTP)| — | ✓ (BEC) | ✓ | — |
| **Inbound Pipeline** (Worker)| ✓ | — | ✓ (Events)| ✓ (Milter/LMTP)| ✓ (EML)| ✓ (Classify)| ✓ | ✓ |
| **Release Quarantine** | ✓ | — | — | ✓ (JMAP) | ✓ (Read EML)| — | — | — |
| **Delete Quarantine** | ✓ | — | — | — | — | — | — | — |
| **Requeue Dead Letter** | ✓ | — | ✓ (Outbox)| — | — | — | — | — |

---

## 7. Event Matrix

| Event Name | Producer | Trigger Condition | Consumers | Delivery Mechanism |
|---|---|---|---|---|
| `inbound_email_received_event` | MailService Inbound Pipeline | Nhận email hợp lệ, quét sạch mã độc/phishing | ShipmentWorkflow, Notification | Transactional Outbox (RabbitMQ) |
| `inbound_email_quarantined_event` | MailService Inbound Pipeline | Phát hiện mã độc, điểm spam vượt ngưỡng | Security/Alerting | Transactional Outbox (RabbitMQ) |
| `outbound_email_sent_event` | MailService Outbound Handler | Stalwart SMTP chấp nhận email (250 OK) | Audit, Analytics | Transactional Outbox (RabbitMQ) |
| `outbound_email_rejected_event` | MailService Outbound Handler | Bị chặn do chính sách, malware, hoặc rate-limit | Security/Alerting | Transactional Outbox (RabbitMQ) |
| `send_system_email_command` | Other Aurora Microservices | Cần gửi email hệ thống tự động | MailService | Direct RabbitMQ Consumer |

---

## 8. End-to-End Flow References

### 8.1 Create Draft Flow
```text
[Client / AI Agent]
       │
       ▼
 [BFF Gateway] ──── (Authenticate JWT & inject metadata)
       │
       ▼
 [MailSecurity.CreateDraftMessage]
       │
       ▼
 [MailService Handler] ──── (Compute ContentHash, Revision #1)
       │
       ▼
  [Neon PostgreSQL] ──── (Commit EmailDraft entity)
```

### 8.2 Outbound Send Flow (Synchronous Delivery)
```text
[Client (Staff / Admin)]
       │
       ▼
 [BFF Gateway] ──── (Validate JWT + mail:send permission + Attachment limits)
       │
       ▼
 [MailSecurity.SubmitOutboundMessage]
       │
       ├─► 1. Validate Draft Revision (if DraftRootId present)
       ├─► 2. ClamAV Scan (Attachments stream)
       ├─► 3. AI Governance (BEC & Phishing risk evaluation)
       ├─► 4. Redis Rate Limiter (Token bucket check)
       ├─► 5. MailKit SMTP (Submit to Stalwart on port 587 STARTTLS)
       │
       ▼  (Atomic Commit)
  [Neon PostgreSQL]
       ├─► Save ProcessedMessage (Status: Sent, QueueId)
       ├─► Mark EmailDraft as Sent
       └─► Write OutboundEmailSentEvent to Outbox
               │
               ▼
        [RabbitMQ Exchange]
```

### 8.3 Quarantine Release Flow
```text
[Admin / Staff]
       │
       ▼
 [BFF Gateway] ──── (Validate mail:release permission)
       │
       ▼
 [MailSecurity.ReleaseQuarantine]
       │
       ├─► 1. Fetch raw EML from Cloudflare R2
       ├─► 2. Deliver EML to Stalwart recipient inbox
       ├─► 3. Update QuarantineRecord (Status: Released)
       └─► 4. Write Audit Log
```

### 8.4 AI Agent Draft & Staff Approval Flow
```text
[AI Agent / Route Agent]
       │
       ▼ (Trusted Aurora service metadata: x-service-id, x-tenant-id)
 [MailSecurity.CreateDraftMessage]
       │
       ▼
 [Neon PostgreSQL] ──── (Save EmailDraft Revision #1, Source: "AI_Agent")
       │
       ▼
 [Staff Web UI / Staff.Bff] ──── (ListDrafts / GetDraft)
       │
       ▼
 [Staff Review & Edits] ──── (CreateDraftRevision #2 if modified)
       │
       ▼ (Staff triggers send with mail:send permission)
 [MailSecurity.SubmitOutboundMessage] ────► Stalwart SMTP ────► Internet
```

> [!CAUTION]
> **AI Agent Safety Rule:** AI Agent chỉ được phép tạo và đọc bản nháp (`CreateDraftMessage`, `GetDraft`). AI Agent **tuyệt đối không được cấp quyền** gọi trực tiếp `SubmitOutboundMessage` ra Internet. Mọi email gửi đi phải do Staff hoặc Tenant Admin xác nhận.

---

## 9. Authorization Scope Model

```text
================================================================================
AURORA IDENTITY & SCOPE ISOLATION MODEL
================================================================================

1. SYSTEM_ADMIN:
   - Được phép truy cập các route quản trị hệ thống (/api/v1/system/...).
   - Mang quyền PLATFORM scope đối với các tác vụ hạ tầng/ops đã được định nghĩa.

2. TENANT_ADMIN:
   - LUÔN LUÔN bị giới hạn trong phạm vi Tenant (TENANT Scope).
   - KHÔNG BAO GIỜ được cấp PLATFORM scope.
   - Toàn bộ truy vấn dữ liệu bị chặn qua DbContext Global Query Filter.

3. STAFF:
   - LUÔN LUÔN bị giới hạn trong phạm vi Tenant (TENANT Scope).
   - Phân quyền theo [RequirePermission] module mail.

4. AI AGENT:
   - Thực thi trong tenant/service execution context.
   - Không nhận quyền PLATFORM ngầm định.

5. FAIL-CLOSED PRINCIPLE:
   - Bất kỳ request nào thiếu TenantId hợp lệ (null/empty) trong bối cảnh Tenant
     sẽ lập tức bị từ chối (Fail-Closed) và KHÔNG BAO GIỜ rơi về PLATFORM scope.
================================================================================
```

---

## 10. Pagination Standard

Tất cả các API dạng danh sách (`List*`) tuân thủ chuẩn Token/Cursor Pagination:
- **Request parameters:**
  - `pageSize` (int, mặc định: 20, min: 1, max: 100).
  - `pageToken` (string, optional — cursor từ trang trước).
- **Response structure:**
  ```json
  {
    "items": [ ... ],
    "nextPageToken": "eyJvZmZzZXQiOjQwfQ=="
  }
  ```

---

## 11. API Lifecycle & Compatibility Rules

1. **Protobuf Backward Compatibility:**
   - Tuyệt đối không đánh lại số thứ tự (`field numbers`) trong `mail_platform.proto`.
   - Không tái sử dụng các field numbers đã bỏ.
   - Mọi trường mới phải là tùy chọn (`optional`) và được thêm vào cuối thông điệp.
2. **HTTP API Versioning:**
   - Mọi route đều có prefix `/api/v1/`.
   - Các thay đổi breaking phải được phát hành dưới `/api/v2/`.
3. **RabbitMQ Event Versioning:**
   - Sự kiện tuân thủ semantic versioning; breaking schema bắt buộc đặt tên sự kiện mới (vd: `outbound_email_sent_event_v2`).

---

## 12. Verification Checklist

- [x] 17/17 BFF Endpoints documented with accurate route, method, and DTOs.
- [x] 16/16 MailService gRPC RPCs documented and accounted for.
- [x] 0 True Internal-Only RPCs (Được phân loại lại thành 2 Restricted BFF APIs: DLQ Requeue và Quarantine Delete).
- [x] Tất cả Permissions khớp hoàn toàn với `PermissionConstants.cs`.
- [x] Outbound HTTP status chính xác là `200 OK` cho synchronous delivery pipeline.
- [x] Giới hạn Payload & Attachment được đồng bộ chặt chẽ: Decoded Single (25MB) < Decoded Total (50MB) < gRPC Message (75MB) < HTTP Body (80MB).
- [x] Quy trình AI Agent Draft $\rightarrow$ Staff Review $\rightarrow$ Send được ghi nhận chi tiết.
- [x] Mô hình Authorization Scope (PLATFORM vs TENANT) được định nghĩa fail-closed.

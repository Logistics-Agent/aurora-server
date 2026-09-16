# Hướng Dẫn Cấu Hình Mail Demo (Cloudflare Email Routing + Brevo SMTP)

Tài liệu này hướng dẫn thiết lập hệ thống Inbound và Outbound email cho Aurora Mail Platform demo nhanh mà không cần qua Stalwart mail server.

---

## 1. Kiến Trúc Hoạt Động

- **Inbound (Email Đến)**:
  `Internet Sender` ➔ `Cloudflare Email Routing` ➔ `Cloudflare Email Worker` ➔ `HTTPS POST` ➔ `Aurora MailService (/api/v1/mail/inbound/cloudflare)` ➔ `Security Pipeline (ClamAV, AI Phishing, Spam, DKIM/DMARC)` ➔ `Thread & DB` ➔ `Aurora UI`.
- **Outbound (Email Đi)**:
  `Aurora UI / API` ➔ `Outbound Security Pipeline` ➔ `MailKitSmtpDeliveryService` ➔ `Brevo SMTP Relay (port 587 STARTTLS)` ➔ `Internet Recipient`.

---

## 2. Các Bước Cấu Hình Cloudflare Email Routing & Worker

### Bước 2.1: Kích hoạt Cloudflare Email Routing
1. Đăng nhập vào Cloudflare Dashboard ➔ Chọn domain `e-verland.site`.
2. Chọn menu **Email Routing**.
3. Bấm **Enable Email Routing**. Cloudflare sẽ nhắc bạn thêm các bản ghi DNS MX & TXT (SPF):
   - **MX Records**:
     - Priority 19: `isaac.mx.cloudflare.net`
     - Priority 69: `linda.mx.cloudflare.net`
     - Priority 93: `amir.mx.cloudflare.net`
   - **TXT (SPF) Record**:
     - Name: `@`
     - Value: `v=spf1 include:_spf.mx.cloudflare.net ~all` (hoặc kết hợp với Brevo: `v=spf1 include:_spf.mx.cloudflare.net include:spf.brevo.com ~all`).

### Bước 2.2: Tạo Cloudflare Email Worker
1. Trên Cloudflare Dashboard ➔ **Workers & Pages** ➔ **Create Application** ➔ **Create Worker**.
2. Đặt tên worker: `aurora-email-worker`.
3. Dán mã nguồn từ file [docs/cloudflare-worker/email-worker.js](file:///d:/IT/CD/aurora-server/src/dotnet/MailService/docs/cloudflare-worker/email-worker.js).
4. Vào **Settings** ➔ **Variables** ➔ Thêm các biến môi trường:
   - `AURORA_INBOUND_URL`: `https://<DOMAIN_HOAC_INGRESS_AURORA>/api/v1/mail/inbound/cloudflare`
   - `AURORA_WEBHOOK_SECRET`: Chuỗi secret ngẫu nhiên (ví dụ: `cf_webhook_secret_demo_2026_x89f2`).
5. Bấm **Save and Deploy**.

### Bước 2.3: Thiết lập Định Tuyến (Routing Rule)
1. Quay lại **Email Routing** ➔ Tab **Routing Rules**.
2. Chọn **Custom addresses** ➔ Bấm **Create address**:
   - **Custom address**: `ops@e-verland.site` (hoặc Catch-all nếu muốn nhận tất cả địa chỉ).
   - **Action**: `Send to a Worker`.
   - **Destination Worker**: Chọn `aurora-email-worker`.
3. Bấm **Save**.

---

## 3. Cấu Hình Brevo (Outbound SMTP Relay)

### Bước 3.1: Xác thực Domain trong Brevo
1. Đăng nhập vào [Brevo Console](https://app.brevo.com/) ➔ **Senders, Domains & Dedicated IPs** ➔ **Domains**.
2. Thêm domain `e-verland.site`.
3. Thêm các bản ghi DKIM và SPF do Brevo cung cấp vào DNS của Cloudflare.
4. Xác thực Sender: Tạo sender `ops@e-verland.site`.

### Bước 3.2: Lấy thông tin SMTP Relay Key
1. Vào mục **SMTP & API** ➔ Tab **SMTP**.
2. Ghi nhận các thông tin:
   - **SMTP Server**: `smtp-relay.brevo.com`
   - **Port**: `587`
   - **Login**: Email đăng nhập Brevo (ví dụ: `admin@e-verland.site` hoặc tài khoản Brevo của bạn).
   - **Master Password / SMTP Key**: Bấm **Generate a new SMTP key** và lưu lại.

---

## 4. Cấu Hình Secrets & Environment Variables trong Aurora MailService

### Kubernetes / Azure Key Vault (AKV) Secret Mapping
Cập nhật Secret trong cụm k8s hoặc file `.env` / `appsettings.json`:

```ini
# Chọn Transport Provider
MailTransport__Provider=Brevo

# Brevo Outbound Credentials
Brevo__SmtpHost=smtp-relay.brevo.com
Brevo__SmtpPort=587
Brevo__SmtpUsername=<BREVO_SMTP_LOGIN>
Brevo__SmtpPassword=<BREVO_SMTP_KEY>

# Cloudflare Inbound Webhook Secret (Khớp với AURORA_WEBHOOK_SECRET trên Worker)
CloudflareInbound__WebhookSecret=<CF_WEBHOOK_SECRET>
```

---

## 5. Kế Hoạch Kiểm Thử (Test Plan)

### 5.1. Test Outbound (Gửi Email từ Aurora ➔ Gmail)
- Gửi từ giao diện Aurora UI hoặc gọi API với Sender: `ops@e-verland.site`, Recipient: `personal@gmail.com`.
- **Kỳ vọng**:
  - MailService log: `SMTP delivery succeeded via provider Brevo (smtp-relay.brevo.com:587). ProviderMessageId: ..., Status: Success`.
  - Hộp thư Gmail nhận được email với người gửi `ops@e-verland.site`.

### 5.2. Test Inbound (Gửi Email từ Gmail ➔ Aurora)
- Dùng Gmail cá nhân gửi tới `ops@e-verland.site` với tiêu đề `AURORA CF INBOUND DEMO 01`.
- **Kỳ vọng**:
  - Cloudflare Worker thực thi thành công và chuyển tiếp HTTP POST tới `/api/v1/mail/inbound/cloudflare`.
  - MailService log: `Cloudflare inbound accepted and processed: DeliveryId=..., MessageId=..., ThreadId=...`.
  - Trên Aurora UI, thread mới xuất hiện trong hộp thư `ops@e-verland.site`.

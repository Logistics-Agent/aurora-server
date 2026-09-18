# Hướng Dẫn Cấu Hình Mail Demo (Cloudflare Email Routing + Brevo SMTP)

Tài liệu này hướng dẫn thiết lập hệ thống Inbound và Outbound email cho Aurora Mail Platform theo kiến trúc production/demo:
- **Inbound**: Cloudflare Email Routing + Cloudflare Email Worker
- **Outbound**: Brevo SMTP Relay (port 587 STARTTLS)
- **AI Governance**: Dedicated VM `http://10.30.2.10:9090` (AKS AI đã dừng)
- **Legacy / Fallback**: Stalwart Mail Server (không nằm trong critical path nhưng sẵn sàng cho rollback/demo khi cần)

---

## 1. Kiến Trúc Hoạt Động

```text
Inbound Mail:
Internet Sender
  → Cloudflare Email Routing
  → Cloudflare Email Worker
  → Aurora MailService Webhook (/api/v1/mail/cloudflare/inbound)
  → AI Governance (http://10.30.2.10:9090)
  → Mailbox / Database / Aurora UI

Outbound Mail:
Aurora Mail UI / API
  → MailService (Outbound Pipeline & IMailTransport)
  → Brevo SMTP Relay (smtp-relay.brevo.com:587 STARTTLS)
  → External Recipient
```

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
     - Value: `v=spf1 include:_spf.mx.cloudflare.net include:spf.brevo.com ~all`

### Bước 2.2: Tạo Cloudflare Email Worker
1. Trên Cloudflare Dashboard ➔ **Workers & Pages** ➔ **Create Application** ➔ **Create Worker**.
2. Đặt tên worker: `aurora-email-worker`.
3. Dán mã nguồn từ file [docs/cloudflare-worker/email-worker.js](file:///d:/IT/CD/aurora-server/src/dotnet/MailService/docs/cloudflare-worker/email-worker.js).
4. Vào **Settings** ➔ **Variables** ➔ Thêm các biến môi trường:
   - `AURORA_INBOUND_URL`: `https://api.humanak.cyou/api/v1/mail/inbound/cloudflare`
   - `AURORA_WEBHOOK_SECRET`: Shared secret (khớp với secret `aurora-mail-cloudflare-webhook-secret`).
5. Bấm **Save and Deploy**.

> Không để Worker dùng endpoint cũ `api.e-verland.site`. Worker source có
> fallback production về `api.humanak.cyou`, nhưng sau khi sửa source vẫn phải
> bấm **Save and Deploy** trên Cloudflare để code mới có hiệu lực.

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

> `senderAddress` khi gọi API phải là sender/domain đã được xác thực trong Brevo.
> Nếu môi trường tenant đang dùng `staff@guardm.space` thì phải xác thực
> `guardm.space` trong Brevo và dùng đúng SMTP key của workspace đó; không thể
> dùng key của `e-verland.site` rồi kỳ vọng Gmail nhận thư từ `guardm.space`.

Nếu email có attachment, ClamAV phải reachable tại
`clamav.aurora.svc.cluster.local:3310`. MailService cố ý từ chối/defer
attachment khi ClamAV down; không bypass bước này trong production.

### Bước 3.2: Lấy thông tin SMTP Relay Key
1. Vào mục **SMTP & API** ➔ Tab **SMTP**.
2. Ghi nhận các thông tin:
   - **SMTP Server**: `smtp-relay.brevo.com`
   - **Port**: `587`
   - **Login**: Email đăng nhập Brevo (ví dụ: `ops@e-verland.site` hoặc tài khoản Brevo của bạn).
   - **Master Password / SMTP Key**: Bấm **Generate a new SMTP key** và lưu vào Azure Key Vault.

---

## 4. Cấu Hình Secrets & Lệnh Dùng Cho AKS

### 4.1. Thêm Secret vào Azure Key Vault (Khuyến nghị cho Production/ExternalSecrets)
Chạy các lệnh Azure CLI để tạo 3 secret cần thiết trong Key Vault:

```bash
KEYVAULT_NAME="<your-keyvault-name>" # ví dụ: kv-aurora-prod

# 1. Thêm Brevo SMTP Username
az keyvault secret set \
  --vault-name "$KEYVAULT_NAME" \
  --name "aurora-mail-brevo-smtp-username" \
  --value "<BREVO_SMTP_LOGIN>"

# 2. Thêm Brevo SMTP Password / Key
az keyvault secret set \
  --vault-name "$KEYVAULT_NAME" \
  --name "aurora-mail-brevo-smtp-password" \
  --value "<BREVO_SMTP_KEY>"

# 3. Thêm Cloudflare Inbound Webhook Secret
az keyvault secret set \
  --vault-name "$KEYVAULT_NAME" \
  --name "aurora-mail-cloudflare-webhook-secret" \
  --value "<SHARED_CF_WEBHOOK_SECRET>"
```

### 4.2. ExternalSecrets Mapping trên Kubernetes
ExternalSecret trong `deploy/helm/values.yaml` tự động đồng bộ 3 keys vào `mail-service-kv-secret`:
- `Brevo__SmtpUsername` ← `aurora-mail-brevo-smtp-username`
- `Brevo__SmtpPassword` ← `aurora-mail-brevo-smtp-password`
- `CloudflareInbound__WebhookSecret` ← `aurora-mail-cloudflare-webhook-secret`

### 4.3. Lệnh Deploy & Restart MailService trên AKS

```bash
# 1. Kết nối tới AKS cluster
az aks get-credentials --resource-group <RESOURCE_GROUP> --name <AKS_CLUSTER_NAME>

# 2. Upgrade Helm Chart
helm upgrade --install mail-service ./deploy/helm \
  --namespace aurora \
  --values ./deploy/helm/values.yaml

# 3. Restart Pods để nhận config và secret mới
kubectl rollout restart deployment/mail-service -n aurora

# 4. Kiểm tra trạng thái Rollout
kubectl rollout status deployment/mail-service -n aurora

# 5. Xem Live Logs của MailService
kubectl logs -n aurora -l app.kubernetes.io/name=mail-service -f --tail=100
```

---

## 5. Kế Hoạch Kiểm Thử (Test Plan)

### 5.1. Test Outbound (Gửi Email từ Aurora ➔ Gmail)
- Gửi từ giao diện Aurora UI hoặc gọi API với Sender: `ops@e-verland.site`, Recipient: `personal@gmail.com`.
- **Kỳ vọng**:
  - MailService log: `SMTP delivery succeeded via provider Brevo (smtp-relay.brevo.com:587). Recipients: ..., ProviderMessageId: ..., Status: Success`.
  - Hộp thư người nhận nhận được email từ `ops@e-verland.site`.

> HTTP `200`/`processedMessageId` chỉ xác nhận Brevo đã nhận lệnh SMTP (250),
> không phải Gmail đã đặt thư vào Inbox. Khi không thấy thư, kiểm tra Spam,
> Brevo Transactional > Logs và `GET /api/v1/mail/messages/{processedMessageId}`
> để xem stage `StalwartSmtpSubmission`.

### 5.2. Test Inbound (Gửi Email từ Gmail ➔ Aurora)
- Dùng Gmail cá nhân gửi tới `ops@e-verland.site` với tiêu đề `AURORA INBOUND DEMO`.
- **Kỳ vọng**:
  - Cloudflare Worker chuyển tiếp HTTP POST tới `/api/v1/mail/cloudflare/inbound`.
  - MailService log: `Inbound source = Cloudflare, DeliveryId = ..., Recipient = ops@e-verland.site, MessageId = ...`.
  - AI Phishing và Security Checks được thực thi qua AI Governance `http://10.30.2.10:9090`.
  - Thread và tin nhắn xuất hiện trong hộp thư `ops@e-verland.site`.
  - Cloudflare retry không tạo trùng lặp tin nhắn (idempotent).

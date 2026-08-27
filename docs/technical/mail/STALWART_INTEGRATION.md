# Aurora Mail Platform — Stalwart Mail Server Integration

> **Status**: AUTHORITATIVE / PRODUCTION ARCHITECTURE (CODE-FIRST)  
> **Source-of-Truth**: Audited against `stalwartlabs/mail-server:v0.10.8`, `config.toml`, `StalwartManagementClient.cs`, `StalwartSmtpClient.cs`, `docker-compose.prod.yml`, and `MailAdminController.cs`.

---

## 1. Role of Stalwart in the Aurora Architecture

**Stalwart** is an all-in-one, high-performance mail server written in Rust that provides:
- **Inbound SMTP Server (Port 25)**: Receives external emails from internet MTAs.
- **Authenticated Submission (Port 587)**: Accepts outbound messages from `MailService` with STARTTLS.
- **IMAPS Service (Port 993)**: Encrypted mailbox access.
- **JMAP / REST Management API (Port 8080)**: Programmatic domain, mailbox, and alias provisioning.
- **DKIM Signing Engine**: Cryptographically signs outgoing emails with tenant-specific RSA-2048 private keys.

```
Internet MTAs ──(SMTP Port 25)──> [ Stalwart ] ──(Inbound Webhook)──> MailService
                                         ▲
MailService ──(Submission Port 587)─────┘ (Outbound SMTP with DKIM Signing)
```

---

## 2. Network Topology & Ports

In production, Stalwart runs as a Docker container (`aurora-stalwart`) on the dedicated mail node:

| Port | Protocol | Binding | Purpose |
|---|---|---|---|
| **`25`** | SMTP | `0.0.0.0:25` (Public Internet) | Inbound mail reception from internet MTAs. |
| **`587`** | SMTP Submission | `0.0.0.0:587` (STARTTLS) | Authenticated outbound message submission. |
| **`993`** | IMAPS | `0.0.0.0:993` (TLS) | Secure IMAP fetch for third-party email clients. |
| **`4190`** | ManageSieve | `0.0.0.0:4190` | Sieve email filtering rules. |
| **`8080`** | HTTP REST / JMAP | **`127.0.0.1:8080` (Loopback Only)** | Administrative provisioning API for `MailService`. |

> [!CAUTION]
> Port `8080` must **NEVER** be exposed to public networks. It is bound strictly to `127.0.0.1` and accessed by `MailService` via the internal Docker bridge network (`mail_internal`).

---

## 3. Configuration Specification (`config.toml`)

Stalwart is configured via `/opt/stalwart/config.toml`:

```toml
[server]
hostname = "mail.aurora-logistics.com"

[storage]
data = "/opt/stalwart/data"
blob = "/opt/stalwart/blobs"

[certificate."default"]
cert = "/opt/stalwart/certs/fullchain.pem"
private-key = "/opt/stalwart/certs/privkey.pem"

[session.smtp]
auth = true
tls = "required"
max-message-size = 26214400 # 25MB Max Message Size

[session.submission]
auth = true
tls = "required"

[management]
enabled = true
bind = "0.0.0.0:8080"
```

---

## 4. Provisioning Integration Flows (`StalwartManagementClient`)

`MailService` communicates with Stalwart's management API over HTTP REST/JMAP:

### 4.1 Domain Provisioning & DKIM Generation
When a tenant admin provisions a domain (`POST /api/v1/admin/mail/domains`):
1. `MailService` calls Stalwart: `POST /api/v1/domains` with `{ "domain": "acmelogistics.com" }`.
2. Stalwart generates a unique **RSA-2048 DKIM Keypair** for selector `aurora-2025`.
3. `MailService` saves the public key `dkimTxtRecord` in `Domain` table and returns DNS configuration records to the Tenant Admin.

### 4.2 Mailbox Creation
When a shared mailbox is created (`POST /api/v1/admin/mail/mailboxes`):
1. `MailService` calls Stalwart: `POST /api/v1/accounts` with `{ "name": "ops@acmelogistics.com", "secret": "..." }`.
2. Registers local part and full address in `Mailbox` table.

### 4.3 Alias Creation
When an email alias is created (`POST /api/v1/admin/mail/aliases`):
1. `MailService` calls Stalwart: `POST /api/v1/aliases` with `{ "address": "support@acmelogistics.com", "targets": ["ops@acmelogistics.com"] }`.

---

## 5. Health Checks & Monitoring

Stalwart exposes a health check endpoint:
```bash
curl -f http://localhost:8080/healthz
```
- Monitored by Docker healthcheck every 10 seconds.
- If Stalwart fails 3 consecutive health checks, Docker marks the container unhealthy and alerts SRE via `monitor-resources.sh`.
